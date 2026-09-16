import assert from 'node:assert/strict';
import test from 'node:test';
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js';

function manifest(overrides = {}) {
  return {
    id: 'canon',
    release: '1.0.0',
    abiHash: 'abi-canon-001',
    capabilities: ['observe'],
    dependencies: [],
    schemas: {
      input: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' },
      output: { id: 'sphinx.probe.open/output@1', hash: 'schema-hash-002' },
    },
    ...overrides,
  };
}

function inquiryEvents(lock) {
  return [
    {
      type: 'InquiryCreated',
      inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
      revision: 0,
      parent: 'none',
      question: 'locked question',
      pluginLock: lock,
      budget: { compute: 10, budget: 10 },
      root: { envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { question: 'locked question' } }, adapter: 'question-to-root:v1' },
    },
    {
      type: 'WorkPlanned',
      inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
      revision: 1,
      parent: 'ev0',
      work: { id: 'work_01h455vb4pex5vsknk084sn02y', branch: 'branch_01h455vb4pex5vsknk084sn02z', attempt: 1 },
    },
    {
      type: 'ObservationAccepted',
      inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
      revision: 2,
      parent: 'ev1',
      observation: {
        rootSnapshotHash: 'rootsnap001',
        branch: 'branch_01h455vb4pex5vsknk084sn02z',
        work: 'work_01h455vb4pex5vsknk084sn02y',
        attempt: 1,
        pluginLock: lock,
        schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' },
        promptId: 'prompt-open-001',
        questionId: 'q-001',
        wording: { frame: 'open', polarity: 'neutral' },
        permutation: { candidates: ['a'], labels: ['A'], order: [0] },
        treatment: 'open-first',
        blindToken: 'blind01h455vb4pex5vsknk084sn02e',
        seed: 'seed-0001',
        model: { provider: 'local-sim', name: 'sim-1' },
        sampling: { temperature: 0, maxTokens: 16 },
        usage: { promptTokens: 5, completionTokens: 3 },
        payload: { text: 'hello' },
      },
    },
  ];
}

test('WHAT[EPI-020] missing_dependency_duplicate_release_or_abi_mismatch_fails_closed_or_drifted_plugin_runs', async () => {
  const surface = gecSurface;
  const clean = await surface.bindPlugins({ manifests: [manifest(), manifest({ id: 'helper', release: '2.1.0', abiHash: 'abi-helper-001', dependencies: ['canon'] })] });
  assert.equal(clean.ok, true, 'a satisfied dependency chain must bind');
  assert.ok(clean.lock, 'successful bind must return the immutable lock');

  const cases = [
    {
      name: 'missing dependency',
      manifests: [manifest({ dependencies: ['ghost-plugin'] })],
      code: 'missing-dependency',
    },
    {
      name: 'same id with two releases',
      manifests: [manifest(), manifest({ release: '2.0.0', abiHash: 'abi-canon-002' })],
      code: 'duplicate-release',
    },
    {
      name: 'empty schema hash',
      manifests: [manifest({ schemas: { input: { id: 'sphinx.probe.open/input@1', hash: '' }, output: { id: 'sphinx.probe.open/output@1', hash: 'schema-hash-002' } } })],
      code: 'schema-mismatch',
    },
  ];
  for (const { name, manifests, code } of cases) {
    const result = await surface.bindPlugins({ manifests });
    assert.equal(result.ok, false, `${name} must fail closed`);
    assert.equal(result.error.code, code, `${name} must report ${code}`);
  }

  const abiConflict = await surface.bindPlugins({ manifests: [manifest(), manifest({ abiHash: 'abi-canon-999' })] });
  assert.equal(abiConflict.ok, false, 'two entries for one id and release with different ABI hashes must not bind');
  assert.equal(abiConflict.error.code, 'abi-mismatch');

  const relocked = await surface.bindPlugins({
    manifests: [manifest({ abiHash: 'abi-canon-999' })],
    existingLock: clean.lock,
  });
  assert.equal(relocked.ok, false, 'rebinding against an explicit prior lock with a changed ABI hash must fail');
  assert.equal(relocked.error.code, 'abi-mismatch');
});

test('WHAT[EPI-020] schema_hash_mismatch_rejects_observation_or_content_drift_passes_silently', async () => {
  const surface = gecSurface;
  const lock = [manifest()];
  const events = inquiryEvents(lock);
  const accepted = await surface.replay({ events });
  assert.equal(accepted.ok, true, 'matching schema content hash must replay');

  const drifted = structuredClone(events);
  drifted[2].observation.schema.hash = 'schema-hash-tampered';
  const rejected = await surface.replay({ events: drifted });
  assert.equal(rejected.ok, false, 'tampered schema content hash must be rejected before the observation lands');
  assert.equal(rejected.error.code, 'schema-mismatch');

  const renamed = structuredClone(events);
  renamed[2].observation.schema.id = 'sphinx.probe.open/input@2';
  const renamedResult = await surface.replay({ events: renamed });
  assert.equal(renamedResult.ok, false, 'a schema revision change is an identity change and must be rejected');
  assert.equal(renamedResult.error.code, 'schema-mismatch');
});

test('WHAT[EPI-020] mid_run_plugin_swap_is_rejected_or_lock_is_advisory', async () => {
  const surface = gecSurface;
  const lock = [manifest()];
  const events = inquiryEvents(lock);
  const swapped = structuredClone(events);
  swapped[2].observation.pluginLock = [manifest({ release: '9.9.9', abiHash: 'abi-canon-999' })];
  const result = await surface.replay({ events: swapped });
  assert.equal(result.ok, false, 'running with a different release than the creation lock must fail');
  assert.equal(result.error.code, 'plugin-swapped');

  const added = structuredClone(events);
  added[2].observation.pluginLock = [...lock, manifest({ id: 'late', release: '1.0.0', abiHash: 'abi-late-001' })];
  const addedResult = await surface.replay({ events: added });
  assert.equal(addedResult.ok, false, 'adding an unbound plugin mid-run must fail');
  assert.equal(addedResult.error.code, 'plugin-swapped');
});
