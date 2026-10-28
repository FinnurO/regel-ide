namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #390, docs/33 §4.6, Johanns rangering 2026-10-27] Svakeste premiss for ett AVLEDET svar som regnes ut og vises, ikke lagres.
/// Rangeringen, sterkest først: <c>tekstfunn</c> = <c>eksternt_dokumentert</c> &gt; <c>normalisering</c> &gt; <c>avledet</c> &gt;
/// <c>fortolkning</c> &gt; <c>uavklart</c> &gt; <c>omstridt</c>. Et premiss uten vurdert kunnskapsstatus (NULL) regnes ikke som sterkere enn
/// <c>uavklart</c>. <c>foreldet</c> bærer ingen slutning.
/// <para>
/// <b>Avgrensning [LÅST, oppdraget for #390]:</b> brukes bare av tilhørighetssiden (<see cref="OmradeTilhorighetTjeneste"/>). Å håndheve
/// regelen i alle avledninger (utledet medlemskap, standardregler, ankeinstans, pliktoppslag, lagrede F-kanter) er #411, og bygges ikke her.
/// Funksjonen er likevel skrevet som en egen, ren funksjon, så #411 kan ta den i bruk uten å skrive den på nytt.
/// </para>
/// </summary>
public static class SvakestePremiss
{
    /// <summary>Rangen til en kunnskapsstatus i en avledning: 0 er sterkest. NULL (ikke vurdert) er like svak som <c>uavklart</c>;
    /// <c>foreldet</c> og ukjente verdier er svakest av alle (de bærer ingen slutning).</summary>
    public static int Rang(string? kunnskapsstatus) => kunnskapsstatus switch
    {
        Strukturkanter.Tekstfunn or Strukturkanter.EksterntDokumentert => 0,
        Strukturkanter.Normalisering => 1,
        Strukturkanter.Avledet => 2,
        Strukturkanter.Fortolkning => 3,
        Strukturkanter.Uavklart or null => 4,
        Strukturkanter.Omstridt => 5,
        _ => 6,
    };

    /// <summary>
    /// Kunnskapsstatusen et avledet svar kan vises med, gitt det svakeste premisset: <c>avledet</c> når premisset er minst like sterkt som
    /// <c>avledet</c>; ellers premissets nivå (<c>fortolkning</c>, <c>uavklart</c> eller <c>omstridt</c>). Et ikke vurdert premiss gir
    /// <c>uavklart</c> (docs/33 §4.6: «gir ikke et avledet svar som om det var avgjort»). Et foreldet premiss gir <c>uavklart</c> — det
    /// skal ikke forekomme her, fordi bare gjeldende kanter brukes.
    /// </summary>
    public static string SvarStatus(string? svakeste) => Rang(svakeste) switch
    {
        <= 2 => Strukturkanter.Avledet,
        3 => Strukturkanter.Fortolkning,
        5 => Strukturkanter.Omstridt,
        _ => Strukturkanter.Uavklart,
    };

    /// <summary>Det svakeste av premissene (det første ved lik rang, i kjedens rekkefølge), eller null når listen er tom.</summary>
    public static (Guid Id, string? Kunnskapsstatus)? Finn(IEnumerable<(Guid Id, string? Kunnskapsstatus)> premisser)
    {
        (Guid Id, string? Kunnskapsstatus)? svakest = null;
        foreach (var p in premisser)
        {
            if (svakest is null || Rang(p.Kunnskapsstatus) > Rang(svakest.Value.Kunnskapsstatus)) svakest = p;
        }
        return svakest;
    }
}
