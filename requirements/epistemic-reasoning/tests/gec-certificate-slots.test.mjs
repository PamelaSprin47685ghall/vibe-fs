import assert from 'node:assert/strict';
import test from 'node:test';
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js';

const nodeId = 'n01h455vb4pex5vsknk084sn02b';

function baseCertificate() {
  return { nodeId, witnesses: ['ev-root'], derivations: ['ev-root'] };
}

function patchesInCanonicalOrder() {
  return [
    { slot: 'exact', value: { mean: 0.7 }, guarantee: { kind: 'inclusion' }, witnesses: ['ev-exact'], derivations: ['ev-exact'] },
    { slot: 'bound', lower: 0.6, upper: 0.8, guarantee: { kind: 'inclusion' }, witnesses: ['ev-bound'], derivations: ['ev-bound'] },
    {
      slot: 'sample',
      summary: { mean: 0.71, n: 2000 },
      guarantee: { kind: 'coverage', level: 0.95, assumptions: ['iid-draws'], error: 0.02 },
      witnesses: ['ev-sample'],
      derivations: ['ev-sample'],
    },
    { slot: 'ordinal', constraints: [{ before: 'a', after: 'b' }], guarantee: { kind: 'ordinal' }, witnesses: ['ev-ord'], derivations: ['ev-ord'] },
    {
      slot: 'latent',
      posterior: { family: 'dirichlet', params: [7, 3] },
      guarantee: { kind: 'coverage', level: 0.9, assumptions: ['correct-spec'], error: 0.05 },
      witnesses: ['ev-latent'],
      derivations: ['ev-latent'],
    },
    { slot: 'residual', value: 0.04, witnesses: ['ev-res'], derivations: ['ev-res'] },
  ];
}

test('WHAT[EPI-016] single_certificate_holds_exact_bound_sample_ordinal_latent_together_or_solver_mode_splits_state', async () => {
  const surface = gecSurface;
  let certificate = baseCertificate();
  for (const patch of patchesInCanonicalOrder()) {
    const result = await surface.refineCertificate({ certificate, patch });
    assert.equal(result.ok, true, `slot ${patch.slot} must apply without evicting siblings`);
    certificate = result.certificate;
  }
  assert.equal(certificate.nodeId, nodeId);
  assert.ok(certificate.exact, 'exact slot must survive alongside other slots');
  assert.ok(certificate.lowerEnvelope !== undefined || certificate.bound !== undefined || certificate.lower !== undefined, 'lower bound slot must survive');
  assert.ok(certificate.upperEnvelope !== undefined || certificate.bound !== undefined || certificate.upper !== undefined, 'upper bound slot must survive');
  assert.ok(certificate.sampleSummary || certificate.sample, 'sample slot must survive alongside exact and bound');
  assert.ok(certificate.ordinalConstraints || certificate.ordinal, 'ordinal slot must survive');
  assert.ok(certificate.latentPosterior || certificate.latent, 'latent slot must survive');
  assert.ok(certificate.residual !== undefined, 'residual slot must survive');
  assert.ok(Array.isArray(certificate.witnesses) && certificate.witnesses.length >= 6, 'witnesses must accumulate across slots');
  assert.ok(Array.isArray(certificate.derivations) && certificate.derivations.length >= 6, 'derivations must accumulate across slots');

  const reversed = patchesInCanonicalOrder().slice().reverse();
  let other = baseCertificate();
  for (const patch of reversed) {
    const result = await surface.refineCertificate({ certificate: other, patch });
    assert.equal(result.ok, true, `slot ${patch.slot} must apply in reverse order too`);
    other = result.certificate;
  }
  assert.deepEqual(
    { exact: other.exact, sample: other.sampleSummary || other.sample, residual: other.residual },
    { exact: certificate.exact, sample: certificate.sampleSummary || certificate.sample, residual: certificate.residual },
    'slot values must be order independent while witnesses accumulate',
  );
});

