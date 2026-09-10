using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, hjemmel-validering-runden, 2026-09-10, issue #233] Sjekker om hjemmelrelasjonene faktisk
/// peker på bestemmelser som FINNES.
///
/// <para>
/// Bakgrunn: vi har aldri målt det. #217 målte hvor mange hjemler som kunne løses til et dypere
/// nivå, men ikke om paragraf-eId-en i seg selv treffer noe. Johann fant tilfellet ved å spørre hva
/// som skiller Jan Mayen-forskriften fra andre: hjemmelen der er lagret som <c>…/nor/§1.</c> — med
/// punktum, fordi Lovdatas «Hjemmel»-felt avslutter setningen. Noden heter <c>§1</c>. Referansen kan
/// altså ikke følges, og har aldri kunnet følges.
/// </para>
///
/// <para>
/// <b>Fire utfall, og bare ETT av dem er en feil.</b> Det er hele poenget med denne tjenesten: et
/// enkelt «gyldig/ugyldig»-tall ville blandet sammen ekte parsefeil med to helt legitime tilstander,
/// og gjort målingen ubrukelig som kvalitetsmål.
/// </para>
/// </summary>
public sealed class HjemmelValideringTjeneste(RegelIdeDbContext db)
{
    /// <summary>Hva én hjemmelrelasjon er.</summary>
    public enum Utfall
    {
        /// <summary>eId-en treffer en ekte node i den refererte rettskilden. Referansen kan følges.</summary>
        Gyldig,

        /// <summary>
        /// eId-en ER dokumentets egen ELI, uten paragrafdel. LEGITIMT, ikke en mangel: en
        /// delegeringsforskrift er hjemlet i en HEL forskrift/kongelig resolusjon, ikke i én
        /// bestemmelse (målt 2026-09-02: 1711 dokumenter har dette mønsteret).
        /// </summary>
        Dokumentniva,

        /// <summary>
        /// Målet er en referanse-stub uten noder — loven er referert, men ikke importert. LEGITIMT:
        /// referansen kan ikke verifiseres internt, men den er ikke dermed gal. Dette er tallet som
        /// avgjør om en EKSTERN sjekk mot Lovdata er verdt å bygge.
        /// </summary>
        MaaletIkkeImportert,

        /// <summary>
        /// Loven ER importert og har noder, men INGEN av dem har denne eId-en. Dette er den ekte
        /// feilen — «§1.»-klassen. En referanse som ser gyldig ut og ikke kan følges.
        /// </summary>
        NodeFinnesIkke,
    }

    /// <param name="RettskildeId">Dokumentet hjemmelen står i (typisk en forskrift).</param>
    /// <param name="RettskildeTittel">Tittelen, slik rapporten kan leses uten flere oppslag.</param>
    /// <param name="HjemmelEid">eId-en som ble sjekket.</param>
    /// <param name="Utfall">Hvilken av de fire kategoriene raden faller i.</param>
    public sealed record Rad(Guid RettskildeId, string RettskildeTittel, string HjemmelEid, Utfall Utfall);

    /// <param name="Antall">Hjemmelrelasjoner gjennomgått.</param>
    /// <param name="Gyldig">Treffer en ekte node.</param>
    /// <param name="Dokumentniva">Peker på et helt dokument — riktig for delegeringskjeder.</param>
    /// <param name="MaaletIkkeImportert">Kan ikke verifiseres internt fordi loven mangler noder.</param>
    /// <param name="NodeFinnesIkke">EKTE feil: loven har noder, men ingen med denne eId-en.</param>
    /// <param name="Feilrader">De faktiske feilradene, til triage. Avgrenset av <c>maksFeilrader</c>.</param>
    public sealed record Resultat(
        int Antall,
        int Gyldig,
        int Dokumentniva,
        int MaaletIkkeImportert,
        int NodeFinnesIkke,
        IReadOnlyList<Rad> Feilrader);

