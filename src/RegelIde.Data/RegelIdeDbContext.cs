using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RegelIde.Data;

/// <summary>
/// Lagrer <see cref="DateTimeOffset"/> som UTC-ticks. Brukes KUN på SQLite-profilen.
/// <para>
/// SQLite har ingen dato-type, og EF Core nekter å oversette både ORDER BY og sammenligning på
/// <c>DateTimeOffset</c> — <c>NotSupportedException</c>, ikke stille degradering. Det ville tatt
/// ned proveniens-/historikk-endepunktene, som sorterer på <c>dato</c>.
/// </para>
/// <para>
/// Merk at EF Core sin innebygde <c>DateTimeOffsetToBinaryConverter</c> IKKE kan brukes her: den
/// pakker offset inn i verdien, slik at sorteringen følger lokal veggklokke i stedet for faktisk
/// tidspunkt. Den kaster ikke — den gir stille feil rekkefølge, som er verre. <c>UtcTicks</c> er
/// monotont i faktisk tid uansett offset.
/// </para>
/// <para>
/// Verdien leses tilbake som UTC. Det er ufarlig her fordi all kode setter tidsstempler med
/// <c>DateTimeOffset.UtcNow</c> — det finnes ingen lokal offset å miste.
/// </para>
/// </summary>
internal sealed class UtcTicksKonverter() : ValueConverter<DateTimeOffset, long>(
    verdi => verdi.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

internal static class ModellByggerUtvidelser
{
    /// <summary>
    /// <c>now()</c> som databasestandard på Postgres. På SQLite droppes standardverdien: funksjonen
    /// finnes ikke der, og <c>CURRENT_TIMESTAMP</c> gir en streng uten offset som ikke leses trygt
    /// tilbake til <see cref="DateTimeOffset"/>. Applikasjonen setter uansett verdien selv overalt.
    /// </summary>
    public static PropertyBuilder<DateTimeOffset> StandardNaa(this PropertyBuilder<DateTimeOffset> p, bool sqlite) =>
        sqlite ? p : p.HasDefaultValueSql("now()");
}

public sealed class RegelIdeDbContext(DbContextOptions<RegelIdeDbContext> options) : DbContext(options)
{
    public DbSet<Virksomhet> Virksomheter => Set<Virksomhet>();
    public DbSet<Bruker> Brukere => Set<Bruker>();
    public DbSet<RettskildeEntitet> Rettskilder => Set<RettskildeEntitet>();
    public DbSet<RettskildeNodeEntitet> RettskildeNoder => Set<RettskildeNodeEntitet>();
    public DbSet<RettskildeReferanseEntitet> RettskildeReferanser => Set<RettskildeReferanseEntitet>();
    public DbSet<TekstTaggEntitet> TekstTagger => Set<TekstTaggEntitet>();
    public DbSet<TaggKindKonfigurasjonEntitet> TaggKindKonfigurasjoner => Set<TaggKindKonfigurasjonEntitet>();
    public DbSet<HandbokKommentarMetadataEntitet> HandbokKommentarMetadata => Set<HandbokKommentarMetadataEntitet>();
    public DbSet<ProveniensEntitet> Proveniens => Set<ProveniensEntitet>();
    public DbSet<TjenesteEntitet> Tjenester => Set<TjenesteEntitet>();
    public DbSet<TjenesteRegelverksreferanseEntitet> TjenesteRegelverksreferanser => Set<TjenesteRegelverksreferanseEntitet>();
    public DbSet<BegrepEntitet> Begreper => Set<BegrepEntitet>();
    public DbSet<KodelisteEntitet> Kodelister => Set<KodelisteEntitet>();
    public DbSet<KodelisteKodeEntitet> KodelisteKoder => Set<KodelisteKodeEntitet>();
    public DbSet<DatasettEntitet> Datasett => Set<DatasettEntitet>();
    public DbSet<VilkarEntitet> Vilkar => Set<VilkarEntitet>();
    public DbSet<VilkarInputDatasettEntitet> VilkarInputDatasett => Set<VilkarInputDatasettEntitet>();
    public DbSet<RegelnodeEntitet> Regelnoder => Set<RegelnodeEntitet>();
    public DbSet<RegelnodeBarnEntitet> RegelnodeBarn => Set<RegelnodeBarnEntitet>();
    public DbSet<UnntakEntitet> Unntak => Set<UnntakEntitet>();

    /// <summary>
    /// UTC-ticks-konverteringen gjelder alle <see cref="DateTimeOffset"/>-felter, og settes ett sted
    /// framfor på 18 properties. Kun på SQLite — Postgres har <c>timestamptz</c> og trenger den ikke.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        if (Database.IsSqlite())
        {
            b.Properties<DateTimeOffset>().HaveConversion<UtcTicksKonverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // EF cacher modellen per provider, så det er trygt å forgrene på motor her.
        var sqlite = Database.IsSqlite();
        var jsonKolonne = sqlite ? "TEXT" : "jsonb";

        b.Entity<Virksomhet>(e =>
        {
            e.ToTable("virksomheter");
            e.HasKey(x => x.Id).HasName("virksomheter_pkey");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.Organisasjonsnummer).HasColumnName("organisasjonsnummer");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.HasIndex(x => x.Organisasjonsnummer).IsUnique().HasDatabaseName("ux_virksomheter_organisasjonsnummer")
                .HasFilter("organisasjonsnummer IS NOT NULL");
        });

        b.Entity<Bruker>(e =>
        {
            e.ToTable("brukere");
            e.HasKey(x => x.Id).HasName("brukere_pkey");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Rolle).HasColumnName("rolle");
            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_brukere_virksomhet");
        });

        b.Entity<RettskildeEntitet>(e =>
        {
            e.ToTable("rettskilder", t => t.HasCheckConstraint(
                "ck_rettskilder_importrolle", "importrolle IN ('primaer', 'referanse')"));
            e.ToTable("rettskilder", t => t.HasCheckConstraint(
                "ck_rettskilder_akn_xml", "importrolle = 'referanse' OR akn_xml IS NOT NULL"));
            e.HasKey(x => x.Id).HasName("rettskilder_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Doctype).HasColumnName("doctype");
            e.Property(x => x.Kildetype).HasColumnName("kildetype");
            e.Property(x => x.Importrolle).HasColumnName("importrolle").HasDefaultValue("primaer");
            e.Property(x => x.Tittel).HasColumnName("tittel");
            e.Property(x => x.Kortnavn).HasColumnName("kortnavn");
            e.Property(x => x.Eli).HasColumnName("eli");
            e.Property(x => x.AknXml).HasColumnName("akn_xml");
            e.Property(x => x.Ikrafttredelse).HasColumnName("ikrafttredelse");
            e.Property(x => x.KonsolidertDato).HasColumnName("konsolidert_dato");
            e.Property(x => x.Utgiver).HasColumnName("utgiver");
            e.Property(x => x.Status).HasColumnName("status");
            // Optimistic concurrency (05-arkitektur-og-nfk.md §2: "skal varsle og avvise en lagring
            // som ville overskrevet en endring gjort av en annen bruker") — konfigureres eksplisitt
            // som concurrency token, ikke bare et vanlig heltall. EF Core inkluderer da den GAMLE
            // versjon-verdien i WHERE-klausulen på UPDATE og kaster DbUpdateConcurrencyException
            // hvis en annen skriving allerede har økt den. Appen selv må øke Versjon ved hver
            // faktiske endring (kun rettskilder er versjonert, §2.1: dokumentnivå, ikke nodenivå).
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1).IsConcurrencyToken();
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterId).HasColumnName("erstatter_id");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<RettskildeEntitet>().WithMany().HasForeignKey(x => x.ErstatterId);
            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);

            // To partial unique-indekser i stedet for den opprinnelige ene (docs/00-endringslogg-v0.3.md):
            // 1) Delte/nasjonale kilder (virksomhet_id IS NULL) — kun én 'gjeldende' rad per ELI GLOBALT,
            //    slik at f.eks. alkoholloven aldri kan finnes som to forskjellige "gjeldende" rader.
            // 2) Virksomhetens egne lokale kilder (virksomhet_id IS NOT NULL) — kun én 'gjeldende' rad
            //    per ELI PER VIRKSOMHET. Uten dette skillet ville en global (eli)-constraint feilaktig
            //    hindret to ulike virksomheter fra hver å ha sin egen lokale forskrift med samme ELI-form.
            e.HasIndex(x => x.Eli).IsUnique()
                .HasDatabaseName("ux_rettskilder_eli_gjeldende_delt")
                .HasFilter("entitetsstatus = 'gjeldende' AND virksomhet_id IS NULL");
            e.HasIndex(x => new { x.VirksomhetId, x.Eli }).IsUnique()
                .HasDatabaseName("ux_rettskilder_eli_gjeldende_per_virksomhet")
                .HasFilter("entitetsstatus = 'gjeldende' AND virksomhet_id IS NOT NULL");
        });

        b.Entity<RettskildeNodeEntitet>(e =>
        {
            e.ToTable("rettskilde_noder");
            e.HasKey(x => x.Id).HasName("rettskilde_noder_pkey");
            e.Property(x => x.RettskildeId).HasColumnName("rettskilde_id");
            e.Property(x => x.Eid).HasColumnName("eid");
            e.Property(x => x.Kildesystem).HasColumnName("kildesystem").HasDefaultValue("lovdata");
            e.Property(x => x.KildeId).HasColumnName("kilde_id");
            e.Property(x => x.OffisiellEli).HasColumnName("offisiell_eli");
            e.Property(x => x.ParentNodeId).HasColumnName("parent_node_id");
            e.Property(x => x.NodeType).HasColumnName("node_type");
            e.Property(x => x.Nummer).HasColumnName("nummer");
            e.Property(x => x.Overskrift).HasColumnName("overskrift");
            e.Property(x => x.Tekst).HasColumnName("tekst");
            e.Property(x => x.TekstHash).HasColumnName("tekst_hash");
            e.Property(x => x.Opphevet).HasColumnName("opphevet").HasDefaultValue(false);
            e.Property(x => x.OpphevetDato).HasColumnName("opphevet_dato");
            e.Property(x => x.Sorteringsrekkefolge).HasColumnName("sorteringsrekkefolge");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterNodeId).HasColumnName("erstatter_node_id");

            e.HasOne<RettskildeEntitet>().WithMany(r => r.Noder)
                .HasForeignKey(x => x.RettskildeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RettskildeNodeEntitet>().WithMany()
                .HasForeignKey(x => x.ParentNodeId);
            e.HasOne<RettskildeNodeEntitet>().WithMany()
                .HasForeignKey(x => x.ErstatterNodeId);

            // Filtrert (ikke en vanlig UNIQUE) fra 2026-07-26 (docs/08-byggesteg1-teknisk-design.md §2,
            // ux_rettskilde_noder_eid_gjeldende): en redigert håndbok-seksjon oppretter en NY rad med
            // samme eid (§2.1) — den gamle raden får entitetsstatus='erstattet' i stedet for å bli
            // overskrevet, og må derfor kunne sameksistere med den nye uten å kollidere på
            // (rettskilde_id, eid). Virkningsløst for Lov/Forskrift-rader (alltid 'gjeldende').
            e.HasIndex(x => new { x.RettskildeId, x.Eid }).IsUnique()
                .HasDatabaseName("ux_rettskilde_noder_eid_gjeldende")
                .HasFilter("entitetsstatus = 'gjeldende'");
            e.HasIndex(x => x.ParentNodeId).HasDatabaseName("ix_rettskilde_noder_parent");
            e.HasIndex(x => new { x.Eid, x.TekstHash }).HasDatabaseName("ix_rettskilde_noder_eid_hash");
        });

        b.Entity<HandbokKommentarMetadataEntitet>(e =>
        {
            e.ToTable("handbok_kommentar_metadata");
            e.HasKey(x => x.NodeId).HasName("handbok_kommentar_metadata_pkey");
            e.Property(x => x.NodeId).HasColumnName("node_id").ValueGeneratedNever();
            e.Property(x => x.Dokumenttype).HasColumnName("dokumenttype");
            e.Property(x => x.Bindende).HasColumnName("bindende");
            e.Property(x => x.FesteNiva).HasColumnName("feste_niva");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Revisjonsgrunn).HasColumnName("revisjonsgrunn");
            e.Property(x => x.Publisert).HasColumnName("publisert");
            e.Property(x => x.SistFagligEndret).HasColumnName("sist_faglig_endret");
            e.Property(x => x.UnderoverskrifterJson).HasColumnName("underoverskrifter").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.Marginord).HasColumnName("marginord");
            e.Property(x => x.PraksisJson).HasColumnName("praksis").HasColumnType(jsonKolonne).HasDefaultValue("[]");

            // 1:1 med rettskilde_noder — samme Id, ingen egen surrogatnøkkel.
            e.HasOne<RettskildeNodeEntitet>().WithOne(n => n.HandbokMetadata)
                .HasForeignKey<HandbokKommentarMetadataEntitet>(x => x.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RettskildeReferanseEntitet>(e =>
        {
            e.ToTable("rettskilde_referanser");
            e.HasKey(x => x.Id).HasName("rettskilde_referanser_pkey");
            e.Property(x => x.FraNodeId).HasColumnName("fra_node_id");
            e.Property(x => x.TilRettskildeId).HasColumnName("til_rettskilde_id");
            e.Property(x => x.TilEid).HasColumnName("til_eid");
            e.Property(x => x.Opprinnelse).HasColumnName("opprinnelse").HasDefaultValue("import");
            e.Property(x => x.TekstStart).HasColumnName("tekst_start");
            e.Property(x => x.TekstLengde).HasColumnName("tekst_lengde");

            e.HasOne<RettskildeNodeEntitet>().WithMany()
                .HasForeignKey(x => x.FraNodeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RettskildeEntitet>().WithMany()
                .HasForeignKey(x => x.TilRettskildeId);

            e.HasIndex(x => new { x.FraNodeId, x.TilRettskildeId, x.TilEid }).IsUnique()
                .HasDatabaseName("rettskilde_referanser_fra_node_id_til_rettskilde_id_til_ei_key");
        });

        b.Entity<TekstTaggEntitet>(e =>
        {
            e.ToTable("tekst_tagger");
            e.HasKey(x => x.Id).HasName("tekst_tagger_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.RettskildeId).HasColumnName("rettskilde_id");
            e.Property(x => x.NodeEid).HasColumnName("node_eid");
            e.Property(x => x.StartOffset).HasColumnName("start_offset");
            e.Property(x => x.EndOffset).HasColumnName("end_offset");
            e.Property(x => x.QuotePrefix).HasColumnName("quote_prefix");
            e.Property(x => x.QuoteExact).HasColumnName("quote_exact");
            e.Property(x => x.QuoteSuffix).HasColumnName("quote_suffix");
            e.Property(x => x.NodeTekstHash).HasColumnName("node_tekst_hash");
            e.Property(x => x.Kind).HasColumnName("kind");
            e.Property(x => x.RefId).HasColumnName("ref_id");
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.KreverGjennomgang).HasColumnName("krever_gjennomgang").HasDefaultValue(false);

            e.HasOne<RettskildeEntitet>().WithMany()
                .HasForeignKey(x => x.RettskildeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);

            // VirksomhetId med i nøkkelen: to virksomheter kan tagge samme delte rettskilde-node
            // med samme offset (f.eks. begge merker samme paragraf som "vilkår", men til SINE EGNE,
            // ulike vilkårsnoder) — uten dette ville constrainten feilaktig kollidert dem, særlig
            // før ref_id er satt (nullable inntil byggesteg 2/4, altså ofte NULL for begge samtidig).
            e.HasIndex(x => new { x.VirksomhetId, x.RettskildeId, x.NodeEid, x.StartOffset, x.EndOffset, x.Kind, x.RefId })
                .IsUnique().HasDatabaseName("tekst_tagger_unik_tagg");
            e.HasIndex(x => new { x.RettskildeId, x.NodeEid }).HasDatabaseName("ix_tekst_tagger_node");
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_tekst_tagger_virksomhet");
        });

        b.Entity<TaggKindKonfigurasjonEntitet>(e =>
        {
            e.ToTable("taggkind_konfigurasjon");
            e.HasKey(x => x.Id).HasName("taggkind_konfigurasjon_pkey");
            e.Property(x => x.Kode).HasColumnName("kode");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.Farge).HasColumnName("farge");
            e.Property(x => x.Sorteringsrekkefolge).HasColumnName("sorteringsrekkefolge");
            e.Property(x => x.Aktiv).HasColumnName("aktiv").HasDefaultValue(true);

            e.HasIndex(x => x.Kode).IsUnique().HasDatabaseName("ux_taggkind_konfigurasjon_kode");
        });

        b.Entity<ProveniensEntitet>(e =>
        {
            e.ToTable("proveniens");
            e.HasKey(x => x.Id).HasName("proveniens_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.EntitetType).HasColumnName("entitet_type");
            e.Property(x => x.EntitetId).HasColumnName("entitet_id");
            e.Property(x => x.EndretAv).HasColumnName("endret_av");
            e.Property(x => x.Dato).HasColumnName("dato").StandardNaa(sqlite);
            e.Property(x => x.Handling).HasColumnName("handling");
            e.Property(x => x.KildeReferanserJson).HasColumnName("kilde_referanser").HasColumnType(jsonKolonne);
            e.Property(x => x.AiForslagVersjon).HasColumnName("ai_forslag_versjon");
            e.Property(x => x.GodkjentAv).HasColumnName("godkjent_av");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);

            e.HasIndex(x => new { x.EntitetType, x.EntitetId }).HasDatabaseName("ix_proveniens_entitet");
        });

        b.Entity<TjenesteEntitet>(e =>
        {
            e.ToTable("tjenester");
            e.HasKey(x => x.Id).HasName("tjenester_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Tittel).HasColumnName("tittel");
            e.Property(x => x.Beskrivelse).HasColumnName("beskrivelse");
            e.Property(x => x.KompetentMyndighet).HasColumnName("kompetent_myndighet");
            e.Property(x => x.Output).HasColumnName("output");
            e.Property(x => x.Tjenestetype).HasColumnName("tjenestetype");
            e.Property(x => x.Malgruppe).HasColumnName("malgruppe");
            e.Property(x => x.Kanaler).HasColumnName("kanaler");
            e.Property(x => x.Kostnad).HasColumnName("kostnad");
            e.Property(x => x.Behandlingstid).HasColumnName("behandlingstid");
            e.Property(x => x.Kontaktpunkt).HasColumnName("kontaktpunkt");
            e.Property(x => x.KonsekvensVedBrudd).HasColumnName("konsekvens_ved_brudd");
            e.Property(x => x.Sprak).HasColumnName("sprak");
            e.Property(x => x.HendelserJson).HasColumnName("hendelser").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.TjenesteavhengigheterJson).HasColumnName("tjenesteavhengigheter").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("utkast");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterId).HasColumnName("erstatter_id");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");
            e.Property(x => x.RotnodeId).HasColumnName("rotnode_id");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<TjenesteEntitet>().WithMany().HasForeignKey(x => x.ErstatterId);
            e.HasOne<RegelnodeEntitet>().WithMany().HasForeignKey(x => x.RotnodeId);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_tjenester_virksomhet");
        });

        b.Entity<TjenesteRegelverksreferanseEntitet>(e =>
        {
            e.ToTable("tjeneste_regelverksreferanser");
            e.HasKey(x => x.Id).HasName("tjeneste_regelverksreferanser_pkey");
            e.Property(x => x.TjenesteId).HasColumnName("tjeneste_id");
            e.Property(x => x.TilRettskildeId).HasColumnName("til_rettskilde_id");
            e.Property(x => x.TilEid).HasColumnName("til_eid");

            e.HasOne<TjenesteEntitet>().WithMany().HasForeignKey(x => x.TjenesteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RettskildeEntitet>().WithMany().HasForeignKey(x => x.TilRettskildeId);

            e.HasIndex(x => new { x.TjenesteId, x.TilRettskildeId, x.TilEid }).IsUnique()
                .HasDatabaseName("ux_tjeneste_regelverksreferanser");
        });

        b.Entity<BegrepEntitet>(e =>
        {
            e.ToTable("begreper");
            e.HasKey(x => x.Id).HasName("begreper_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Term).HasColumnName("term");
            e.Property(x => x.Definisjon).HasColumnName("definisjon");
            e.Property(x => x.LovreferanseEid).HasColumnName("lovreferanse_eid");
            e.Property(x => x.GjelderFor).HasColumnName("gjelder_for");
            e.Property(x => x.KodelisteReferanseId).HasColumnName("kodeliste_referanse_id");
            e.Property(x => x.SkosUrl).HasColumnName("skos_url");
            e.Property(x => x.Begrepstype).HasColumnName("begrepstype");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("utkast");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterId).HasColumnName("erstatter_id");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<BegrepEntitet>().WithMany().HasForeignKey(x => x.ErstatterId);
            e.HasOne<KodelisteEntitet>().WithMany().HasForeignKey(x => x.KodelisteReferanseId);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_begreper_virksomhet");
        });

        b.Entity<KodelisteEntitet>(e =>
        {
            e.ToTable("kodelister", t => t.HasCheckConstraint(
                "ck_kodelister_type", "type IN ('juridisk', 'teknisk', 'ekstern-referanse')"));
            e.HasKey(x => x.Id).HasName("kodelister_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Kode).HasColumnName("kode");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.JuridiskGrunnlagEid).HasColumnName("juridisk_grunnlag_eid");
            e.Property(x => x.EksternKildeUri).HasColumnName("ekstern_kilde_uri");
            e.Property(x => x.EksternKildeVersjon).HasColumnName("ekstern_kilde_versjon");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("utkast");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterId).HasColumnName("erstatter_id");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<KodelisteEntitet>().WithMany().HasForeignKey(x => x.ErstatterId);
            e.HasIndex(x => x.Kode).IsUnique().HasDatabaseName("ux_kodelister_kode")
                .HasFilter("entitetsstatus = 'gjeldende'");
        });

        b.Entity<KodelisteKodeEntitet>(e =>
        {
            e.ToTable("kodeliste_koder");
            e.HasKey(x => x.Id).HasName("kodeliste_koder_pkey");
            e.Property(x => x.KodelisteId).HasColumnName("kodeliste_id");
            e.Property(x => x.Kode).HasColumnName("kode");
            e.Property(x => x.Term).HasColumnName("term");
            e.Property(x => x.Definisjon).HasColumnName("definisjon");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.ErstattesAvKodeId).HasColumnName("erstattes_av_kode_id");

            e.HasOne<KodelisteEntitet>().WithMany(k => k.Koder).HasForeignKey(x => x.KodelisteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<KodelisteKodeEntitet>().WithMany().HasForeignKey(x => x.ErstattesAvKodeId);
            e.HasIndex(x => new { x.KodelisteId, x.Kode }).IsUnique().HasDatabaseName("ux_kodeliste_koder_kode");
        });

        b.Entity<DatasettEntitet>(e =>
        {
            e.ToTable("datasett");
            e.HasKey(x => x.Id).HasName("datasett_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Felt).HasColumnName("felt");
            e.Property(x => x.Prop).HasColumnName("prop");
            e.Property(x => x.Dtype).HasColumnName("dtype");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Kilde).HasColumnName("kilde");
            e.Property(x => x.KodelisteId).HasColumnName("kodeliste_id");
            e.Property(x => x.Grunnlag).HasColumnName("grunnlag");
            e.Property(x => x.Lagring).HasColumnName("lagring");
            e.Property(x => x.Mottakere).HasColumnName("mottakere");
            e.Property(x => x.Bruk).HasColumnName("bruk");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<KodelisteEntitet>().WithMany().HasForeignKey(x => x.KodelisteId);
            e.HasIndex(x => x.Prop).HasDatabaseName("ix_datasett_prop");
        });

        b.Entity<VilkarEntitet>(e =>
        {
            e.ToTable("vilkar");
            e.HasKey(x => x.Id).HasName("vilkar_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Tittel).HasColumnName("tittel");
            e.Property(x => x.Beskrivelse).HasColumnName("beskrivelse");
            e.Property(x => x.GeneriskMal).HasColumnName("generisk_mal");
            e.Property(x => x.Vilkarstype).HasColumnName("vilkarstype");
            e.Property(x => x.GjelderRolle).HasColumnName("gjelder_rolle");
            e.Property(x => x.JuridiskGrunnlagJson).HasColumnName("juridisk_grunnlag").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.BegrepId).HasColumnName("begrep_id");
            e.Property(x => x.Vurderingstype).HasColumnName("vurderingstype");
            e.Property(x => x.ParametreJson).HasColumnName("parametre").HasColumnType(jsonKolonne).HasDefaultValue("{}");
            e.Property(x => x.SkjonnsgrunnlagBegrepId).HasColumnName("skjonnsgrunnlag_begrep_id");
            e.Property(x => x.SkjonnsmomenterJson).HasColumnName("skjonnsmomenter").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.KreverDokumentasjon).HasColumnName("krever_dokumentasjon").HasDefaultValue(false);
            e.Property(x => x.Eskaleringsrolle).HasColumnName("eskaleringsrolle");
            e.Property(x => x.VeiledningTilBruker).HasColumnName("veiledning_til_bruker");
            e.Property(x => x.VeiledningTilSaksbehandler).HasColumnName("veiledning_til_saksbehandler");
            e.Property(x => x.ErFormel).HasColumnName("er_formel").HasDefaultValue(false);
            e.Property(x => x.FormelBeskrivelse).HasColumnName("formel_beskrivelse");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("utkast");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterId).HasColumnName("erstatter_id");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<VilkarEntitet>().WithMany().HasForeignKey(x => x.ErstatterId);
            e.HasOne<BegrepEntitet>().WithMany().HasForeignKey(x => x.BegrepId);
            e.HasOne<BegrepEntitet>().WithMany().HasForeignKey(x => x.SkjonnsgrunnlagBegrepId);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_vilkar_virksomhet");
        });

        b.Entity<VilkarInputDatasettEntitet>(e =>
        {
            e.ToTable("vilkar_input_datasett");
            e.HasKey(x => x.Id).HasName("vilkar_input_datasett_pkey");
            e.Property(x => x.VilkarId).HasColumnName("vilkar_id");
            e.Property(x => x.DatasettId).HasColumnName("datasett_id");

            e.HasOne<VilkarEntitet>().WithMany().HasForeignKey(x => x.VilkarId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<DatasettEntitet>().WithMany().HasForeignKey(x => x.DatasettId);
            e.HasIndex(x => new { x.VilkarId, x.DatasettId }).IsUnique().HasDatabaseName("ux_vilkar_input_datasett");
        });

        b.Entity<RegelnodeEntitet>(e =>
        {
            e.ToTable("regelnoder", t => t.HasCheckConstraint(
                "ck_regelnoder_barn_operator", "barn_operator IN ('OG', 'ELLER', 'IKKE')"));
            e.HasKey(x => x.Id).HasName("regelnoder_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Tittel).HasColumnName("tittel");
            e.Property(x => x.Beskrivelse).HasColumnName("beskrivelse");
            e.Property(x => x.GeneriskMal).HasColumnName("generisk_mal");
            e.Property(x => x.BarnOperator).HasColumnName("barn_operator");
            e.Property(x => x.UtdataNavn).HasColumnName("utdata_navn");
            e.Property(x => x.UtdataType).HasColumnName("utdata_type");
            e.Property(x => x.ErRotnode).HasColumnName("er_rotnode").HasDefaultValue(false);
            e.Property(x => x.JuridiskGrunnlagJson).HasColumnName("juridisk_grunnlag").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.InnvilgelseTekst).HasColumnName("innvilgelse_tekst");
            e.Property(x => x.AvslagTekst).HasColumnName("avslag_tekst");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("utkast");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterId).HasColumnName("erstatter_id");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<RegelnodeEntitet>().WithMany().HasForeignKey(x => x.ErstatterId);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_regelnoder_virksomhet");
        });

        b.Entity<RegelnodeBarnEntitet>(e =>
        {
            e.ToTable("regelnode_barn", t => t.HasCheckConstraint(
                "ck_regelnode_barn_type", "barn_type IN ('vilkar', 'regelnode')"));
            e.HasKey(x => x.Id).HasName("regelnode_barn_pkey");
            e.Property(x => x.RegelnodeId).HasColumnName("regelnode_id");
            e.Property(x => x.BarnType).HasColumnName("barn_type");
            e.Property(x => x.BarnId).HasColumnName("barn_id");

            e.HasOne<RegelnodeEntitet>().WithMany().HasForeignKey(x => x.RegelnodeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RegelnodeId, x.BarnType, x.BarnId }).IsUnique().HasDatabaseName("ux_regelnode_barn");
        });

        b.Entity<UnntakEntitet>(e =>
        {
            e.ToTable("unntak", t => t.HasCheckConstraint(
                "ck_unntak_betingelse_type", "betingelse_type IN ('vilkar', 'regelnode')"));
            e.HasKey(x => x.Id).HasName("unntak_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Tittel).HasColumnName("tittel");
            e.Property(x => x.Beskrivelse).HasColumnName("beskrivelse");
            e.Property(x => x.GjelderRegelId).HasColumnName("gjelder_regel_id");
            e.Property(x => x.BetingelseType).HasColumnName("betingelse_type");
            e.Property(x => x.BetingelseId).HasColumnName("betingelse_id");
            e.Property(x => x.JuridiskGrunnlagJson).HasColumnName("juridisk_grunnlag").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("utkast");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.ErstatterId).HasColumnName("erstatter_id");
            e.Property(x => x.GyldigFra).HasColumnName("gyldig_fra");
            e.Property(x => x.GyldigTil).HasColumnName("gyldig_til");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<UnntakEntitet>().WithMany().HasForeignKey(x => x.ErstatterId);
            e.HasOne<RegelnodeEntitet>().WithMany().HasForeignKey(x => x.GjelderRegelId);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_unntak_virksomhet");
            e.HasIndex(x => x.GjelderRegelId).HasDatabaseName("ix_unntak_gjelder_regel");
        });
    }
}