test('WHAT[EPI-016] sample_slot_requires_coverage_assumptions_or_point_estimate_masquerades_as_bound', async () => {
  const surface = gecSurface;
  const invalidPatches = [
    {
      name: 'lower above upper',
      patch: { slot: 'bound', lower: 0.9, upper: 0.1, guarantee: { kind: 'inclusion' } },
      code: 'invalid-bound',
    },
    {
      name: 'sample claims deterministic inclusion',
      patch: { slot: 'sample', summary: { mean: 0.5, n: 100 }, guarantee: { kind: 'inclusion' } },
      code: 'missing-coverage',
    },
    {
      name: 'sample without level',
      patch: { slot: 'sample', summary: { mean: 0.5, n: 100 }, guarantee: { kind: 'coverage', assumptions: ['iid-draws'], error: 0.02 } },
      code: 'missing-coverage',
    },
    {
      name: 'sample without assumptions',
      patch: { slot: 'sample', summary: { mean: 0.5, n: 100 }, guarantee: { kind: 'coverage', level: 0.95, error: 0.02 } },
      code: 'missing-coverage',
    },
    {
      name: 'exact without deterministic guarantee',
      patch: { slot: 'exact', value: { mean: 0.5 } },
      code: 'missing-guarantee',
    },
    {
      name: 'latent without coverage',
      patch: { slot: 'latent', posterior: { family: 'dirichlet', params: [1, 1] }, guarantee: { kind: 'inclusion' } },
      code: 'missing-coverage',
    },
  ];
  for (const { name, patch, code } of invalidPatches) {
    const result = await surface.refineCertificate({ certificate: baseCertificate(), patch });
    assert.equal(result.ok, false, `${name} must fail with a typed error`);
    assert.equal(result.error.code, code, `${name} must report ${code}`);
  }

  const validSample = await surface.refineCertificate({
    certificate: baseCertificate(),
    patch: {
      slot: 'sample',
      summary: { mean: 0.5, n: 500 },
      guarantee: { kind: 'coverage', level: 0.95, assumptions: ['iid-draws'], error: 0.03 },
    },
  });
  assert.equal(validSample.ok, true, 'well-formed sample with coverage must be accepted');
});

test('WHAT[EPI-016] exact_bound_declare_inclusion_while_sample_declares_coverage_or_value_preorder_collapses', async () => {
  const surface = gecSurface;
  const start = baseCertificate();
  const withExact = await surface.refineCertificate({
    certificate: start,
    patch: { slot: 'exact', value: { mean: 0.62 }, guarantee: { kind: 'inclusion' }, witnesses: ['ev-a'], derivations: ['ev-a'] },
  });
  assert.equal(withExact.ok, true);
  const witnessOnly = await surface.refineCertificate({
    certificate: withExact.certificate,
    patch: { slot: 'witness', witnesses: ['ev-extra'], derivations: ['ev-extra'] },
  });
  const afterWitness = witnessOnly.ok ? witnessOnly.certificate : withExact.certificate;
  assert.deepEqual(afterWitness.exact, withExact.certificate.exact, 'witness growth alone must not perturb declared value slots');
  assert.ok(
    (afterWitness.witnesses || []).length > (withExact.certificate.witnesses || []).length ||
      witnessOnly.ok === false,
    'witness growth must accumulate or be an explicit witness slot, never silently rewrite values',
  );

  const sampleAsInclusion = await surface.refineCertificate({
    certificate: withExact.certificate,
    patch: { slot: 'sample', summary: { mean: 0.62, n: 800 }, guarantee: { kind: 'inclusion' } },
  });
  assert.equal(sampleAsInclusion.ok, false, 'sample slot must never accept a deterministic inclusion guarantee');
  assert.equal(sampleAsInclusion.error.code, 'missing-coverage');

  const boundAsCoverage = await surface.refineCertificate({
    certificate: withExact.certificate,
    patch: { slot: 'bound', lower: 0.55, upper: 0.7, guarantee: { kind: 'inclusion' } },
  });
  assert.equal(boundAsCoverage.ok, true, 'bound slot must accept deterministic inclusion');
  assert.deepEqual(boundAsCoverage.certificate.exact, withExact.certificate.exact, 'adding a bound must not evict the exact slot');
});
