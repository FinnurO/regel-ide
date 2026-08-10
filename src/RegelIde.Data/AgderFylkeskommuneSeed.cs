using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Byggesteg 5 runde 3 — testcase for å teste ekte KI-agentkjøring mot ekte data. Oppretter KUN
/// `Virksomhet` + én `Bruker` — selve rettskilde-import/fil-opplasting/lenker/agent-kjøring gjøres
/// LIVE gjennom appen (det er selve poenget med testen), ikke forhåndsseedet her.
/// <para>
/// Ingen API/UI finnes i dag for å opprette en ny virksomhet+bruker — kun ett idempotent
/// oppstartsblokk i Program.cs (<c>if (!await db.Brukere.AnyAsync())</c>) som IKKE kjører igjen når
/// brukere allerede finnes. Guardet derfor på et spesifikt virksomhetsnavn, samme mønster som
/// <see cref="TestkommuneInnholdSeed"/>, ikke på hele Brukere-tabellen.
/// </para>
/// </summary>
public static class AgderFylkeskommuneSeed
{
    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        if (await db.Virksomheter.AnyAsync(v => v.Navn == "Agder fylkeskommune", ct)) return;

        var agder = new Virksomhet { Id = Guid.NewGuid(), Navn = "Agder fylkeskommune", OpprettetTidspunkt = DateTimeOffset.UtcNow };
        db.Virksomheter.Add(agder);
        // Rolle="Fagansvarlig", ikke "Jurist" — en lang rekke eksisterende API-tester (Byggesteg5-,
        // Byggesteg2-, Byggesteg4EndepunktTests m.fl.) gjør et globalt, IKKE virksomhet-scopet
        // .Single(b => b.Rolle == "Jurist") mot /api/brukere og forutsetter dermed at nøyaktig én
        // bruker med den rollen finnes i hele den delte testdatabasen.
        db.Brukere.Add(new Bruker { Id = Guid.NewGuid(), Navn = "Silje Jurist", VirksomhetId = agder.Id, Rolle = "Fagansvarlig" });
        await db.SaveChangesAsync(ct);
    }
}
