-- [Ny, issue #386, 2026-10-27] Juristrunden K2, K3 og K5: hjemmelen for begreper som BEHOLDES som rolle/klasse settes til
-- kilden som etablerer eller navngir dem (prinsippet fra #395). Et lukket sett på tre rader (CLAUDE.md §3: opprydding, ikke en
-- funksjon). Godkjenningen røres ikke. De gamle verdiene står i proveniensen (kilde_referanser), så rettingen kan reverseres.
-- Kjørt mot lokal `regelide` 2026-10-27. Idempotent: rader som alt har den nye hjemmelen, endres ikke.
BEGIN;

CREATE TEMP TABLE r386 ON COMMIT DROP AS
SELECT b."Id" AS id, v.eid, v.ks, v.kort, v.begrunnelse,
       jsonb_build_object('lovreferanse_eid', b.lovreferanse_eid, 'lovkilde_id', b.lovkilde_id, 'kunnskapsstatus', b.kunnskapsstatus,
                          'begrunnelse', b.begrunnelse, 'juristrunde', '#386 ' || v.kort) AS foer
FROM (VALUES
  ('3466d93c', 'https://lovdata.no/eli/lov/1990/06/29/50/nor/§2-5/ledd-1', 'tekstfunn', 'K2',
   'Energiloven § 2-5 første ledd: «Departementet utpeker en reguleringsmyndighet …». Juristrunden #386 K2.'),
  ('ba1c5bff', 'https://lovdata.no/eli/lov/2002/06/28/61/nor/§4/ledd-1', 'tekstfunn', 'K3',
   'Naturgassloven § 4 første ledd: «Departementet utpeker en reguleringsmyndighet …». Juristrunden #386 K3.'),
  ('99f62b57', 'https://lovdata.no/eli/forskrift/2020/12/18/2867/nor/punkt-1', 'normalisering', 'K5',
   'Navnevedtaket FOR-2020-12-18-2867 punkt 1: fylkesmannsembetene heter «Statsforvalteren» (fra 1. januar 2021). Klassen er embetene; '
   || 'ordet står i bestemt form. Tidligere hjemmel (reindriftsloven § 62 fjerde ledd) var en tilfeldig forekomst. Juristrunden #386 K5.')
) AS v(prefiks, eid, ks, kort, begrunnelse)
JOIN begreper b ON b."Id"::text LIKE v.prefiks || '%' AND b.entitetsstatus = 'gjeldende'
WHERE b.lovreferanse_eid IS DISTINCT FROM v.eid;

-- Hjemmelnoden må finnes som gjeldende node (ingen gjettet eId).
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM r386 r WHERE NOT EXISTS (
      SELECT 1 FROM rettskilde_noder n JOIN rettskilder k ON k."Id" = n.rettskilde_id
      WHERE n.eid = r.eid AND n.entitetsstatus = 'gjeldende' AND k.entitetsstatus = 'gjeldende')) THEN
    RAISE EXCEPTION 'En hjemmel-eId finnes ikke i korpuset';
  END IF;
END $$;

UPDATE begreper b SET lovreferanse_eid = r.eid, kunnskapsstatus = r.ks, begrunnelse = r.begrunnelse,
  sist_endret_av = 'Kari Jurist', sist_endret_tidspunkt = now()
FROM r386 r WHERE b."Id" = r.id;

INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
SELECT gen_random_uuid(), 'begrep', r.id, 'Kari Jurist', now(), 'endret', r.foer FROM r386 r;

SELECT b.term, b.begrepskategori, b.lovreferanse_eid, b.kunnskapsstatus FROM begreper b JOIN r386 r ON r.id = b."Id";
COMMIT;
