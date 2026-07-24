using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

public sealed class RegelIdeDbContext(DbContextOptions<RegelIdeDbContext> options) : DbContext(options)
{
    public DbSet<Virksomhet> Virksomheter => Set<Virksomhet>();
    public DbSet<Bruker> Brukere => Set<Bruker>();
    public DbSet<RettskildeEntitet> Rettskilder => Set<RettskildeEntitet>();
    public DbSet<RettskildeNodeEntitet> RettskildeNoder => Set<RettskildeNodeEntitet>();
    public DbSet<RettskildeReferanseEntitet> RettskildeReferanser => Set<RettskildeReferanseEntitet>();
    public DbSet<TekstTaggEntitet> TekstTagger => Set<TekstTaggEntitet>();
    public DbSet<ProveniensEntitet> Proveniens => Set<ProveniensEntitet>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Virksomhet>(e =>
        {
            e.ToTable("virksomheter");
            e.HasKey(x => x.Id).HasName("virksomheter_pkey");
            e.Property(x => x.Navn).HasColumnName("navn");
            e.Property(x => x.Organisasjonsnummer).HasColumnName("organisasjonsnummer");
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").HasDefaultValueSql("now()");
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
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").HasDefaultValueSql("now()");
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
            e.Property(x => x.Sorteringsrekkefolge).HasColumnName("sorteringsrekkefolge");

            e.HasOne<RettskildeEntitet>().WithMany(r => r.Noder)
                .HasForeignKey(x => x.RettskildeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RettskildeNodeEntitet>().WithMany()
                .HasForeignKey(x => x.ParentNodeId);

            e.HasIndex(x => new { x.RettskildeId, x.Eid }).IsUnique().HasDatabaseName("rettskilde_noder_rettskilde_id_eid_key");
            e.HasIndex(x => x.ParentNodeId).HasDatabaseName("ix_rettskilde_noder_parent");
            e.HasIndex(x => new { x.Eid, x.TekstHash }).HasDatabaseName("ix_rettskilde_noder_eid_hash");
        });

        b.Entity<RettskildeReferanseEntitet>(e =>
        {
            e.ToTable("rettskilde_referanser");
            e.HasKey(x => x.Id).HasName("rettskilde_referanser_pkey");
            e.Property(x => x.FraNodeId).HasColumnName("fra_node_id");
            e.Property(x => x.TilRettskildeId).HasColumnName("til_rettskilde_id");
            e.Property(x => x.TilEid).HasColumnName("til_eid");

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
            e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").HasDefaultValueSql("now()");

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

        b.Entity<ProveniensEntitet>(e =>
        {
            e.ToTable("proveniens");
            e.HasKey(x => x.Id).HasName("proveniens_pkey");
            e.Property(x => x.VirksomhetId).HasColumnName("virksomhet_id");
            e.Property(x => x.EntitetType).HasColumnName("entitet_type");
            e.Property(x => x.EntitetId).HasColumnName("entitet_id");
            e.Property(x => x.EndretAv).HasColumnName("endret_av");
            e.Property(x => x.Dato).HasColumnName("dato").HasDefaultValueSql("now()");
            e.Property(x => x.Handling).HasColumnName("handling");
            e.Property(x => x.KildeReferanserJson).HasColumnName("kilde_referanser").HasColumnType("jsonb");
            e.Property(x => x.AiForslagVersjon).HasColumnName("ai_forslag_versjon");
            e.Property(x => x.GodkjentAv).HasColumnName("godkjent_av");

            e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.VirksomhetId);

            e.HasIndex(x => new { x.EntitetType, x.EntitetId }).HasDatabaseName("ix_proveniens_entitet");
        });
    }
}
