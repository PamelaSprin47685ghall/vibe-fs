import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const inquiry = 'iq_01h455vb4pex5vsknk084sn02x';
const branch = 'branch_01h455vb4pex5vsknk084sn02z';
function created() {
  return { type: 'InquiryCreated', inquiry, revision: 0, parent: 'none', question: 'lifecycle probe', pluginLock: [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }], budget: { compute: 10, budget: 10 }, root: { envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { question: 'lifecycle probe' } }, adapter: 'question-to-root:v1' } };
}
function planned(revision, parent, workId, dependencies = []) {
  return { type: 'WorkPlanned', inquiry, revision, parent, work: { id: workId, branch, attempt: 1, dependencies } };
}
function prefix(workId = 'work_aaaa', dependencies = []) {
  return [created(), planned(1, 'ev0', workId, dependencies)];
}
function transition(revision, parent, work, from, to, extra = {}) {
  return {
    type: 'WorkTransitioned',
    inquiry,
    revision,
    parent,
    work,
    from,
    to,
    ...extra,
  };
}
function workRef(workId, attempt = 1, extra = {}) {
  return { id: workId, branch, attempt, ...extra };
}

test('WHAT[epistemic-reasoning-021] ready_requires_satisfied_dependencies_or_dangling_work_runs_early', async () => {
  const surface = gecSurface;
  const blocked = [
    created(),
    planned(1, 'ev0', 'work_aaaa', []),
    planned(2, 'ev1', 'work_bbbb', ['work_aaaa']),
    transition(3, 'ev2', workRef('work_bbbb', 1, { dependencies: ['work_aaaa'] }), 'Planned', 'Ready', {}),
  ];
  const early = await surface.replay({ events: blocked });
  assert.equal(early.ok, false, 'a work item whose dependencies are unfinished must not become ready');
  assert.equal(early.error.code, 'dependency-unsatisfied');

  const legal = [
    ...prefix('work_aaaa', []),
    transition(2, 'ev1', workRef('work_aaaa', 1, { dependencies: [] }), 'Planned', 'Ready', {}),
    transition(3, 'ev2', workRef('work_aaaa', 1, { fence: 'fence-1' }), 'Ready', 'Leased', {}),
    transition(4, 'ev3', workRef('work_aaaa', 1, { fence: 'fence-1', session: 'sess-1' }), 'Leased', 'Executing', {}),
  ];
  const accepted = await surface.replay({ events: legal });
  assert.equal(accepted.ok, true, 'dependency-free work must traverse Planned Ready Leased Executing');

  const skipped = [
    ...prefix('work_aaaa', []),
    transition(2, 'ev1', workRef('work_aaaa', 1, { fence: 'fence-1', session: 'sess-1' }), 'Planned', 'Executing', {}),
  ];
  const rejected = await surface.replay({ events: skipped });
  assert.equal(rejected.ok, false, 'skipping Ready and Leased must be rejected');
  assert.equal(rejected.error.code, 'illegal-transition');

  const fenceless = [
    ...prefix('work_aaaa', []),
    transition(2, 'ev1', workRef('work_aaaa', 1), 'Planned', 'Ready', {}),
    transition(3, 'ev2', workRef('work_aaaa', 1), 'Ready', 'Leased', {}),
  ];
  const noFence = await surface.replay({ events: fenceless });
  assert.equal(noFence.ok, false, 'leasing without fence evidence must be rejected');
  assert.equal(noFence.error.code, 'missing-fence');
});
test('WHAT[epistemic-reasoning-021] terminal_states_never_return_to_executing_and_attempt_accepts_single_observation_or_retry_forks_state', async () => {
  const surface = gecSurface;
  const runToSuccess = (workId) => [
    ...prefix(workId, []),
    transition(2, 'ev1', workRef(workId, 1), 'Planned', 'Ready', {}),
    transition(3, 'ev2', workRef(workId, 1, { fence: 'fence-1' }), 'Ready', 'Leased', {}),
    transition(4, 'ev3', workRef(workId, 1, { fence: 'fence-1', session: 'sess-1' }), 'Leased', 'Executing', {}),
    transition(5, 'ev4', workRef(workId, 1, { fence: 'fence-1', session: 'sess-1' }), 'Executing', 'Succeeded', { observation: 'obs-1' }),
  ];
  const done = await surface.replay({ events: runToSuccess('work_cccc') });
  assert.equal(done.ok, true);

  const illegalResumes = ['Executing', 'Ready', 'Leased', 'InputRequired'];
  for (const to of illegalResumes) {
    const events = [
      ...runToSuccess('work_dddd'),
      transition(6, 'ev5', workRef('work_dddd', 1, { fence: 'fence-1', session: 'sess-1' }), 'Succeeded', to, {}),
    ];
    const result = await surface.replay({ events });
    assert.equal(result.ok, false, `terminal Succeeded must never return to ${to}`);
    assert.equal(result.error.code, 'illegal-transition');
  }

  const duplicateObservation = [
    ...runToSuccess('work_eeee').slice(0, 5),
    transition(5, 'ev4', workRef('work_eeee', 1, { fence: 'fence-1', session: 'sess-1' }), 'Executing', 'Succeeded', { observation: 'obs-1' }),
    transition(6, 'ev5', workRef('work_eeee', 1, { fence: 'fence-1', session: 'sess-1' }), 'Succeeded', 'Succeeded', { observation: 'obs-2' }),
  ];
  const dup = await surface.replay({ events: duplicateObservation });
  assert.equal(dup.ok, false, 'the same attempt must accept at most one observation');
  assert.equal(dup.error.code, 'duplicate-observation');

  const retry = [
    ...prefix('work_ffff', []),
    transition(2, 'ev1', workRef('work_ffff', 1, { fence: 'fence-1', session: 'sess-1' }), 'Planned', 'Ready', {}),
    transition(3, 'ev2', workRef('work_ffff', 1, { fence: 'fence-1', session: 'sess-1' }), 'Ready', 'Leased', {}),
    transition(4, 'ev3', workRef('work_ffff', 1, { fence: 'fence-1', session: 'sess-1' }), 'Leased', 'Executing', {}),
    transition(5, 'ev4', workRef('work_ffff', 1, { fence: 'fence-1', session: 'sess-1' }), 'Executing', 'Failed', { error: 'boom' }),
    transition(6, 'ev5', workRef('work_ffff', 2), 'Failed', 'Ready', {}),
  ];
  const retried = await surface.replay({ events: retry });
  assert.equal(retried.ok, true, 'retry must re-enter Ready with a fresh attempt rather than resuming Executing');
});
test('WHAT[epistemic-reasoning-021] wall_clock_fields_are_rejected_or_timer_drives_lifecycle', async () => {
  const surface = gecSurface;
  const timed = [
    'leaseExpiresAt',
    'heartbeatTimeout',
    'wallClock',
    'expiresAt',
    'timeoutMs',
  ];
  for (const field of timed) {
    const events = [
      ...prefix('work_gggg', []),
      transition(2, 'ev1', workRef('work_gggg', 1), 'Planned', 'Ready', { [field]: 1234567890 }),
    ];
    const result = await surface.replay({ events });
    assert.equal(result.ok, false, `${field} must never drive the lifecycle`);
    assert.equal(result.error.code, 'wall-clock-field');
  }

  const leased = [
    ...prefix('work_hhhh', []),
    transition(2, 'ev1', workRef('work_hhhh', 1), 'Planned', 'Ready', {}),
    transition(3, 'ev2', workRef('work_hhhh', 1, { fence: 'fence-9' }), 'Ready', 'Leased', {}),
    transition(4, 'ev3', workRef('work_hhhh', 1, { fence: 'fence-9' }), 'Leased', 'Cancelled', { reason: 'host-cancel' }),
  ];
  const cancelled = await surface.replay({ events: leased });
  assert.equal(cancelled.ok, true, 'cancel must flow through a durable transition rather than a timer');
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

test('WHAT[epistemic-reasoning-021] soak_replay_rejects_lifecycle_violations_with_stable_codes_across_waves', async () => {
  const lock = [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }]
  const fields = ['leaseExpiresAt', 'heartbeatTimeout', 'wallClock', 'expiresAt', 'timeoutMs']
  for (let wave = 0; wave < WAVES; wave += 1) {
    const tag = String(wave).padStart(4, '0')
    const inquiry = `iq_soaklife${tag}`
    const branch = `branch_soaklife${tag}`
    const workId = `work_soaklife${tag}`
    const created = {
      type: 'InquiryCreated',
      inquiry,
      revision: 0,
      parent: 'none',
      question: 'lifecycle soak probe',
      pluginLock: lock,
      budget: { compute: 10, budget: 10 },
      root: {
        envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { wave } },
        adapter: 'question-to-root:v1',
      },
    }
    const planned = {
      type: 'WorkPlanned',
      inquiry,
      revision: 1,
      parent: 'ev0',
      work: { id: workId, branch, attempt: 1 },
    }
    const ref = (attempt = 1, extra = {}) => ({ id: workId, branch, attempt, ...extra })
    const transition = (revision, parent, work, from, to, extra = {}) => ({
      type: 'WorkTransitioned',
      inquiry,
      revision,
      parent,
      work,
      from,
      to,
      ...extra,
    })
    const legalPrefix = [
      created,
      planned,
      transition(2, 'ev1', ref(), 'Planned', 'Ready', {}),
      transition(3, 'ev2', ref(1, { fence: `fence-${tag}` }), 'Ready', 'Leased', {}),
      transition(4, 'ev3', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Leased', 'Executing', {}),
    ]
    assert.equal(gecSurface.replay({ events: legalPrefix }).ok, true, `wave ${wave} legal prefix must replay`)
    const succeeded = [
      ...legalPrefix,
      transition(5, 'ev4', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Executing', 'Succeeded', { observation: `obs-${tag}` }),
    ]
    assert.equal(gecSurface.replay({ events: succeeded }).ok, true, `wave ${wave} run to Succeeded must replay`)
    const resurrection = gecSurface.replay({
      events: [...succeeded, transition(6, 'ev5', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Succeeded', 'Executing', {})],
    })
    assert.equal(resurrection.ok, false)
    assert.equal(resurrection.error.code, 'illegal-transition', `wave ${wave} terminal must never return to executing`)
    const secondObservation = gecSurface.replay({
      events: [
        ...succeeded,
        transition(6, 'ev5', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Succeeded', 'Succeeded', { observation: `obs-rewrite-${tag}` }),
      ],
    })
    assert.equal(secondObservation.ok, false)
    assert.equal(secondObservation.error.code, 'duplicate-observation', `wave ${wave} one attempt must accept one observation`)
    const timed = gecSurface.replay({
      events: [created, planned, transition(2, 'ev1', ref(), 'Planned', 'Ready', { [fields[wave % fields.length]]: 1234567890 })],
    })
    assert.equal(timed.ok, false)
    assert.equal(timed.error.code, 'wall-clock-field', `wave ${wave} field ${fields[wave % fields.length]} must never drive the lifecycle`)
  }
})
}
