-- Piloten #375 (barnehageloven): måltall mot det GODKJENTE resultatet (AK 5).
-- Kjøres etter Johanns gjennomgang i forslagskøen. Bare lesing.
--
--   export MSYS_NO_PATHCONV=1
--   podman exec -i regel-ide-postgres-1 psql -U postgres -d regelide < data/fasit/strukturmodell/pilot-375-maling.sql
--
-- Pilotens forslag er radene som lasteren skrev 2026-10-28 (22:43–22:53 UTC) med hjemmel i de 20 kildene.
-- En rad som er revidert i gjennomgangen (POST /api/strukturkanter/{id}/revisjon), er foreldet og har en etterfølger
-- (erstatter_kant_id). Utfallet er da etterfølgerens status, og raden regnes som «godkjent med retting».

\pset footer off

create temp view pilot_kilder as
select unnest(array[
  'cc249ccf-642c-46e5-b600-ad5e6536d85f','7336cbdd-fe95-40cd-99af-5a087cd925ff','e10f216d-c5e3-4450-8cc6-7deb61089844',
  'd6bf421a-30b3-4d1d-9be0-a2fac4a8672d','144457fa-f123-4a1f-a513-8c159f4192ac','304adc0b-7b9c-44e6-9d2b-14aac1bf04c3',
  'bb421303-527c-4339-8a8b-e005cf5f5252','4f41fcf5-24a6-4529-9cec-dbc726f72f45','46450957-7a07-4aa6-a992-f1579ce54aa8',
  'c5c52a2c-923e-451e-b8fc-c06966ce5624','8d8680e2-fbc3-4d3c-9cd4-69ef591e15d9','4345e0ee-98a4-411e-aa38-7c1201a6a1f3',
  '5f2537a6-cf3b-415e-88a0-ced12d6f561c','4a7fd337-31e2-4050-b9bd-6a383e199aaa','bfb70c59-1250-4372-a5c0-ee2c50b22fc1',
  '6a45f7bd-be95-4b7d-9292-d1c18366e5c3','971ae1f2-55c7-48c7-a951-9774a925bcca','1edbc1f9-f16f-4be1-ace0-0a185cc7174e',
  'd12aff08-ef02-47b3-af7e-0899db992973','3643ad1e-6402-4a53-88e4-32ac26f0f6c5']::uuid[]) as id;

create temp view pilot_forslag as
select k."Id" as id, split_part(k.oppdagelses_kilde, ':', 1) as lag, k.kategori, k.typekode, k.status, k.entitetsstatus
from strukturkanter k
where k.hjemmel_rettskilde_id in (select id from pilot_kilder)
  and k.oppdagelses_kilde ~ '^(monster|ki):'
  and k.opprettet_tidspunkt between '2026-10-28 22:43:00+00' and '2026-10-28 22:53:00+00';

-- Siste versjon av hver rad (følger erstatter_kant_id framover).
create temp view pilot_utfall as
with recursive kjede(start, id, status, kunnskapsstatus, steg) as (
  select f.id, k."Id", k.status, k.kunnskapsstatus, 0
  from pilot_forslag f join strukturkanter k on k."Id" = f.id
  union all
  select kj.start, n."Id", n.status, n.kunnskapsstatus, kj.steg + 1
  from kjede kj join strukturkanter n on n.erstatter_kant_id = kj.id
)
select distinct on (start) start as id, status, kunnskapsstatus, steg > 0 as revidert
from kjede order by start, steg desc;

\echo '1. Utfall per lag (forventet 71 rader: 38 mønster, 33 KI)'
select f.lag, u.status, u.revidert, count(*) from pilot_forslag f join pilot_utfall u using (id) group by 1, 2, 3 order by 1, 2, 3;

\echo '2. Presisjon per lag og kategori: (godkjent + faglig vurdert) / (vurdert). Ikke vurderte rader telles ikke.'
select f.lag, f.kategori,
       count(*) filter (where u.status in ('godkjent', 'faglig_vurdert')) as godkjent,
       count(*) filter (where u.status = 'avvist') as avvist,
       count(*) filter (where u.status in ('maskinelt_foreslatt', 'manuelt_foreslatt')) as ikke_vurdert,
       round(100.0 * count(*) filter (where u.status in ('godkjent', 'faglig_vurdert'))
             / nullif(count(*) filter (where u.status in ('godkjent', 'faglig_vurdert', 'avvist')), 0), 1) as presisjon_pst
from pilot_forslag f join pilot_utfall u using (id) group by rollup (1, 2) order by 1, 2;

\echo '3. Gjenfinning: godkjente maskinforslag / alle godkjente utsagn med hjemmel i pilotkildene (maskin + manuelt lagt til etter piloten).'
\echo '   Mål for hvor mye av det godkjente resultatet konverteringen foreslo. Manuelle rader regnes fra pilotdagen.'
with godkjent_maskin as (
  select count(*) as n from pilot_utfall where status in ('godkjent', 'faglig_vurdert')
), manuelle as (
  select count(*) as n from strukturkanter k
  where k.hjemmel_rettskilde_id in (select id from pilot_kilder)
    and k.entitetsstatus = 'gjeldende' and k.status in ('godkjent', 'faglig_vurdert')
    and k.opprettet_tidspunkt > '2026-10-28 22:53:00+00'
    and k.oppdagelses_kilde !~ '^(monster|ki):'
    and k.erstatter_kant_id is null   -- revisjoner av pilotrader er ikke nye utsagn
)
select g.n as godkjente_maskinforslag, m.n as manuelt_lagt_til,
       round(100.0 * g.n / nullif(g.n + m.n, 0), 1) as gjenfinning_pst
from godkjent_maskin g, manuelle m;

\echo '4. Utsagn per kunnskapsstatus etter gjennomgangen (siste versjon)'
select coalesce(u.kunnskapsstatus, '(ikke vurdert)') as kunnskapsstatus, u.status, count(*)
from pilot_utfall u group by 1, 2 order by 1, 2;
