using System.Text.RegularExpressions;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Det deterministiske laget i den automatiske
/// konverteringen (docs/33 §5.2 punkt 1): navngitte mønstre over nodeteksten, fasit-formatet ut.
/// <para>
/// <b>Hva mønstrene er valgt etter.</b> Startsettet er formuleringene korpusmålingen i docs/33 §1 fant
/// høy presisjon for (kompetanse knyttet til en bestemmelse dominerer; kanter mellom aktører er få).
/// Hvert mønster har korpustallet sitt i <see cref="Strukturmonster.Korpusgrunnlag"/>, og presisjonen
/// mot fasiten for de fem kildene måles per mønster i
/// <c>RegelIde.Data.Tests/Strukturfasit/</c> → <c>data/fasit/strukturmodell/maling-monster.md</c>.
/// </para>
/// <para>
/// <b>Hva laget bevisst IKKE gjør</b> (#307, CLAUDE.md §8):
/// </para>
/// <list type="bullet">
/// <item>Ingen oppslag mot virksomhetskatalogen og ingen fuzzy-treff: aktører identifiseres kun ved
/// tekstformen slik den står. Oppløsning mot katalog og område er #313/#314.</item>
/// <item>Ingen gjetting av endepunkter: kan setningen ikke avgjøre fra/til, blir de null — f.eks.
/// førsteinstansen i «… kan påklages til klagenemnda», eller «disse» i «delegere myndighet til disse».</item>
/// <item>Ingen kontekst utover setningen: «departementet» oppløses ikke til lovens departement, og et
/// pronomen følges ikke tilbake til forrige ledd. Det er nettopp de tilfellene KI-laget (#308) skal måles på.</item>
/// <item>Ingen verdier for entitetstype/navngitt/referent/oppløsning/sikkerhet: de krever skjønn eller
/// oppslag, og står null. Fasiten fyller dem; målingen sammenligner dem ikke.</item>
/// </list>
/// </summary>
public sealed class MonsterStrukturkonverterer : IStrukturkonverterer
{
    /// <summary>Verdien i <see cref="Strukturdokument.AnnotertAv"/>. Uten dato: utdata skal være deterministisk.</summary>
    public const string AnnotertAv = "MonsterStrukturkonverterer (deterministisk mønsterlag, #307) — maskinelt, ikke verifisert";

    private const int MaksEidEksempler = 5;

    /// <summary>
    /// Setningsgrense: punktum/semikolon/kolon fulgt av mellomrom og stor bokstav (evt. etter «[», «« » eller
    /// «(»). Dato- og nummerforkortelser («29. juni», «nr. 50») splittes derfor ikke — neste tegn er
    /// liten bokstav eller siffer.
    /// </summary>
    private static readonly Regex Setningsgrense = new(@"(?<=[.;:!?])\s+(?=[\[«(]?\p{Lu})", RegexOptions.CultureInvariant);

    /// <summary>Alle mønstrene, i den rekkefølgen de kjøres. Rekkefølgen avgjør aktør-id-ene i utdata.</summary>
    public static IReadOnlyList<Strukturmonster> Monstre { get; } = Monsterkatalog.Bygg();

