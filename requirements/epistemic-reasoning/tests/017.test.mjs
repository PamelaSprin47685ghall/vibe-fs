import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

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
  const surface = gecSurface;
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
  const surface = gecSurface;
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
  const surface = gecSurface;
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const SEED = 0x51eca129
const WAVES = 12
const SUBJECTS = Array.from({ length: 16 }, (_, index) => `witness-${String(index).padStart(2, '0')}`)
const TREATMENTS = ['wording-a', 'wording-b']
const CANDIDATES = ['c1', 'c2', 'c3']
const PLANTED_EFFECT = 0.15
const xorshift = (initial) => {
  let state = initial >>> 0 || 1
  return () => {
    state ^= state << 13
    state >>>= 0
    state ^= state >>> 17
    state ^= state << 5
    state >>>= 0
    return state / 4294967296
  }
}
const waveInput = (wave) => ({
  rootSnapshot: `snap-soak-${String(wave).padStart(2, '0')}`,
  seed: (SEED + wave * 7919) >>> 0,
  subjects: [...SUBJECTS],
  treatments: [...TREATMENTS],
  candidates: [...CANDIDATES],
})
const waveEvents = (wave, assignment) => {
  const inquiry = `iq_soak${String(wave).padStart(4, '0')}`
  const branch = `branch_soak${String(wave).padStart(4, '0')}`
  const work = `work_soak${String(wave).padStart(4, '0')}`
  const lock = [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }]
  return [
    {
      type: 'InquiryCreated',
      inquiry,
      revision: 0,
      parent: 'none',
      question: 'soak probe',
      pluginLock: lock,
      budget: { compute: 100, budget: 100 },
      root: {
        envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { wave } },
        adapter: 'question-to-root:v1',
      },
    },
    {
      type: 'WorkPlanned',
      inquiry,
      revision: 1,
      parent: 'ev0',
      work: { id: work, branch, attempt: 1 },
    },
    {
      type: 'ObservationAccepted',
      inquiry,
      revision: 2,
      parent: 'ev1',
      observation: {
        rootSnapshotHash: `snap-soak-${String(wave).padStart(2, '0')}`,
        branch,
        work,
        attempt: 1,
        pluginLock: lock,
        schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' },
        promptId: `prompt-soak-${wave}`,
        questionId: `q-soak-${wave}`,
        wording: { frame: 'open', polarity: 'neutral' },
        permutation: { candidates: [...CANDIDATES], labels: ['A', 'B', 'C'], order: [0, 1, 2] },
        treatment: assignment,
        blindToken: `blind01soakwave${String(wave).padStart(4, '0')}`,
        seed: `seed-soak-${wave}`,
        model: { provider: 'local-sim', name: 'sim-1' },
        sampling: { temperature: 0, maxTokens: 16 },
        usage: { promptTokens: 5, completionTokens: 3 },
        payload: { wave },
      },
    },
    { type: 'BudgetDebited', inquiry, revision: 3, parent: 'ev2', debit: { compute: 7, budget: 7 } },
  ]
}

test('WHAT[EPI-017] soak_replay_and_hash_stay_stable_across_repeated_waves', async () => {
  const hashes = new Set()
  for (let wave = 0; wave < WAVES; wave += 1) {
    const events = waveEvents(wave, wave % 2 === 0 ? 'wording-a' : 'wording-b')
    const hashed = gecSurface.semanticHash({ events })
    assert.match(hashed.hash, /^[0-9a-f]{64}$/)
    const first = gecSurface.replay({ events })
    assert.equal(first.ok, true)
    assert.equal(first.stateHash, hashed.hash, `wave ${wave} replay must equal the canonical hash`)
    const second = gecSurface.replay({ events })
    assert.deepEqual(second.state, first.state, `wave ${wave} replay must be deterministic`)
    assert.equal(second.stateHash, first.stateHash)
    hashes.add(first.stateHash)
  }
  assert.equal(hashes.size, WAVES, 'distinct wave payloads must diverge to distinct hashes')
})
}
