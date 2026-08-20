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
    public DbSet<RettskildeNodeEmbeddingEntitet> RettskildeNodeEmbeddinger => Set<RettskildeNodeEmbeddingEntitet>();
    public DbSet<ProveniensEntitet> Proveniens => Set<ProveniensEntitet>();
    public DbSet<TjenesteEntitet> Tjenester => Set<TjenesteEntitet>();
    public DbSet<HandlingEntitet> Handlinger => Set<HandlingEntitet>();
    public DbSet<TjenesteRegelverksreferanseEntitet> TjenesteRegelverksreferanser => Set<TjenesteRegelverksreferanseEntitet>();
    public DbSet<HendelseEntitet> Hendelser => Set<HendelseEntitet>();
    public DbSet<TjenesteHendelseEntitet> TjenesteHendelser => Set<TjenesteHendelseEntitet>();
    public DbSet<TjenesteavhengighetEntitet> Tjenesteavhengigheter => Set<TjenesteavhengighetEntitet>();
    public DbSet<EksternTjenestereferanseEntitet> EksterneTjenestereferanser => Set<EksternTjenestereferanseEntitet>();
    public DbSet<HandbokRettskildeomfangEntitet> HandbokRettskildeomfang => Set<HandbokRettskildeomfangEntitet>();
    public DbSet<KunnskapsbibliotekLenkeEntitet> KunnskapsbibliotekLenker => Set<KunnskapsbibliotekLenkeEntitet>();
    public DbSet<KunnskapsbibliotekFilEntitet> KunnskapsbibliotekFiler => Set<KunnskapsbibliotekFilEntitet>();
    public DbSet<LovdataKatalogOppforingEntitet> LovdataKatalogOppforinger => Set<LovdataKatalogOppforingEntitet>();
    public DbSet<EksternKildeEntitet> EksterneKilder => Set<EksternKildeEntitet>();
    public DbSet<NettsideStiEntitet> NettsideStier => Set<NettsideStiEntitet>();
    public DbSet<NettsideLenkeEntitet> NettsideLenker => Set<NettsideLenkeEntitet>();
    public DbSet<BegrepEntitet> Begreper => Set<BegrepEntitet>();
    public DbSet<KodelisteEntitet> Kodelister => Set<KodelisteEntitet>();
    public DbSet<KodelisteKodeEntitet> KodelisteKoder => Set<KodelisteKodeEntitet>();
    public DbSet<DatasettEntitet> Datasett => Set<DatasettEntitet>();
    public DbSet<DatasettVerdiEntitet> DatasettVerdier => Set<DatasettVerdiEntitet>();
    public DbSet<VilkarEntitet> Vilkar => Set<VilkarEntitet>();
    public DbSet<VilkarInputDatasettEntitet> VilkarInputDatasett => Set<VilkarInputDatasettEntitet>();
    public DbSet<RegelnodeEntitet> Regelnoder => Set<RegelnodeEntitet>();
    public DbSet<RegelnodeBarnEntitet> RegelnodeBarn => Set<RegelnodeBarnEntitet>();
    public DbSet<UnntakEntitet> Unntak => Set<UnntakEntitet>();
    public DbSet<VilkarstreKommentarEntitet> VilkarstreKommentarer => Set<VilkarstreKommentarEntitet>();

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
            e.Property(x => x.Kommunenummer).HasColumnName("kommunenummer");
            e.Property(x => x.Forvaltningsniva).HasColumnName("forvaltningsniva");
            e.Property(x => x.Aktiv).HasColumnName("aktiv").HasDefaultValue(true);
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
            e.Property(x => x.AltinnBrukerId).HasColumnName("altinn_bruker_id");
            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_brukere_virksomhet");
            // Partiell unik indeks: gjentatt innlogging skal treffe samme rad, men de seedede
            // testbrukerne har alle NULL her og må kunne eksistere side om side.
            e.HasIndex(x => x.AltinnBrukerId)
                .IsUnique()
                .HasFilter("altinn_bruker_id IS NOT NULL")
                .HasDatabaseName("ux_brukere_altinn_bruker_id");
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

            // Lag 1 (docs/15-handbok-dokumentgraf-notat.md §2/§8 Trinn 1) — hentet, bitidentisk original.
            e.Property(x => x.Url).HasColumnName("url");
            e.Property(x => x.Innhold).HasColumnName("innhold");
            e.Property(x => x.InnholdsHash).HasColumnName("innholds_hash");
            e.Property(x => x.Hentet).HasColumnName("hentet");
            e.Property(x => x.HttpEtag).HasColumnName("http_etag");
            e.Property(x => x.HttpLastModified).HasColumnName("http_last_modified");

            // RettsligStatus, to akser (§3.3, [LÅST] avklaringsrunde 1 2026-08-12).
            e.Property(x => x.NormativVirkning).HasColumnName("normativ_virkning");
            e.Property(x => x.FunksjonellRolle).HasColumnName("funksjonell_rolle");
            e.Property(x => x.InterntDokNr).HasColumnName("internt_dok_nr");
            e.Property(x => x.Revisjonsnr).HasColumnName("revisjonsnr");
            e.Property(x => x.VedtattAv).HasColumnName("vedtatt_av");
            e.Property(x => x.Vedtaksdato).HasColumnName("vedtaksdato");
            e.Property(x => x.Saksnummer).HasColumnName("saksnummer");
            e.Property(x => x.HjemmelEid).HasColumnName("hjemmel_eid");

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

        b.Entity<RettskildeNodeEmbeddingEntitet>(e =>
        {
            e.ToTable("rettskilde_node_embeddinger");
            e.HasKey(x => x.NodeId).HasName("rettskilde_node_embeddinger_pkey");
            e.Property(x => x.NodeId).HasColumnName("node_id").ValueGeneratedNever();
            e.Property(x => x.Embedding).HasColumnName("embedding");
            e.Property(x => x.Modell).HasColumnName("modell");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            // 1:1 med rettskilde_noder — ingen navigasjonsegenskap tilbake (i motsetning til
            // HandbokMetadata) siden dette kun konsumeres via RettskildeEmbeddingTjeneste/
            // RagKontekstHjelper, aldri via en Include fra RettskildeNodeEntitet selv.
            e.HasOne<RettskildeNodeEntitet>().WithMany()
                .HasForeignKey(x => x.NodeId)
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
            e.Property(x => x.Livshendelser).HasColumnName("livshendelser").HasDefaultValueSql("'{}'");
            e.Property(x => x.LosKlassifisering).HasColumnName("los_klassifisering");
            e.Property(x => x.Tjenesteomrade).HasColumnName("tjenesteomrade");
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

        b.Entity<HandlingEntitet>(e =>
        {
            e.ToTable("handlinger");
            e.HasKey(x => x.Id).HasName("handlinger_pkey");
            e.Property(x => x.TjenesteId).HasColumnName("tjeneste_id");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.Handlingstype).HasColumnName("handlingstype");
            e.Property(x => x.Bruksomraade).HasColumnName("bruksomraade");
            e.Property(x => x.UtfortAv).HasColumnName("utfort_av");
            e.Property(x => x.RotnodeId).HasColumnName("rotnode_id");
            e.Property(x => x.KanalerJson).HasColumnName("kanaler").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.BehandlingstidJson).HasColumnName("behandlingstid").HasColumnType(jsonKolonne).HasDefaultValue("{}");
            e.Property(x => x.KostnadJson).HasColumnName("kostnad").HasColumnType(jsonKolonne).HasDefaultValue("{}");
            e.Property(x => x.VedleggJson).HasColumnName("vedlegg").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.VeiledningstekstJson).HasColumnName("veiledningstekst").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.ArsakerJson).HasColumnName("arsaker").HasColumnType(jsonKolonne).HasDefaultValue("[]");
            e.Property(x => x.ResultatJson).HasColumnName("resultat").HasColumnType(jsonKolonne).HasDefaultValue("{}");
            e.Property(x => x.Merknad).HasColumnName("merknad");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("utkast");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<TjenesteEntitet>().WithMany().HasForeignKey(x => x.TjenesteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RegelnodeEntitet>().WithMany().HasForeignKey(x => x.RotnodeId);
            e.HasIndex(x => x.TjenesteId).HasDatabaseName("ix_handlinger_tjeneste");
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

        b.Entity<HendelseEntitet>(e =>
        {
            e.ToTable("hendelser");
            e.HasKey(x => x.Id).HasName("hendelser_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Beskrivelse).HasColumnName("beskrivelse");
            e.Property(x => x.Versjon).HasColumnName("versjon").HasDefaultValue(1);
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_hendelser_virksomhet");
        });

        b.Entity<TjenesteHendelseEntitet>(e =>
        {
            e.ToTable("tjeneste_hendelser");
            e.HasKey(x => x.Id).HasName("tjeneste_hendelser_pkey");
            e.Property(x => x.TjenesteId).HasColumnName("tjeneste_id");
            e.Property(x => x.HendelseId).HasColumnName("hendelse_id");

            e.HasOne<TjenesteEntitet>().WithMany().HasForeignKey(x => x.TjenesteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<HendelseEntitet>().WithMany().HasForeignKey(x => x.HendelseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TjenesteId, x.HendelseId }).IsUnique().HasDatabaseName("ux_tjeneste_hendelser");
        });

        b.Entity<TjenesteavhengighetEntitet>(e =>
        {
            // Nøyaktig én av til_tjeneste_id/til_ekstern_referanse_id (2026-08-19,
            // feature/tjenesteavhengighet-ekstern-referanse) — se TjenesteavhengighetEntitets klassekommentar.
            // Samme "aldri stol på DB-constraint-en alene for en lesbar feilmelding" som ellers i kodebasen:
            // OpprettAsync validerer det samme defensivt i C# FØR raden når så langt som SaveChangesAsync.
            e.ToTable("tjenesteavhengigheter", t => t.HasCheckConstraint(
                "ck_tjenesteavhengigheter_ett_mal",
                "(til_tjeneste_id IS NOT NULL AND til_ekstern_referanse_id IS NULL) OR " +
                "(til_tjeneste_id IS NULL AND til_ekstern_referanse_id IS NOT NULL)"));
            e.HasKey(x => x.Id).HasName("tjenesteavhengigheter_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.FraTjenesteId).HasColumnName("fra_tjeneste_id");
            e.Property(x => x.TilTjenesteId).HasColumnName("til_tjeneste_id");
            e.Property(x => x.TilEksternReferanseId).HasColumnName("til_ekstern_referanse_id");
            e.Property(x => x.Rel).HasColumnName("rel");
            e.Property(x => x.HendelseId).HasColumnName("hendelse_id");
            e.Property(x => x.Beskrivelse).HasColumnName("beskrivelse");
            e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasOne<TjenesteEntitet>().WithMany().HasForeignKey(x => x.FraTjenesteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TjenesteEntitet>().WithMany().HasForeignKey(x => x.TilTjenesteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<EksternTjenestereferanseEntitet>().WithMany().HasForeignKey(x => x.TilEksternReferanseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<HendelseEntitet>().WithMany().HasForeignKey(x => x.HendelseId);
            e.HasIndex(x => x.FraTjenesteId).HasDatabaseName("ix_tjenesteavhengigheter_fra");
            e.HasIndex(x => x.TilTjenesteId).HasDatabaseName("ix_tjenesteavhengigheter_til");
            e.HasIndex(x => x.TilEksternReferanseId).HasDatabaseName("ix_tjenesteavhengigheter_til_ekstern");
        });

        b.Entity<EksternTjenestereferanseEntitet>(e =>
        {
            e.ToTable("eksterne_tjenestereferanser");
            e.HasKey(x => x.Id).HasName("eksterne_tjenestereferanser_pkey");
            e.Property(x => x.Organisasjonsnummer).HasColumnName("organisasjonsnummer");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.Url).HasColumnName("url");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            // Ikke unik — (organisasjonsnummer, navn) er idempotent-nøkkelen OpprettAsync matcher på i C#,
            // men to forskjellige plassholdere for SAMME org (ulikt navn) er fullt gyldig og forventet.
            e.HasIndex(x => new { x.Organisasjonsnummer, x.Navn }).HasDatabaseName("ix_eksterne_tjenestereferanser_orgnr_navn");
        });

        b.Entity<HandbokRettskildeomfangEntitet>(e =>
        {
            e.ToTable("handbok_rettskildeomfang");
            e.HasKey(x => x.Id).HasName("handbok_rettskildeomfang_pkey");
            e.Property(x => x.HandbokId).HasColumnName("handbok_id");
            e.Property(x => x.TilRettskildeId).HasColumnName("til_rettskilde_id");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            e.HasOne<RettskildeEntitet>().WithMany().HasForeignKey(x => x.HandbokId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RettskildeEntitet>().WithMany().HasForeignKey(x => x.TilRettskildeId);

            e.HasIndex(x => new { x.HandbokId, x.TilRettskildeId }).IsUnique()
                .HasDatabaseName("ux_handbok_rettskildeomfang");
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

        b.Entity<DatasettVerdiEntitet>(e =>
        {
            e.ToTable("datasett_verdier");
            e.HasKey(x => x.Id).HasName("datasett_verdier_pkey");
            e.Property(x => x.DatasettId).HasColumnName("datasett_id");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.VerdiJson).HasColumnName("verdi").HasColumnType(jsonKolonne);
            e.Property(x => x.Kilde).HasColumnName("kilde");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            e.HasOne<DatasettEntitet>().WithMany().HasForeignKey(x => x.DatasettId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            // To filtrerte unik-indekser i stedet for én komposittindeks: Postgres/SQLite behandler NULL
            // som distinkt i en vanlig unik-indeks, så uten filtrering ville "kun én standardverdi-rad
            // per Datasett" (virksomhet_id IS NULL) aldri faktisk blitt håndhevet.
            e.HasIndex(x => new { x.DatasettId, x.VirksomhetId }).IsUnique()
                .HasDatabaseName("ux_datasett_verdier_datasett_virksomhet").HasFilter("virksomhet_id IS NOT NULL");
            e.HasIndex(x => x.DatasettId).IsUnique()
                .HasDatabaseName("ux_datasett_verdier_standardverdi").HasFilter("virksomhet_id IS NULL");
        });

        b.Entity<VilkarEntitet>(e =>
        {
            e.ToTable("vilkar");
            e.HasKey(x => x.Id).HasName("vilkar_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.TjenesteId).HasColumnName("tjeneste_id");
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
            e.HasOne<TjenesteEntitet>().WithMany().HasForeignKey(x => x.TjenesteId);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_vilkar_virksomhet");
            e.HasIndex(x => x.TjenesteId).HasDatabaseName("ix_vilkar_tjeneste");
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
            e.Property(x => x.Rekkefolge).HasColumnName("rekkefolge").HasDefaultValue(0);

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

        b.Entity<VilkarstreKommentarEntitet>(e =>
        {
            e.ToTable("vilkarstre_kommentarer", t => t.HasCheckConstraint(
                "ck_vilkarstre_kommentarer_mal_type", "mal_type IN ('vilkar', 'regelnode', 'unntak')"));
            e.HasKey(x => x.Id).HasName("vilkarstre_kommentarer_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.MalType).HasColumnName("mal_type");
            e.Property(x => x.MalId).HasColumnName("mal_id");
            e.Property(x => x.Dokumenttype).HasColumnName("dokumenttype");
            e.Property(x => x.TekstHtml).HasColumnName("tekst_html");
            e.Property(x => x.Rekkefolge).HasColumnName("rekkefolge").HasDefaultValue(0);
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);
            e.Property(x => x.SistEndretAv).HasColumnName("sist_endret_av");
            e.Property(x => x.SistEndretTidspunkt).HasColumnName("sist_endret_tidspunkt");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);
            e.HasIndex(x => new { x.MalType, x.MalId }).HasDatabaseName("ix_vilkarstre_kommentarer_mal");
        });

        b.Entity<KunnskapsbibliotekLenkeEntitet>(e =>
        {
            e.ToTable("kunnskapsbibliotek_lenker");
            e.HasKey(x => x.Id).HasName("kunnskapsbibliotek_lenker_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Url).HasColumnName("url");
            e.Property(x => x.Beskrivelse).HasColumnName("beskrivelse");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_kunnskapsbibliotek_lenker_virksomhet");
        });

        b.Entity<KunnskapsbibliotekFilEntitet>(e =>
        {
            e.ToTable("kunnskapsbibliotek_filer");
            e.HasKey(x => x.Id).HasName("kunnskapsbibliotek_filer_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.Filnavn).HasColumnName("filnavn");
            e.Property(x => x.Tittel).HasColumnName("tittel");
            e.Property(x => x.Filtype).HasColumnName("filtype");
            e.Property(x => x.Innhold).HasColumnName("innhold");
            e.Property(x => x.UtvunnetTekst).HasColumnName("utvunnet_tekst");
            e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.VirksomhetId).HasDatabaseName("ix_kunnskapsbibliotek_filer_virksomhet");
        });

        b.Entity<LovdataKatalogOppforingEntitet>(e =>
        {
            e.ToTable("lovdata_katalog_oppforinger");
            e.HasKey(x => x.Datokode).HasName("lovdata_katalog_oppforinger_pkey");
            e.Property(x => x.Datokode).HasColumnName("datokode");
            e.Property(x => x.Tittel).HasColumnName("tittel");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.SistOppdatert).HasColumnName("sist_oppdatert");

            e.HasIndex(x => x.SistOppdatert).HasDatabaseName("ix_lovdata_katalog_oppforinger_sist_oppdatert");
        });

        b.Entity<EksternKildeEntitet>(e =>
        {
            e.ToTable("eksterne_kilder");
            e.HasKey(x => x.Id).HasName("eksterne_kilder_pkey");
            e.Property(x => x.Kildetype).HasColumnName("kildetype");
            e.Property(x => x.EksternId).HasColumnName("ekstern_id");
            e.Property(x => x.RaaJson).HasColumnName("raa_json").HasColumnType(jsonKolonne);
            e.Property(x => x.InnholdsHash).HasColumnName("innholds_hash");
            e.Property(x => x.HentetTidspunkt).HasColumnName("hentet_tidspunkt");

            // Idempotens-nøkkelen re-høsting matcher på — se EksternKildeEntitet punkt (a)/(c).
            e.HasIndex(x => new { x.Kildetype, x.EksternId }).IsUnique().HasDatabaseName("ux_eksterne_kilder_kildetype_ekstern_id");
            e.HasIndex(x => x.Kildetype).HasDatabaseName("ix_eksterne_kilder_kildetype");
        });

        // ---------- Punkt 8 (avklaringsrunde 2026-08-13): NettsideDokumentEntitet er fjernet — en ----------
        // ---------- nettside ER nå en ordinær RettskildeEntitet (Kildetype="Brukerveiledning"). ----------

        b.Entity<NettsideStiEntitet>(e =>
        {
            e.ToTable("nettside_stier", t => t.HasCheckConstraint(
                "ck_nettside_stier_stitype", "sti_type IN ('tematisk', 'organisatorisk')"));
            e.HasKey(x => x.Id).HasName("nettside_stier_pkey");
            e.Property(x => x.RettskildeId).HasColumnName("rettskilde_id");
            e.Property(x => x.Sti).HasColumnName("sti");
            e.Property(x => x.StiType).HasColumnName("sti_type");

            e.HasOne<RettskildeEntitet>().WithMany(r => r.Stier)
                .HasForeignKey(x => x.RettskildeId).OnDelete(DeleteBehavior.Cascade);

            // §3.4: samme nettside kan ha FLERE stier, men IKKE den samme stien registrert to ganger.
            e.HasIndex(x => new { x.RettskildeId, x.StiType, x.Sti }).IsUnique()
                .HasDatabaseName("ux_nettside_stier_rettskilde_type_sti");
        });

        b.Entity<NettsideLenkeEntitet>(e =>
        {
            e.ToTable("nettside_lenker", t => t.HasCheckConstraint(
                "ck_nettside_lenker_type", "type IN ('lenker_til', 'lovdatalenke')"));
            e.HasKey(x => x.Id).HasName("nettside_lenker_pkey");
            e.Property(x => x.FraNodeId).HasColumnName("fra_node_id");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.RaaHref).HasColumnName("raa_href");
            e.Property(x => x.AnkerTekst).HasColumnName("anker_tekst");
            e.Property(x => x.TilEidKandidat).HasColumnName("til_eid_kandidat");
            e.Property(x => x.TilRettskildeId).HasColumnName("til_rettskilde_id");

            e.HasOne<RettskildeNodeEntitet>().WithMany()
                .HasForeignKey(x => x.FraNodeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RettskildeEntitet>().WithMany()
                .HasForeignKey(x => x.TilRettskildeId);

            e.HasIndex(x => new { x.FraNodeId, x.RaaHref }).HasDatabaseName("ix_nettside_lenker_fra_href");
            e.HasIndex(x => x.TilRettskildeId).HasDatabaseName("ix_nettside_lenker_til_rettskilde");
        });
    }
}
