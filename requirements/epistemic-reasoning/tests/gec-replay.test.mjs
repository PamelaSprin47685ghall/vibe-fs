import assert from 'node:assert/strict';
import test from 'node:test';
import { loadGecSurface } from './gec-support.mjs';

function baseEvents() {
  return [
    {
      type: 'InquiryCreated',
      inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
      revision: 0,
      parent: 'none',
      question: 'does wording shift the answer?',
      pluginLock: [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }],
      budget: { compute: 10, budget: 10 },
      root: { envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { question: 'does wording shift the answer?' } }, adapter: 'question-to-root:v1' },
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
        pluginLock: [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }],
        schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' },
        promptId: 'prompt-open-001',
        questionId: 'q-001',
        wording: { frame: 'open', polarity: 'neutral' },
        permutation: { candidates: ['a', 'b'], labels: ['A', 'B'], order: [1, 0] },
        treatment: 'open-first',
        blindToken: 'blind01h455vb4pex5vsknk084sn02e',
        seed: 'seed-0001',
        model: { provider: 'local-sim', name: 'sim-1' },
        sampling: { temperature: 0, maxTokens: 64 },
        usage: { promptTokens: 12, completionTokens: 7 },
        payload: { text: 'first observation', n: 1, nested: { a: [1, 2], b: { c: 'x' } } },
      },
    },
    {
      type: 'CertificatePatched',
      inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
      revision: 3,
      parent: 'ev2',
      patch: { node: 'n01h455vb4pex5vsknk084sn02b', slot: 'bound', lower: 0.4, upper: 0.6 },
    },
    {
      type: 'BudgetDebited',
      inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
      revision: 4,
      parent: 'ev3',
      debit: { compute: 3, budget: 3 },
    },
    {
      type: 'AnswerCommitted',
      inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
      revision: 5,
      parent: 'ev4',
      answer: { text: 'stable answer', basis: ['finding-1'] },
    },
  ];
}

function reverseKeys(value) {
  if (Array.isArray(value)) return value.map(reverseKeys);
  if (value !== null && typeof value === 'object') {
    const entries = Object.entries(value).reverse();
    const out = {};
    for (const [key, entryValue] of entries) out[key] = reverseKeys(entryValue);
    return out;
  }
  return value;
}

test('WHAT[EPI-017] replay_is_key_order_invariant_or_stringify_hash_breaks_on_reordered_keys', async () => {
  const surface = await loadGecSurface();
  const events = baseEvents();
  const first = await surface.semanticHash({ events });
  assert.match(first.hash, /^[0-9a-f]{64}$/);
  const replayed = await surface.replay({ events });
  assert.equal(replayed.ok, true);
  assert.equal(replayed.stateHash, first.hash, 'replay state hash must equal the canonical semantic hash');

  const permutations = [events.map(reverseKeys), events.slice().map((event) => reverseKeys({ ...event }))];
  for (const permuted of permutations) {
    const hashed = await surface.semanticHash({ events: permuted });
    assert.equal(hashed.hash, first.hash, 'reordered object keys must not change the canonical hash');
    const again = await surface.replay({ events: permuted });
    assert.equal(again.ok, true);
    assert.equal(again.stateHash, first.hash, 'replay must be invariant to key order');
    assert.deepEqual(again.state, replayed.state, 'replayed state must be identical across key orders');
  }

  const reorderedEvents = events.slice().reverse();
  const moved = await surface.semanticHash({ events: reorderedEvents });
  assert.notEqual(moved.hash, first.hash, 'event sequence order is semantic and must change the hash');
});

test('WHAT[EPI-017] replay_consumes_accepted_observations_without_provider_recall_or_replay_hits_network', async () => {
  const surface = await loadGecSurface();
  const events = baseEvents();
  const first = await surface.replay({ events });
  const second = await surface.replay({ events });
  assert.equal(first.ok, true);
  assert.equal(second.ok, true);
  assert.equal(second.stateHash, first.stateHash, 'replay must be deterministic across identical calls');
  assert.deepEqual(second.state, first.state);
  assert.ok(!('providerCalls' in second) || second.providerCalls === 0, 'replay must not record provider invocations');

  const altered = structuredClone(events);
  altered[2].observation.payload.text = 'different provider wording';
  const diverged = await surface.replay({ events: altered });
  assert.equal(diverged.ok, true);
  assert.notEqual(diverged.stateHash, first.stateHash, 'different accepted payloads must diverge even with the same seed');
});

test('WHAT[EPI-017] replay_rejects_observations_missing_protocol_bindings_or_partial_provenance_replays', async () => {
  const surface = await loadGecSurface();
  const required = [
    'rootSnapshotHash',
    'branch',
    'work',
    'attempt',
    'pluginLock',
    'schema',
    'promptId',
    'questionId',
    'wording',
    'permutation',
    'treatment',
    'blindToken',
    'seed',
    'model',
    'sampling',
    'usage',
  ];
  for (const field of required) {
    const events = baseEvents();
    const observation = structuredClone(events[2].observation);
    delete observation[field];
    events[2] = { ...events[2], observation };
    const result = await surface.replay({ events });
    assert.equal(result.ok, false, `missing ${field} must fail closed`);
    assert.ok(result.error && typeof result.error.code === 'string', 'failure must carry a typed error code');
  }
});
