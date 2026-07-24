namespace RegelIde.Data;

/// <summary>
/// EF Core-entiteter som speiler docs/08-byggesteg1-teknisk-design.md §2 — feltnavn, typer og
/// constraints er låst der etter tre QA-runder. Avvik markert eksplisitt: multi-virksomhet-
/// refaktoreringen (docs/00-endringslogg-v0.3.md, 2026-07-24) la til <see cref="Virksomhet"/> og
/// virksomhet_id-feltene under, som ikke var del av det opprinnelige låste skjemaet.
/// </summary>
public sealed class Virksomhet
{
    public Guid Id { get; set; }
    public required string Navn { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

/// <summary>
/// Lagt til 2026-07-24 (samme runde som GUI-arbeidet) — en enkel testbruker-modell, IKKE ekte
/// autentisering. Erstattes av Ansattporten-innlogging senere uten at denne tabellen forsvinner:
/// en ekte innlogget bruker vil fortsatt trenge en rad her (navn, virksomhet, rolle), bare med
/// identiteten hentet fra et Ansattporten-claim i stedet for en GUI-nedtrekksliste.
/// </summary>
public sealed class Bruker
{
    public Guid Id { get; set; }
    public required string Navn { get; set; }
    public Guid VirksomhetId { get; set; }

    /// <summary>Se RBAC-matrisen i docs/03-domenemodell.md §2: 'Fagansvarlig' | 'Jurist' | 'Systemforvalter' | 'Saksbehandler'.</summary>
    public required string Rolle { get; set; }
}

public sealed class RettskildeEntitet
{
    public Guid Id { get; set; }

    /// <summary>
    /// NULL = delt/nasjonal rettskilde (Lov/Forskrift fra Lovdata — importeres og vises likt for
    /// alle virksomheter, aldri duplisert per virksomhet). Satt = virksomhetens egen lokale kilde
    /// (lokal forskrift, virksomhetsdokument) — kun synlig for og eid av denne virksomheten.
    /// Se docs/00-endringslogg-v0.3.md for begrunnelsen (opptil 1000 offentlige virksomheter —
    /// duplisering av delte nasjonale kilder per virksomhet ville vært både kostbart og feilutsatt
    /// ved lovendringer, som da måtte vedlikeholdes N ganger i stedet for én).
    /// </summary>
    public Guid? VirksomhetId { get; set; }

    public required string Doctype { get; set; } // 'act' | 'doc' | 'judgment' | 'internal'
    public required string Kildetype { get; set; } // 'Lov' | 'Forskrift' | 'Rundskriv' | 'Presedens' | 'Virksomhetsdokument'
    public string Importrolle { get; set; } = "primaer"; // 'primaer' | 'referanse'
    public required string Tittel { get; set; }
    public string? Kortnavn { get; set; }
    public string? Eli { get; set; }
    public string? AknXml { get; set; } // NULL for referanse-stubber
    public DateOnly? Ikrafttredelse { get; set; }
    public DateOnly? KonsolidertDato { get; set; }
    public string? Utgiver { get; set; }
    public required string Status { get; set; } // 'Gjeldende' | 'Opphevet' | 'Utkast'
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }

    public List<RettskildeNodeEntitet> Noder { get; set; } = [];
}

public sealed class RettskildeNodeEntitet
{
    public Guid Id { get; set; }
    public Guid RettskildeId { get; set; }
    public required string Eid { get; set; } // canonical_id — endres aldri
    public string Kildesystem { get; set; } = "lovdata";
    public required string KildeId { get; set; } // source_id
    public string? OffisiellEli { get; set; } // nullable — §1.2, fylles ut hvis Lovdata publiserer seksjons-ELI
    public Guid? ParentNodeId { get; set; }
    public required string NodeType { get; set; } // 'kapittel' | 'underinndeling' | 'paragraf' | 'ledd' | 'punkt'
    public string? Nummer { get; set; }
    public string? Overskrift { get; set; }
    public string? Tekst { get; set; } // kun ledd/punkt (bladtekst)
    public string? TekstHash { get; set; }
    public int Sorteringsrekkefolge { get; set; }
}

public sealed class RettskildeReferanseEntitet
{
    public Guid Id { get; set; }
    public Guid FraNodeId { get; set; }
    public Guid TilRettskildeId { get; set; }
    public required string TilEid { get; set; }
}

public sealed class TekstTaggEntitet
{
    public Guid Id { get; set; }

    /// <summary>
    /// Ikke nullable, i motsetning til RettskildeEntitet.VirksomhetId — en tagg er alltid en
    /// virksomhets eget arbeidsprodukt, selv når den peker på en delt/nasjonal rettskilde. To
    /// virksomheter kan tagge samme lovparagraf ulikt (forskjellige vilkår/begreper), så taggen
    /// arver ikke synlighet fra RettskildeId.
    /// </summary>
    public required Guid VirksomhetId { get; set; }

    public Guid RettskildeId { get; set; }
    public required string NodeEid { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public required string QuotePrefix { get; set; }
    public required string QuoteExact { get; set; }
    public required string QuoteSuffix { get; set; }
    public required string NodeTekstHash { get; set; }
    public required string Kind { get; set; } // 'begrep' | 'tjeneste' | 'vilkar' | 'regel'
    public Guid? RefId { get; set; } // nullable inntil byggesteg 2/4
    public string Entitetsstatus { get; set; } = "gjeldende";
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

public sealed class ProveniensEntitet
{
    public Guid Id { get; set; }

    /// <summary>NULL når den underliggende hendelsen gjaldt en delt/nasjonal entitet (§ RettskildeEntitet.VirksomhetId).</summary>
    public Guid? VirksomhetId { get; set; }

    public required string EntitetType { get; set; } // 'rettskilde' | 'begrep' | 'vilkar' | 'regelnode' | 'unntak' | …
    public Guid EntitetId { get; set; }
    public required string EndretAv { get; set; }
    public DateTimeOffset Dato { get; set; }
    public required string Handling { get; set; } // 'opprettet' | 'endret' | 'foreslatt_av_ai' | 'validert' | 'publisert' | 'arkivert'
    public string? KildeReferanserJson { get; set; } // jsonb
    public string? AiForslagVersjon { get; set; }
    public string? GodkjentAv { get; set; }
}
