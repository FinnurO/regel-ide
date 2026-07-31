using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Seed-demonstrasjon av Hendelse/Tjenesteavhengighet (docs/03-domenemodell.md §1.5, docs/13-backlog.md
/// §2.1) — kobler de 13 tjenestene fra rundskriv-fasitens § 12 "Relevante tjenester"
/// (<see cref="FasitRunde4Seed"/>) faktisk sammen med "Alminnelig skjenkebevilling", i stedet for at
/// de forblir 13 frittstående, ukoblede Tjeneste-rader (jf. `RundskrivReproduksjonTests`s §12-gap).
/// Alt innhold under er hentet direkte fra rundskriv-fasiten og domenemodellens egne eksempler —
/// «Alminnelig skjenkebevilling» → hendelsen "Endring av eierskap" → «Endring av eiere eller
/// eierandeler» og "Kontroll/tilsyn" som klassifiserer begge tjenestene er ordrett domenemodellens
/// egne eksempler (§1.5), ikke oppspinn.
///
/// Kjøres etter <see cref="FasitRunde4Seed"/> (krever de 13 tjenestene den oppretter).
/// </summary>
public static class HendelseTjenesteavhengighetSeed
{
    private const string SeedBruker = "Kari Jurist";
    private const string MarkorHendelse = "Kontroll/tilsyn"; // global guard

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        if (await db.Hendelser.AnyAsync(h => h.Navn == MarkorHendelse, ct)) return;

        var testkommunen = await db.Virksomheter.FirstOrDefaultAsync(v => v.Navn == "Testkommunen", ct);
        if (testkommunen is null) return;
        var virksomhetId = testkommunen.Id;

        async Task<Guid?> FinnTjenesteIdAsync(string tittel) =>
            (await db.Tjenester.FirstOrDefaultAsync(t => t.Tittel == tittel && t.Entitetsstatus == "gjeldende", ct))?.Id;

        var alminnelig = await FinnTjenesteIdAsync("Alminnelig skjenkebevilling");
        if (alminnelig is null) return; // byggesteg 2/4-seedingen må ha kjørt først

        var hendelseregister = new HendelseregisterTjeneste(db);
        var avhengighetregister = new TjenesteavhengighetregisterTjeneste(db);

        // "Kontroll/tilsyn" klassifiserer BEGGE tjenestene symmetrisk (cpsv:isClassifiedBy) — domenemodellens
        // egen illustrasjon av hvorfor Hendelse IKKE er en rettet relasjon.
        var kontrollTilsyn = await hendelseregister.OpprettAsync(null, "Kontroll/tilsyn", "virksomhetshendelse",
            "Kommunens kontroll av at bevillingshaver overholder regelverket.", SeedBruker, ct);
        await hendelseregister.KobleTilTjenesteAsync(alminnelig.Value, kontrollTilsyn.Id, ct);
        if (await FinnTjenesteIdAsync("Kontroller av salgs- og skjenkesteder") is { } kontroller)
        {
            await hendelseregister.KobleTilTjenesteAsync(kontroller, kontrollTilsyn.Id, ct);
        }

        // §9 "meldeplikt ved endring av styrer, stedfortreder eller eiersammensetning" — tre distinkte,
        // navngitte virksomhetshendelser, hver med sin egen utlost_av-kant (ikke én diffus fellesnevner).
        await KobleUtlostAvAsync(db, hendelseregister, avhengighetregister, virksomhetId, alminnelig.Value,
            "Endring av eiere eller eierandeler", "Endring av eierskap",
            "En eier eller eierandel i bevillingshavers virksomhet endres.", ct);
        await KobleUtlostAvAsync(db, hendelseregister, avhengighetregister, virksomhetId, alminnelig.Value,
            "Eierskifte og drift i overgangsperioden på tidligere eiers bevilling", "Eierskifte",
            "Virksomheten skifter eier og driver videre på tidligere eiers bevilling i overgangsperioden.", ct);
        await KobleUtlostAvAsync(db, hendelseregister, avhengighetregister, virksomhetId, alminnelig.Value,
            "Oppsigelse av bevilling", "Avvikling av virksomhet",
            "Virksomheten opphører eller bevillingen sies opp.", ct);
        await KobleUtlostAvAsync(db, hendelseregister, avhengighetregister, virksomhetId, alminnelig.Value,
            "Konsekvenser ved brudd på regelverket", "Brudd på regelverket",
            "Bevillingshaver bryter vilkår i bevillingen eller alkohollovgivningen.", ct);

        // §5 "Skjenking i næringsøyemed krever BÅDE serverings- og skjenkebevilling" — ekte forutsetning.
        if (await FinnTjenesteIdAsync("Serveringsbevilling") is { } serveringsbevilling)
        {
            await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, serveringsbevilling, alminnelig.Value,
                "forutsetning_for", "Skjenking i næringsøyemed krever både serverings- og skjenkebevilling (rundskriv-fasit § 5).", ct);

            // §7 "Etablererprøven"/"Kunnskapsprøvene" er forutsetning for Serveringsbevilling (domenemodellens eget eksempel).
            if (await FinnTjenesteIdAsync("Etablererprøven") is { } etablererprove)
            {
                await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, etablererprove, serveringsbevilling,
                    "forutsetning_for", null, ct);
            }
            if (await FinnTjenesteIdAsync("Kunnskapsprøvene") is { } kunnskapsprovene)
            {
                await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, kunnskapsprovene, serveringsbevilling,
                    "forutsetning_for", null, ct);
            }
        }

        // Domenemodellens eget "gir_mulighet_til"-eksempel — Utvidelse for en enkelt anledning, samt
        // Skjenkebevilling for et arrangement (samme "enkelt anledning"-mønster, § 1-6 andre ledd).
        if (await FinnTjenesteIdAsync("Utvidelse av skjenkebevilling for en enkelt anledning") is { } utvidelse)
        {
            await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, alminnelig.Value, utvidelse,
                "gir_mulighet_til", null, ct);
        }
        if (await FinnTjenesteIdAsync("Skjenkebevilling for et arrangement") is { } arrangement)
        {
            await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, alminnelig.Value, arrangement,
                "gir_mulighet_til", null, ct);
        }

        // Salgsbevilling — samme lov (alkoholloven), annet kapittel (salg vs. skjenking) — relatert, men
        // ikke en strukturell forutsetning i noen retning. Generell "avhengig_av".
        if (await FinnTjenesteIdAsync("Salgsbevilling") is { } salgsbevilling)
        {
            await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, alminnelig.Value, salgsbevilling,
                "avhengig_av", "Samme lov, ulikt kapittel (salg vs. skjenking) — ingen streng forutsetning.", ct);
        }

        // §9 gebyret beregnes ut fra omsetningen — omsetningsoppgaven er input til gebyrberegningen.
        if (await FinnTjenesteIdAsync("Omsetningsoppgave og bevillingsgebyr") is { } omsetningsoppgave)
        {
            await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, omsetningsoppgave, alminnelig.Value,
                "input_til", "Bevillingsgebyret beregnes ut fra omsetningsoppgaven (rundskriv-fasit § 9).", ct);
        }

        // §9 "meldeplikt ved endring av styrer, stedfortreder eller eiersammensetning" dekker delvis det
        // samme som "Endringer i driften" — generell, løsere kobling (ikke utlost_av: dette er en løpende
        // meldeplikt, ikke én navngitt, avgrenset hendelse).
        if (await FinnTjenesteIdAsync("Endringer i driften som får betydning for bevillingen") is { } endringerIDriften)
        {
            await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, alminnelig.Value, endringerIDriften,
                "avhengig_av", "Meldeplikt ved endring av styrer, stedfortreder eller eiersammensetning (rundskriv-fasit § 9).", ct);
        }
    }

    private static async Task KobleUtlostAvAsync(
        RegelIdeDbContext db, HendelseregisterTjeneste hendelseregister, TjenesteavhengighetregisterTjeneste avhengighetregister,
        Guid virksomhetId, Guid alminneligId, string tilTjenesteTittel, string hendelseNavn, string hendelseBeskrivelse,
        CancellationToken ct)
    {
        var tilTjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Tittel == tilTjenesteTittel && t.Entitetsstatus == "gjeldende", ct);
        if (tilTjeneste is null) return;

        var hendelse = await hendelseregister.OpprettAsync(null, hendelseNavn, "virksomhetshendelse", hendelseBeskrivelse, SeedBruker, ct);
        await OpprettAvhengighetAsync(avhengighetregister, virksomhetId, alminneligId, tilTjeneste.Id, "utlost_av", null, ct, hendelse.Id);
    }

    private static async Task OpprettAvhengighetAsync(
        TjenesteavhengighetregisterTjeneste register, Guid virksomhetId, Guid fraTjenesteId, Guid tilTjenesteId,
        string rel, string? beskrivelse, CancellationToken ct, Guid? hendelseId = null)
    {
        try
        {
            await register.OpprettAsync(virksomhetId, fraTjenesteId, tilTjenesteId, rel, hendelseId, beskrivelse, SeedBruker, ct);
        }
        catch (ArgumentException)
        {
            // Allerede opprettet (idempotens ved gjentatt oppstart) — no-op.
        }
    }
}
