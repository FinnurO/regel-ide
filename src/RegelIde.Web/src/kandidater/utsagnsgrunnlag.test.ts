import { describe, expect, it } from 'vitest';
import { grunnlagFeil, sikkerhetFeil, tilRequest, TOM_SIKKERHET, TOMT_GRUNNLAG } from './utsagnsgrunnlag';

// [Ny, issue #385 AC8–AC10] Veiviserens «Hva bygger dette på?» og «Hvor sikkert er det?».
describe('utsagnsgrunnlag', () => {
  const kilde = {
    ...TOMT_GRUNNLAG, grunnlag: 'kilde' as const, kildeTekst: 'Om Tilsynsutvalget', kildeType: 'nettside_annet' as const,
    kildeDokumentasjon: 'sekundaer' as const, lenker: ['https://www.tilsynsutvalget.no/', '', 'https://www.tilsynsutvalget.no/'],
  };

  it('krever et valg, og minst én gyldig lenke for kilde utenfor korpus', () => {
    expect(grunnlagFeil(TOMT_GRUNNLAG)).not.toBeNull();
    expect(grunnlagFeil(kilde)).toBeNull();
    expect(grunnlagFeil({ ...kilde, lenker: [''] })).toMatch(/minst én lenke/);
    expect(grunnlagFeil({ ...kilde, lenker: ['tilsynsutvalget.no'] })).toMatch(/ikke en gyldig lenke/);
    expect(grunnlagFeil({ ...TOMT_GRUNNLAG, grunnlag: 'hjemmel' })).toMatch(/bestemmelsen/);
  });

  it('«hjemmel mangler» er uavklart med det som mangler i begrunnelsen', () => {
    const mangler = { ...TOMT_GRUNNLAG, grunnlag: 'mangler' as const };
    expect(grunnlagFeil(mangler)).toBeNull();
    expect(sikkerhetFeil(mangler, { ...TOM_SIKKERHET, godkjenning: 'faglig_vurdert', kunnskapsstatus: 'fortolkning' })).toMatch(/uavklart/);
    expect(sikkerhetFeil(mangler, { ...TOM_SIKKERHET, godkjenning: 'faglig_vurdert', kunnskapsstatus: 'uavklart' })).toMatch(/mangler/);
    expect(sikkerhetFeil(mangler, { ...TOM_SIKKERHET, godkjenning: 'faglig_vurdert', kunnskapsstatus: 'uavklart', begrunnelse: 'Vedtaket er ikke funnet.' })).toBeNull();
  });

  it('kunnskapsstatusen må passe grunnlaget (docs/profil/03 §5)', () => {
    const s = { ...TOM_SIKKERHET, godkjenning: 'godkjent' as const };
    expect(sikkerhetFeil(kilde, { ...s, kunnskapsstatus: 'tekstfunn' })).toMatch(/bestemmelse/);
    expect(sikkerhetFeil(kilde, { ...s, kunnskapsstatus: 'eksternt_dokumentert' })).toBeNull();
    expect(sikkerhetFeil(kilde, { ...s, kunnskapsstatus: 'fortolkning', begrunnelse: 'x' })).toMatch(/tolkningsargument/);
    expect(sikkerhetFeil(kilde, { ...TOM_SIKKERHET, kunnskapsstatus: 'eksternt_dokumentert' })).toMatch(/godkjenning/);
  });

  it('forespørselen har lenkene uten dubletter og tomme, og ingen hjemmel ved kilde', () => {
    const r = tilRequest(kilde, { ...TOM_SIKKERHET, godkjenning: 'faglig_vurdert', kunnskapsstatus: 'eksternt_dokumentert' }, 'rk');
    expect(r).toMatchObject({ grunnlag: 'kilde', hjemmelRettskildeId: null, kildeLenker: ['https://www.tilsynsutvalget.no/'], godkjenning: 'faglig_vurdert' });
    expect(tilRequest({ ...TOMT_GRUNNLAG, grunnlag: 'hjemmel', hjemmelEid: 'e' }, { ...TOM_SIKKERHET, godkjenning: 'godkjent' }, 'rk'))
      .toMatchObject({ hjemmelRettskildeId: 'rk', hjemmelEid: 'e', kildeLenker: null, kunnskapsstatus: null });
  });
});
