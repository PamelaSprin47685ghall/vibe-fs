import assert from 'node:assert/strict';
import test from 'node:test';
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js';

function events() {
  return [
    { type: 'InquiryCreated', inquiry: 'iq_01h455vb4pex5vsknk084sn02x', revision: 0, parent: 'none', question: 'closure probe', pluginLock: [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }], budget: { compute: 10, budget: 10 }, root: { envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { question: 'closure probe' } }, adapter: 'question-to-root:v1' } },
    { type: 'GraphPatched', inquiry: 'iq_01h455vb4pex5vsknk084sn02x', revision: 1, parent: 'ev0', patch: { kind: 'K', relation: 'R', target: 'n01h455vb4pex5vsknk084sn02b' } },
  ];
}

const dagCase = {
  domain: { kind: 'finite-dag', nodes: 4, edges: [[0, 1], [1, 2], [2, 3]] },
  operator: { kind: 'dag-recurrence', order: [0, 1, 2, 3], seeds: { 0: 1 }, rule: 'max-pred-plus-one' },
  expectFixedPoint: { 0: 1, 1: 2, 2: 3, 3: 4 },
};

const latticeCase = {
  domain: { kind: 'lattice', monotone: true, continuous: true },
  operator: { kind: 'finite-map', start: 0, table: [1, 2, 2] },
  expectFixedPoint: 2,
};

const contractionCase = {
  domain: { kind: 'metric', modulus: 0.5 },
  operator: { kind: 'affine', factor: 0.5, offset: 1, start: 0 },
  expectFixedPoint: 2,
};

test('WHAT[EPI-026] declared_finite_dag_lattice_or_contraction_converges_or_closure_claims_without_domain', async () => {
  const surface = gecSurface;
  const dag = await surface.replay({ events: events(), closure: { domain: dagCase.domain, operator: dagCase.operator, maxIterations: 50 } });
  assert.equal(dag.ok, true);
  assert.equal(dag.converged, true, 'an acyclic recurrence must reach its least fixed point');
  assert.deepEqual(dag.fixedPoint, dagCase.expectFixedPoint, 'dag evaluation must follow topological order from the seeds');

  const lattice = await surface.replay({ events: events(), closure: { domain: latticeCase.domain, operator: latticeCase.operator, maxIterations: 50 } });
  assert.equal(lattice.ok, true);
  assert.equal(lattice.converged, true, 'a monotone continuous map on a finite chain must converge');
  assert.equal(lattice.fixedPoint, latticeCase.expectFixedPoint, 'finite iteration from bottom must yield the least fixed point');
  assert.ok(!('unique' in lattice) || lattice.unique !== true, 'continuity alone must not imply uniqueness');

  const contraction = await surface.replay({ events: events(), closure: { domain: contractionCase.domain, operator: contractionCase.operator, maxIterations: 50 } });
  assert.equal(contraction.ok, true);
  assert.equal(contraction.converged, true, 'a contraction with modulus below one must converge');
  assert.ok(Math.abs(contraction.fixedPoint - contractionCase.expectFixedPoint) < 1e-9, 'affine iteration must approach factor-adjusted offset');
  assert.equal(contraction.unique, true, 'only the contraction modulus may support a uniqueness claim');
});

test('WHAT[EPI-026] undeclared_domain_reports_bounded_residual_without_uniqueness_or_naive_fixed_point_overclaims', async () => {
  const surface = gecSurface;
  const missing = [
    { name: 'absent domain', closure: { operator: latticeCase.operator, maxIterations: 8 } },
    { name: 'empty domain', closure: { domain: { kind: 'none' }, operator: latticeCase.operator, maxIterations: 8 } },
    { name: 'non-monotone lattice', closure: { domain: { kind: 'lattice', monotone: false, continuous: true }, operator: latticeCase.operator, maxIterations: 8 } },
    { name: 'non-continuous lattice', closure: { domain: { kind: 'lattice', monotone: true, continuous: false }, operator: latticeCase.operator, maxIterations: 8 } },
    { name: 'unit modulus', closure: { domain: { kind: 'metric', modulus: 1 }, operator: contractionCase.operator, maxIterations: 8 } },
    { name: 'cyclic graph without lattice claim', closure: { domain: { kind: 'finite-dag', nodes: 2, edges: [[0, 1], [1, 0]] }, operator: dagCase.operator, maxIterations: 8 } },
  ];
  for (const { name, closure } of missing) {
    const result = await surface.replay({ events: events(), closure });
    assert.equal(result.ok, true, `${name} must still replay with bounded effort`);
    assert.equal(result.converged, false, `${name} must not claim convergence`);
    assert.equal(result.fixedPoint, null, `${name} must not present a fixed point`);
    assert.ok(result.iterations <= 8, `${name} must respect the iteration bound`);
    assert.ok(result.residual && typeof result.residual.bound === 'number' && Number.isFinite(result.residual.bound), `${name} must report a finite residual bound`);
    assert.ok(!('unique' in result) || result.unique !== true, `${name} must never claim a unique fixed point`);
  }
});

test('WHAT[EPI-026] async_convergence_stays_conjecture_without_gap_fairness_and_order_or_partial_evidence_claims_limit', async () => {
  const surface = gecSurface;
  const partial = await surface.replay({
    events: events(),
    closure: {
      domain: { kind: 'lattice', monotone: true, continuous: true },
      operator: latticeCase.operator,
      maxIterations: 8,
      async: { finiteDecisionSet: true, strictGap: false, vanishingUncertainty: true, fairScheduling: true, orderAware: true },
    },
  });
  assert.equal(partial.ok, true);
  assert.equal(partial.converged, false, 'a missing strict gap must keep async convergence a conjecture');
  assert.equal(partial.fixedPoint, null);
  assert.ok(partial.residual && Number.isFinite(partial.residual.bound), 'bounded residual must still be reported');
});

test('WHAT[EPI-026] declared_misspecification_downgrades_async_closure_even_when_other_flags_pass', async () => {
  const surface = gecSurface;
  const misspecified = await surface.replay({
    events: events(),
    closure: {
      domain: { kind: 'lattice', monotone: true, continuous: true },
      operator: latticeCase.operator,
      maxIterations: 8,
      async: { finiteDecisionSet: true, strictGap: true, vanishingUncertainty: true, fairScheduling: true, orderAware: true, correctSpecification: false },
    },
  });
  assert.equal(misspecified.ok, true);
  assert.equal(misspecified.converged, false, 'a declared misspecification must downgrade async convergence');
  assert.ok(misspecified.residual && Number.isFinite(misspecified.residual.bound), 'bounded residual must still be reported');
});
