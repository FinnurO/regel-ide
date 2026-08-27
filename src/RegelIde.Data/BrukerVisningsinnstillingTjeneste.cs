using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Én brukers foretrukne fanerekkefølge/-synlighet og accordion-rekkefølge/åpen-tilstand på
/// Tjeneste-siden (2026-08-27, Tjenestedetalj-redesignrunden) — se
/// <see cref="BrukerVisningsinnstillingEntitet"/>s klassekommentar for hvorfor dette er PER BRUKER,
/// ikke per tjeneste, og hvorfor egendefinerte innholdselementer er bevisst utelatt herfra.
/// </summary>
public sealed record VisningsinnstillingInput(
    IReadOnlyList<string> Seksjonsrekkefolge,
    IReadOnlyList<string> SkjulteSeksjoner,
    IReadOnlyList<string> AccordionRekkefolge,
    IReadOnlyDictionary<string, bool> AccordionApne);

public sealed class BrukerVisningsinnstillingTjeneste(RegelIdeDbContext db)
{
    /// <summary>De 7 faste fane-nøklene, standard rekkefølge. "oversikt" er alltid først og er ALDRI
    /// med i denne listen — se BrukerVisningsinnstillingEntitet.</summary>
    public static readonly string[] StandardSeksjonsrekkefolge =
        ["vilkarstre", "innhold", "status", "regelverk", "hendelser", "handlinger", "avhengigheter"];

    /// <summary>De 9 faste accordion-nøklene i Innhold-fanen, standard rekkefølge.</summary>
    public static readonly string[] StandardAccordionRekkefolge =
        ["grunnleggende", "tidspunkt", "innsender", "vedlegg", "opplysninger", "veiledning", "innsending", "kontakt", "innebaerer"];

    private static VisningsinnstillingInput Standard() => new(
        StandardSeksjonsrekkefolge, [], StandardAccordionRekkefolge,
        StandardAccordionRekkefolge.ToDictionary(k => k, k => k == "grunnleggende"));

    /// <summary>Ingen gjettet fallback på HVILKE nøkler som finnes — kun en dokumentert, eksplisitt
    /// standardtilstand (<see cref="Standard"/>) når brukeren ikke har lagret noe ennå.</summary>
    public async Task<VisningsinnstillingInput> HentAsync(Guid brukerId, CancellationToken ct = default)
    {
        var rad = await db.BrukerVisningsinnstillinger.FirstOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        if (rad is null) return Standard();

        return new VisningsinnstillingInput(
            JsonSerializer.Deserialize<List<string>>(rad.SeksjonsrekkefolgeJson, JsonSerialiseringHjelper.Innstillinger) ?? [],
            JsonSerializer.Deserialize<List<string>>(rad.SkjulteSeksjonerJson, JsonSerialiseringHjelper.Innstillinger) ?? [],
            JsonSerializer.Deserialize<List<string>>(rad.AccordionRekkefolgeJson, JsonSerialiseringHjelper.Innstillinger) ?? [],
            JsonSerializer.Deserialize<Dictionary<string, bool>>(rad.AccordionApneJson, JsonSerialiseringHjelper.Innstillinger) ?? []);
    }

    public async Task<VisningsinnstillingInput> LagreAsync(Guid brukerId, VisningsinnstillingInput input, CancellationToken ct = default)
    {
        var rad = await db.BrukerVisningsinnstillinger.FirstOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        if (rad is null)
        {
            rad = new BrukerVisningsinnstillingEntitet { Id = Guid.NewGuid(), BrukerId = brukerId };
            db.BrukerVisningsinnstillinger.Add(rad);
        }

        rad.SeksjonsrekkefolgeJson = JsonSerializer.Serialize(input.Seksjonsrekkefolge, JsonSerialiseringHjelper.Innstillinger);
        rad.SkjulteSeksjonerJson = JsonSerializer.Serialize(input.SkjulteSeksjoner, JsonSerialiseringHjelper.Innstillinger);
        rad.AccordionRekkefolgeJson = JsonSerializer.Serialize(input.AccordionRekkefolge, JsonSerialiseringHjelper.Innstillinger);
        rad.AccordionApneJson = JsonSerializer.Serialize(input.AccordionApne, JsonSerialiseringHjelper.Innstillinger);
        await db.SaveChangesAsync(ct);
        return input;
    }
}
