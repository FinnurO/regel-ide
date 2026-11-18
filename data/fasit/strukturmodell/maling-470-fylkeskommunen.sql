-- [Ny, issue #470] Målingen av standardregelen for «fylkeskommunen» mot den delte basen, bare lesing.
-- Speiler GeneriskStandardregelTjeneste (sperrene i SperreAsync, i samme rekkefølge) og FylkeskommunenOppsettTjeneste (bindingsforslagene).
-- Kjøres i en READ ONLY-transaksjon:
--   podman exec -i regel-ide-postgres-1 psql -U postgres -d regelide -P pager=off < data/fasit/strukturmodell/maling-470-fylkeskommunen.sql
-- Utvalget er det samme som standardregel-dekning: gjeldende primærrettskilder der «fylkeskommunen» står som eget ord.
BEGIN READ ONLY;

WITH k AS (
  SELECT r."Id" AS id, coalesce(r.kortnavn, r.tittel) AS t, r.virksomhet_id, coalesce(r.tittel, '') || ' ' || coalesce(r.kortnavn, '') AS tk
  FROM rettskilder r
  WHERE r.entitetsstatus = 'gjeldende' AND r.importrolle = 'primaer'
    AND EXISTS (SELECT 1 FROM rettskilde_noder n WHERE n.rettskilde_id = r."Id" AND n.entitetsstatus = 'gjeldende' AND n.tekst ~* '\mfylkeskommunen\M')
),
noder AS (
  SELECT n.rettskilde_id AS id, n.eid, n.tekst FROM rettskilde_noder n JOIN k ON k.id = n.rettskilde_id
  WHERE n.entitetsstatus = 'gjeldende' AND n.tekst IS NOT NULL
),
-- (b) navngitt fylkeskommune: ett eller to ord med stor forbokstav foran «fylkeskommune(r)», unntatt ordene i GeneriskStandardregelTjeneste.IkkeNavn,
-- eller «fylkeskommunen i» foran et slikt ord.
navngitt AS (
  SELECT DISTINCT id FROM noder, regexp_matches(tekst, '(?:^|[^[:alpha:]-])((?:[A-ZÆØÅ][[:alpha:]-]+ og )?[A-ZÆØÅ][[:alpha:]-]+) fylkeskommuner?(?:[^[:alpha:]]|$)', 'g') m
  WHERE m[1] LIKE '% og %' OR m[1] NOT IN ('Den','Denne','Dei','Det','Ein','Eit','En','Én','Ei','Enhver','Einkvar','Hver','Kvar','Annen','Anna','Annan',
    'Samme','Same','Vedkommende','Vedkomande','Berørt','Berørte','Aktuell','Aktuelle','Med','Ved','Ny','Nye','Gjeldende','Hvilken','Kva','Eller','Og',
    'Som','Til','Fra','Frå','For','Av','I','At','Om','Hos','Mot','Under','Etter','Begge','Alle','Hele','Heile','Rett','Rette','Ansvarlig','Ansvarleg',
    'Nærmeste','Flere','Fleire','Andre','Øvrige')
  UNION
  SELECT DISTINCT id FROM noder WHERE tekst ~ '(?:^|[^[:alpha:]])[Ff]ylkeskommunen i [A-ZÆØÅ]'
),
utfall AS (
  SELECT k.id, k.t,
    CASE
      WHEN EXISTS (SELECT 1 FROM noder n WHERE n.id = k.id AND n.tekst ~* '\m(med|ved)\s+«?fylkeskommun[a-zæøå]*»?\s+(?:[^.;:]{0,60}?\s)?(menes|meinast|forstås)') THEN 'a_definerer'
      WHEN k.id IN (SELECT id FROM navngitt) THEN 'b_navngir_fylkeskommune'
      WHEN k.virksomhet_id IS NOT NULL THEN 'c_virksomhetsdokument'
      WHEN k.tk ~* 'Svalbard|Jan Mayen|Longyearbyen' THEN 'd_svalbard'
      ELSE 'klassen'
    END AS utfall,
    (SELECT count(*) FROM noder n WHERE n.id = k.id AND n.tekst ~* '\mfylkeskommunen\M') AS noder,
    (SELECT coalesce(sum((SELECT count(*) FROM regexp_matches(n.tekst, '\mfylkeskommunen\M', 'gi'))), 0) FROM noder n WHERE n.id = k.id) AS forekomster,
    EXISTS (SELECT 1 FROM noder n WHERE n.id = k.id AND n.tekst LIKE '%Oslo kommune%' AND lower(n.tekst) LIKE '%fylkeskommun%') AS oslo
  FROM k
)
SELECT utfall, count(*) AS kilder, sum(noder) AS noder, sum(forekomster) AS forekomster, count(*) FILTER (WHERE oslo) AS med_oslo_merknad
FROM utfall GROUP BY ROLLUP (utfall) ORDER BY utfall NULLS LAST;

-- Kildene der en fylkeskommune er navngitt (hull etter sperre b), med antall noder med ordet.
WITH k AS (
  SELECT r."Id" AS id, coalesce(r.kortnavn, r.tittel) AS t FROM rettskilder r
  WHERE r.entitetsstatus = 'gjeldende' AND r.importrolle = 'primaer'
    AND EXISTS (SELECT 1 FROM rettskilde_noder n WHERE n.rettskilde_id = r."Id" AND n.entitetsstatus = 'gjeldende' AND n.tekst ~* '\mfylkeskommunen\M')
)
SELECT k.t, count(*) AS noder_med_ordet,
  (SELECT string_agg(DISTINCT m[1] || ' fylkeskommune', ' | ') FROM rettskilde_noder n2,
     regexp_matches(n2.tekst, '(?:^|[^[:alpha:]-])((?:[A-ZÆØÅ][[:alpha:]-]+ og )?[A-ZÆØÅ][[:alpha:]-]+) fylkeskommuner?(?:[^[:alpha:]]|$)', 'g') m
   WHERE n2.rettskilde_id = k.id AND n2.entitetsstatus = 'gjeldende' AND m[1] NOT IN ('Den','Enhver','Med','Flere')) AS navngitt
FROM k JOIN rettskilde_noder n ON n.rettskilde_id = k.id AND n.entitetsstatus = 'gjeldende' AND n.tekst ~* '\mfylkeskommunen\M'
GROUP BY k.id, k.t ORDER BY 3 NULLS LAST, 2 DESC LIMIT 15;

-- Den generiske navneformen og klassen i dag.
SELECT "Id", term, navneformgrunn, godkjenning, entitetsstatus FROM begreper WHERE navneformgrunn = 'generisk' ORDER BY term;
SELECT "Id", term, godkjenning, kunnskapsstatus, lovreferanse_eid FROM begreper
WHERE medlemskapsregel = 'omraderegister_fylkeskommune' AND entitetsstatus = 'gjeldende';

ROLLBACK;
