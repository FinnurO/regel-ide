namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #385 AC8–AC10, Johann 2026-10-16] Grunnlaget og statusen for et modellutsagn som veiviseren (eller aktørsiden) oppretter:
/// navneform, aktørtype, medlemskap og binding. Samme felt som strukturkantene etter #367 (docs/profil/03 §5), samlet i én verdi slik at
/// veiviserens to nye steg — «Hva bygger dette på?» og «Hvor sikkert er det?» — gir de samme reglene for alt den lager.
/// <list type="bullet">
/// <item><b>Grunnlag</b> (<see cref="Grunnlag"/>): <c>hjemmel</c> (bestemmelse i korpus, eId), <c>kilde</c> (kilde utenfor korpus med type,
/// dokumentasjonsgrad og ÉN ELLER FLERE lenker) eller <c>mangler</c> — «hjemmel mangler», lagret som synlig hull. Et hull er bare lov som
/// <c>uavklart</c>, og begrunnelsen sier hva som mangler (metodenotatet: grunnlaget for uavklart er «hva som mangler»).</item>
/// <item><b>Kunnskapsstatus</b>: tekstfunn og normalisering krever hjemmel, eksternt dokumentert krever kilde, fortolkning krever begrunnelse
/// og minst ett tolkningsargument. Avledet (premisser) og foreldet (revisjon) settes ikke her. NULL = ikke vurdert.</item>
/// <item><b>Godkjenning</b>: ett av <see cref="Strukturkanter.Statuser"/> unntatt <c>avvist</c>. Veiviseren tilbyr bare <c>faglig_vurdert</c>
/// og <c>godkjent</c> — et menneske har vurdert det; maskinene foreslår (<c>maskinelt_foreslatt</c>).</item>
/// </list>
/// Ingenting forhåndsvelges eller utledes her (CLAUDE.md §8): en verdi som mangler, er en feil hos kalleren og avvises med en melding.
/// </summary>
public sealed record Utsagnsgrunnlag
{
    public const string Hjemmel = "hjemmel";
    public const string Kilde = "kilde";
    public const string Mangler = "mangler";
    public static readonly string[] Grunnlagstyper = [Hjemmel, Kilde, Mangler];

    /// <summary>Kunnskapsstatusene veiviseren tilbyr (avledet og foreldet settes aldri ved opprettelse).</summary>
    public static readonly string[] TillatteKunnskapsstatuser =
    [
        Strukturkanter.Tekstfunn, Strukturkanter.Normalisering, Strukturkanter.Fortolkning, Strukturkanter.EksterntDokumentert,
        Strukturkanter.Uavklart, Strukturkanter.Omstridt,
    ];

    public required string Grunnlag { get; init; }
    public Guid? HjemmelRettskildeId { get; init; }
    public string? HjemmelEid { get; init; }
    public string? KildeTekst { get; init; }
    public string? KildeType { get; init; }
    public string? KildeDokumentasjon { get; init; }
    public IReadOnlyList<string>? KildeLenker { get; init; }
    public string? Kunnskapsstatus { get; init; }
    public required string Godkjenning { get; init; }
    public string? Begrunnelse { get; init; }
    public IReadOnlyList<string>? Tolkningsargumenter { get; init; }