    /// <summary>
    /// [Ny, issue #341 + #335 AC2, 2026-10-08] Kan kompetansen delegeres? Avgjort på SITATET, ikke på aktøren (fasitens
    /// «Kongen»-aktør har «Kongen i statsråd» som variant): «Kongen i statsråd …» → false, ellers en fra-aktør «Kongen» →
    /// true, og «X selv» («kommunestyret selv») → false. Alt annet: null (ikke angitt) — samme regel som
    /// <c>konvertering-341-kompetanse.py</c> brukte på fasiten.
    /// </summary>
    internal static bool? Delegerbar(string? fra, string sitat)
    {
        if (fra is null) return null;
        if (Regex.IsMatch(sitat, Regex.Escape(fra) + @"\s+selv\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        if (!fra.StartsWith("Kongen", StringComparison.OrdinalIgnoreCase)) return null;
        if (sitat.Contains("Kongen i statsråd", StringComparison.Ordinal)) return false;
        return string.Equals(fra, "Kongen", StringComparison.OrdinalIgnoreCase) ? true : null;
    }

    /// <inheritdoc />
    public Strukturdokument Konverter(Strukturkonverteringsgrunnlag grunnlag)
    {
        ArgumentNullException.ThrowIfNull(grunnlag);

        var aktorer = new AktorBygger();
        var utsagn = new List<(string Eid, string Sitat, Strukturmonster Monster, string? Fra, string? Til, string? Objekt, string Polaritet)>();
        var sett = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in grunnlag.Noder)
        {
            if (string.IsNullOrWhiteSpace(node.Tekst)) continue;

            var setninger = Setninger(node.Tekst);
            for (var s = 0; s < setninger.Count; s++)
            {
                foreach (var monster in Monstre)
                {
                    foreach (var funn in monster.Finn(setninger[s]))
                    {
                        var fraListe = funn.Fra.Count == 0 ? [null] : funn.Fra.Select(f => (string?)f).ToList();
                        var tilListe = funn.Til.Count == 0 ? [null] : funn.Til.Select(t => (string?)t).ToList();
                        foreach (var fra in fraListe)
                        {
                            foreach (var til in tilListe)
                            {
                                // Samme utsagn fra to mønstre i samme setning (f.eks. «kan i forskrift gi
                                // nærmere forskrifter») skal ikke telle dobbelt — første mønster vinner.
                                var nokkel = string.Join('\u001f', node.Eid, s, monster.Kategori, monster.Type,
                                    fra?.ToLowerInvariant(), til?.ToLowerInvariant(), funn.Sitat);
                                if (!sett.Add(nokkel)) continue;
                                utsagn.Add((node.Eid, funn.Sitat, monster, fra, til, funn.Objekt, funn.Polaritet));
                            }
                        }
                    }
                }
            }
        }

        var ferdigeUtsagn = new List<StrukturUtsagn>(utsagn.Count);
        for (var i = 0; i < utsagn.Count; i++)
        {
            var u = utsagn[i];
            var fraId = u.Fra is null ? null : aktorer.Registrer(u.Fra, u.Eid);
            var tilId = u.Til is null ? null : aktorer.Registrer(u.Til, u.Eid);
            ferdigeUtsagn.Add(new StrukturUtsagn(
                Id: "u" + (i + 1),
                Eid: u.Eid,
                Sitat: u.Sitat,
                Kategori: u.Monster.Kategori,
                Type: u.Monster.Type,
                Fra: fraId,
                Til: tilId,
                Objekt: u.Objekt,
                Polaritet: u.Polaritet,
                Avgrensning: null,
                Betinget: null,
                KildeUtenforKorpus: null,
                Sikkerhet: null,
                Kommentar: null)
            {
                Oppdagelseskilde = u.Monster.Oppdagelseskilde,
                // [Ny, issue #341] Normformen fra leksikonet, og delegerbar etter #335-regelen avgjort på sitatet.
                Normform = u.Monster.Normform,
                Delegerbar = u.Monster.Kategori == "kompetanse" ? Delegerbar(u.Fra, u.Sitat) : null,
            });
        }

        var ledsagende = grunnlag.Noder
            .Where(n => !string.Equals(n.Eli, grunnlag.Eli, StringComparison.Ordinal))
            .Select(n => new StrukturLedsagendeKilde(n.Rettskilde, n.Eli))
            .Distinct()
            .ToList();

        return new Strukturdokument(
            Rettskilde: grunnlag.Rettskilde,
            Eli: grunnlag.Eli,
            Ledsagende: ledsagende,
            AnnotertAv: AnnotertAv,
            NoderLest: grunnlag.Noder.Count,
            Aktorer: aktorer.Bygg(),
            Utsagn: ferdigeUtsagn);
    }

    /// <summary>Deler en nodetekst i setninger. Hver setning er en eksakt delstreng av teksten.</summary>
    public static IReadOnlyList<string> Setninger(string tekst) =>
        Setningsgrense.Split(tekst).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    /// <summary>
    /// Samler aktørene: én per distinkt tekstform (uten skille på store/små bokstaver, som i
    /// målingens treffregel). Første forekomst blir <c>tekstform</c>; andre skrivemåter og — for navn med
    /// skråstrek («Hammerfest/Hámmerfeasta», «Østre valgkrets/Nuortaguovllu válgabiire») — hver av
    /// språkformene teksten selv oppgir, blir <c>varianter</c>. Skråstrek-delingen er tekstens egen
    /// alternasjon, ikke en normalisering.
    /// </summary>
    private sealed class AktorBygger
    {
        private readonly Dictionary<string, Oppforing> _perNokkel = new(StringComparer.Ordinal);
        private readonly List<Oppforing> _rekkefolge = [];

        public string Registrer(string tekstform, string eid)
        {
            var nokkel = tekstform.ToLowerInvariant();
            if (!_perNokkel.TryGetValue(nokkel, out var o))
            {
                o = new Oppforing("a" + (_rekkefolge.Count + 1), tekstform);
                _perNokkel[nokkel] = o;
                _rekkefolge.Add(o);
                if (tekstform.Contains('/'))
                {
                    foreach (var del in tekstform.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        o.LeggTilVariant(del);
                    }
                }
            }
            else if (!string.Equals(o.Tekstform, tekstform, StringComparison.Ordinal))
            {
                o.LeggTilVariant(tekstform);
            }
            o.Antall++;
            if (o.Eider.Count < MaksEidEksempler && !o.Eider.Contains(eid)) o.Eider.Add(eid);
            return o.Id;
        }

        public IReadOnlyList<StrukturAktor> Bygg() => _rekkefolge
            .Select(o => new StrukturAktor(
                Id: o.Id,
                Tekstform: o.Tekstform,
                Varianter: o.Varianter,
                EidEksempler: o.Eider,
                AntallForekomster: o.Antall,
                Entitetstype: null,
                Navngitt: null,
                Referent: null,
                Opplosning: null,
                Distributiv: null,
                Kommentar: null))
            .ToList();

        private sealed class Oppforing(string id, string tekstform)
        {
            public string Id { get; } = id;
            public string Tekstform { get; } = tekstform;
            public List<string> Varianter { get; } = [];
            public List<string> Eider { get; } = [];
            public int Antall { get; set; }

            public void LeggTilVariant(string variant)
            {
                if (!string.Equals(variant, Tekstform, StringComparison.Ordinal) && !Varianter.Contains(variant))
                {
                    Varianter.Add(variant);
                }
            }
        }
    }
}