    /// <param name="maksFeilrader">
    /// Hvor mange feilrader som tas med i svaret. Tallene er alltid komplette; det er bare LISTEN som
    /// avgrenses, slik at et svar ikke blir uleselig hvis en fremtidig parsefeil rammer tusenvis.
    /// </param>
    public async Task<Resultat> ValiderAsync(int maksFeilrader = 200, CancellationToken ct = default)
    {
        var gyldig = 0;
        var dokumentniva = 0;
        var ikkeImportert = 0;
        var manglerNode = 0;
        var antall = 0;
        var feilrader = new List<Rad>();

        // Hvilke rettskilder har i det hele tatt noder? Ett spørsmål for hele korpuset, ikke ett per
        // hjemmel: uten dette ville skillet mellom «ikke importert» og «node finnes ikke» kostet et
        // ekstra rundtur per rad, og det er nettopp det skillet som gjør rapporten brukbar.
        var harNoder = (await db.RettskildeNoder
                .Select(n => n.RettskildeId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        // Gruppert per målrettskilde: nodenes eId-er hentes ÉN gang per lov, ikke én gang per hjemmel.
        // De hyppigst refererte lovene har hundrevis av hjemler pekende på seg.
        var hjemler = await db.RettskildeHjemler
            .Join(db.Rettskilder, h => h.RettskildeId, r => r.Id, (h, r) => new { h.HjemmelRettskildeId, h.HjemmelEid, h.RettskildeId, r.Tittel })
            .ToListAsync(ct);

        foreach (var gruppe in hjemler.GroupBy(h => h.HjemmelRettskildeId))
        {
            var maalHarNoder = harNoder.Contains(gruppe.Key);
            var eider = maalHarNoder
                ? (await db.RettskildeNoder
                    .Where(n => n.RettskildeId == gruppe.Key)
                    .Select(n => n.Eid)
                    .ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal)
                : [];

            foreach (var h in gruppe)
            {
                antall++;
                var utfall = ErDokumentniva(h.HjemmelEid) ? Utfall.Dokumentniva
                    : !maalHarNoder ? Utfall.MaaletIkkeImportert
                    : eider.Contains(h.HjemmelEid) ? Utfall.Gyldig
                    : Utfall.NodeFinnesIkke;

                switch (utfall)
                {
                    case Utfall.Gyldig: gyldig++; break;
                    case Utfall.Dokumentniva: dokumentniva++; break;
                    case Utfall.MaaletIkkeImportert: ikkeImportert++; break;
                    case Utfall.NodeFinnesIkke:
                        manglerNode++;
                        if (feilrader.Count < maksFeilrader)
                        {
                            feilrader.Add(new Rad(h.RettskildeId, h.Tittel, h.HjemmelEid, utfall));
                        }
                        break;
                }
            }
        }

        return new Resultat(antall, gyldig, dokumentniva, ikkeImportert, manglerNode, feilrader);
    }

    /// <param name="Undersokt">Hjemmelrader gjennomgått.</param>
    /// <param name="Rettet">Fikk setningstegnet fjernet fra eId-en.</param>
    /// <param name="RettetOgLoserNa">…og treffer nå en ekte node. Den egentlige gevinsten.</param>
    /// <param name="SlettetSomDuplikat">Raden ble borte fordi den trimmede eId-en ALT fantes på samme
    /// rettskilde — se <see cref="RettSetningstegnAsync"/> for hvorfor det er riktig utfall.</param>
    public sealed record Rettelse(int Undersokt, int Rettet, int RettetOgLoserNa, int SlettetSomDuplikat);

    /// <summary>
    /// [Ny, hjemmel-validering-runden, 2026-09-10, issue #233] Fjerner avsluttende setningstegn fra
    /// LAGREDE hjemmel-eId-er. Parseren gjør det samme for nye importer
    /// (<c>LovdataHtmlParser.UtenSetningstegn</c>) — denne retter de radene som alt er skrevet.
    ///
    /// <para>
    /// Ren strengoperasjon på lagrede rader, ingen reparsing og ingenting eksternt: trimmingen er
    /// bevist trygg (0 av 5070 paragrafnoder har punktum i nummeret), så resultatet er determinert av
    /// den lagrede verdien alene.
    /// </para>
    ///
    /// <para>
    /// <b>Duplikater:</b> <c>ux_rettskilde_hjemler_rettskilde_id_hjemmel_eid</c> krever unik
    /// (rettskilde, eId). Har et dokument BÅDE <c>§1</c> og <c>§1.</c> — samme hjemmel skrevet to
    /// ganger, én gang med setningstegn — ville trimmingen kollidert. Da slettes den trimmede
    /// duplikaten i stedet: de to radene betegnet alltid samme bestemmelse, og den ene var en
    /// skrivemåte, ikke en egen hjemmel.
    /// </para>
    ///
    /// <para>Idempotent: en rad uten setningstegn røres ikke.</para>
    /// </summary>
    public async Task<Rettelse> RettSetningstegnAsync(CancellationToken ct = default)
    {
        var undersokt = 0;
        var rettet = 0;
        var loserNa = 0;
        var slettet = 0;

        // Kun radene som FAKTISK har et avsluttende tegn — resten er det ingen grunn til å laste.
        var kandidater = await db.RettskildeHjemler
            .Where(h => h.HjemmelEid.EndsWith(".") || h.HjemmelEid.EndsWith(",") || h.HjemmelEid.EndsWith(";"))
            .ToListAsync(ct);

        foreach (var h in kandidater)
        {
            undersokt++;
            var ny = h.HjemmelEid.TrimEnd('.', ',', ';');
            if (ny == h.HjemmelEid) continue;

            var finnesAlt = await db.RettskildeHjemler
                .AnyAsync(a => a.RettskildeId == h.RettskildeId && a.HjemmelEid == ny && a.Id != h.Id, ct);
            if (finnesAlt)
            {
                db.RettskildeHjemler.Remove(h);
                slettet++;
                continue;
            }

            h.HjemmelEid = ny;
            rettet++;
            if (await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == h.HjemmelRettskildeId && n.Eid == ny, ct))
            {
                loserNa++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new Rettelse(undersokt, rettet, loserNa, slettet);
    }

    /// <summary>
    /// Er eId-en dokumentets egen ELI, uten paragrafdel? Sjekken er «finnes det noe etter /nor», ikke
    /// et mønster på paragrafnummeret — dokument-ELI-er forekommer også UTEN <c>/nor</c> (målt: alle
    /// dokumentnivå-hjemler mangler segmentet, fordi Lovdatas href gjør det), og da er det heller
    /// ingenting etter det.
    /// </summary>
    private static bool ErDokumentniva(string eid)
    {
        var idx = eid.IndexOf("/nor", StringComparison.Ordinal);
        return idx < 0 || eid.Length <= idx + "/nor".Length + 1;
    }
}