    /// <summary>Lenkene uten tomme og dubletter, i rekkefølgen de kom.</summary>
    public List<string>? Lenker => KildeLenker?.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim())
        .Distinct(StringComparer.Ordinal).ToList() is { Count: > 0 } l ? l : null;

    public string? RenBegrunnelse => string.IsNullOrWhiteSpace(Begrunnelse) ? null : Begrunnelse.Trim();
    public List<string>? RenArgumenter => Tolkningsargumenter is { Count: > 0 } a ? a.Distinct(StringComparer.Ordinal).ToList() : null;

    /// <summary>Kaster <see cref="ArgumentException"/> med en lesbar melding når grunnlaget ikke holder. Sjekker ikke at noden finnes —
    /// det krever databasen (<see cref="AktoridentitetTjeneste.FinnHjemmelnodeAsync"/>).</summary>
    public void Valider()
    {
        if (!Grunnlagstyper.Contains(Grunnlag))
        {
            throw new ArgumentException($"Ukjent grunnlag '{Grunnlag}'. Gyldige: hjemmel, kilde, mangler.");
        }
        switch (Grunnlag)
        {
            case Hjemmel:
                if (HjemmelRettskildeId is null || string.IsNullOrWhiteSpace(HjemmelEid))
                {
                    throw new ArgumentException("Hjemmel i korpus krever rettskilde og bestemmelse (eId).");
                }
                if (KildeTekst is not null || KildeType is not null || KildeDokumentasjon is not null || Lenker is not null)
                {
                    throw new ArgumentException("Oppgi ENTEN hjemmel i korpus ELLER kilde utenfor korpus, ikke begge.");
                }
                break;
            case Kilde:
                if (HjemmelRettskildeId is not null || HjemmelEid is not null)
                {
                    throw new ArgumentException("Oppgi ENTEN hjemmel i korpus ELLER kilde utenfor korpus, ikke begge.");
                }
                if (string.IsNullOrWhiteSpace(KildeTekst))
                {
                    throw new ArgumentException("Kilde utenfor korpus krever en beskrivelse av kilden.");
                }
                if (KildeType is null || !Strukturkanter.KildeUtenforKorpusTyper.Contains(KildeType))
                {
                    throw new ArgumentException(
                        $"Kilde utenfor korpus krever kildetype. Gyldige: {string.Join(", ", Strukturkanter.KildeUtenforKorpusTyper)}.");
                }
                if (KildeDokumentasjon is null || !Strukturkanter.KildeDokumentasjoner.Contains(KildeDokumentasjon))
                {
                    throw new ArgumentException("Kilde utenfor korpus krever dokumentasjon: primaer eller sekundaer.");
                }
                if (Lenker is null)
                {
                    throw new ArgumentException("Kilde utenfor korpus krever minst én lenke til dokumentasjonen (issue #385 AC8).");
                }
                foreach (var l in Lenker)
                {
                    if (!Uri.TryCreate(l, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        throw new ArgumentException($"«{l}» er ikke en gyldig lenke (http eller https).");
                    }
                }
                break;
            case Mangler:
                if (HjemmelRettskildeId is not null || HjemmelEid is not null || KildeTekst is not null || KildeType is not null
                    || KildeDokumentasjon is not null || Lenker is not null)
                {
                    throw new ArgumentException("«Hjemmel mangler» kan ikke ha hjemmel eller kilde.");
                }
                if (Kunnskapsstatus != Strukturkanter.Uavklart || RenBegrunnelse is null)
                {
                    throw new ArgumentException(
                        "«Hjemmel mangler» lagres som uavklart, med en begrunnelse som sier hva som mangler (docs/profil/03 §5).");
                }
                break;
        }

        if (Kunnskapsstatus is not null)
        {
            if (!TillatteKunnskapsstatuser.Contains(Kunnskapsstatus))
            {
                throw new ArgumentException(
                    $"Kunnskapsstatus '{Kunnskapsstatus}' kan ikke settes her. Gyldige: {string.Join(", ", TillatteKunnskapsstatuser)}.");
            }
            if (Kunnskapsstatus is Strukturkanter.Tekstfunn or Strukturkanter.Normalisering && Grunnlag != Hjemmel)
            {
                throw new ArgumentException($"{Kunnskapsstatus} krever hjemmel i korpus med eId (docs/profil/03 §5).");
            }
            if (Kunnskapsstatus == Strukturkanter.EksterntDokumentert && Grunnlag != Kilde)
            {
                throw new ArgumentException("eksternt_dokumentert krever kilde utenfor korpus (docs/profil/03 §5).");
            }
            if (Kunnskapsstatus == Strukturkanter.Fortolkning && (RenBegrunnelse is null || RenArgumenter is null))
            {
                throw new ArgumentException("fortolkning krever begrunnelse og minst ett tolkningsargument (docs/profil/03 §6).");
            }
        }
        if (RenArgumenter?.FirstOrDefault(x => !Strukturkanter.Tolkningsargumenter.Contains(x)) is { } ukjent)
        {
            throw new ArgumentException($"Ukjent tolkningsargument '{ukjent}'. Gyldige: {string.Join(", ", Strukturkanter.Tolkningsargumenter)}.");
        }
        if (!Strukturkanter.Statuser.Contains(Godkjenning) || Godkjenning == Strukturkanter.Avvist)
        {
            throw new ArgumentException(
                $"Ugyldig godkjenning '{Godkjenning}'. Gyldige ved opprettelse: {string.Join(", ", Strukturkanter.Statuser.Where(s => s != Strukturkanter.Avvist))}.");
        }
    }

    /// <summary>Skriver grunnlaget og statusen på en navneform (eller et annet begrep). Hjemmelen er <c>LovkildeId</c> +
    /// <c>LovreferanseEid</c>. Kalleren har validert.</summary>
    public void SkrivTil(BegrepEntitet b, string oppdagelsesKilde)
    {
        b.LovkildeId = Grunnlag == Hjemmel ? HjemmelRettskildeId : b.LovkildeId;
        b.LovreferanseEid = Grunnlag == Hjemmel ? HjemmelEid!.Trim() : null;
        b.KildeUtenforKorpusTekst = Grunnlag == Kilde ? KildeTekst!.Trim() : null;
        b.KildeUtenforKorpusType = Grunnlag == Kilde ? KildeType : null;
        b.KildeUtenforKorpusDokumentasjon = Grunnlag == Kilde ? KildeDokumentasjon : null;
        b.KildeUtenforKorpusLenker = Grunnlag == Kilde ? Lenker : null;
        b.Kunnskapsstatus = Kunnskapsstatus;
        b.Begrunnelse = RenBegrunnelse;
        b.Tolkningsargumenter = RenArgumenter;
        b.Godkjenning = Godkjenning;
        b.OppdagelsesKilde = oppdagelsesKilde;
    }

    /// <summary>Skriver grunnlaget og statusen på et aktørtype-utsagn. Kalleren har validert.</summary>
    public void SkrivTil(AktortypeUtsagnEntitet a)
    {
        a.HjemmelRettskildeId = Grunnlag == Hjemmel ? HjemmelRettskildeId : null;
        a.HjemmelEid = Grunnlag == Hjemmel ? HjemmelEid!.Trim() : null;
        a.KildeUtenforKorpusTekst = Grunnlag == Kilde ? KildeTekst!.Trim() : null;
        a.KildeUtenforKorpusType = Grunnlag == Kilde ? KildeType : null;
        a.KildeUtenforKorpusDokumentasjon = Grunnlag == Kilde ? KildeDokumentasjon : null;
        a.KildeUtenforKorpusLenker = Grunnlag == Kilde ? Lenker : null;
        a.Kunnskapsstatus = Kunnskapsstatus;
        a.Begrunnelse = RenBegrunnelse;
        a.Tolkningsargumenter = RenArgumenter;
        a.Status = Godkjenning;
    }
}
