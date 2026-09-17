import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const {
  budget,
  providerFailureProjection,
  fold: foldFactsThroughOwner,
  authorityRootAccepted,
  providerFailureRecorded,
  providerRetryExhausted,
  providerSuccessRecorded,
  envelope: ownerEnvelope,
  providerFailureFactCaseNames,
} = failureOwner
const SESSION = 'ses_a'
const RUN = 'run_L'
const ROOT = 'msg_u1'
const ROOT_SELECTION_IDENTITY_SEED = {
  kind: 'RootSelection',
  participantIdentity: {
    selectedAgent: 'engineer',
    canonicalRole: 'engineer',
    persona: 'Engineer',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}
const identityFor = (run, { logical = RUN, root = ROOT } = {}) =>
  budget.attemptIdentity(SESSION, logical, root, run)
const rootFact = ({ kind = 'HumanRoot', logical = RUN, root = ROOT } = {}) =>
  authorityRootAccepted({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    authorityKind: kind,
    identitySeed: ROOT_SELECTION_IDENTITY_SEED,
  })
const failureFact = ({ run, count, logical = RUN, root = ROOT, reason = 'provider_error' }) =>
  providerFailureRecorded({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    providerRun: run,
    consecutiveFailureCount: count,
    reason,
  })
const exhaustedFact = ({ count }) =>
  providerRetryExhausted({
    session: SESSION,
    logicalRun: RUN,
    authorityRoot: ROOT,
    finalConsecutiveFailureCount: count,
  })
const foldFacts = (facts) =>
  foldFactsThroughOwner(facts.map((value, index) => ownerEnvelope({ seq: index + 1, session: SESSION, fact: value })))
const budgetOf = (projection) => providerFailureProjection.read(projection)

test('WHAT[PAR-001] an_accepted_authority_root_creates_the_budget', () => {
  const folded = foldFacts([rootFact()])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  assert.deepEqual(budgetOf(folded.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })
})
test('WHAT[PAR-001] a_record_with_no_accepted_root_stops_the_replay', () => {
  const folded = foldFacts([failureFact({ run: 'run_1', count: 1 })])

  assert.equal(folded.ok, false)
  assert.equal(folded.error.Fact, 'FailureRecorded')
  assert.equal(
    folded.error.Reason,
    'provider failure has no active budget: requires an accepted Authority Root',
  )
})
test('WHAT[PAR-001] an_active_authority_root_must_close_before_replacement', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    failureFact({ run: 'run_2', count: 2 }),
    rootFact({ logical: 'run_M', root: 'msg_u2' }),
  ])

  assert.deepEqual(folded, {
    ok: false,
    error: {
      Fact: 'AuthorityRootAccepted',
      Reason: 'active logical run must close before replacement: active=run_L requested=run_M',
    },
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { JournalSurface_bootWithWriterId: bootWithWriterId, JournalSurface_dispose: dispose } = await import("../../../dist/Persistence/Journal/Surface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const { budget, acceptHumanRoot: failureAcceptHumanRoot } = failureOwner
const SESSION = 'ses_ledger'
async function acceptHumanRoot(journal, userMessageId) {
  const accepted = await failureAcceptHumanRoot(journal, SESSION, userMessageId, 'engineer')
  assert.equal(accepted.ok, true, `AcceptHumanRoot failed: ${accepted.error}`)
}
async function admit(journal, providerRunName) {
  const recorded = await failureOwner.recordConfirmedFailure(
    journal,
    budget.defaultBudget,
    SESSION,
    providerRunName,
    'provider_error',
  )

  if (!recorded.ok) return recorded
  return {
    ok: true,
    value: recorded.outcome === 'RetryExhausted' ? 'RetryExhausted' : 'RetryAuthorized',
  }
}

test('WHAT[PAR-001] no_active_run_records_nothing_and_writes_no_fact', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-norun-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-norun', 'rt_ledger_norun', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    // No AcceptHumanRoot: the session has no failure budget (PAR-001: the
    // budget belongs to a Logical Run; there is no run yet).
    const outcome = await failureOwner.recordConfirmedFailure(
      journal,
      budget.defaultBudget,
      SESSION,
      'msg_asst_ghost',
      'provider_error',
    )
    assert.deepEqual(outcome, { ok: true, outcome: 'NoActiveRun' })

    const state = failureOwner.snapshot(journal, SESSION)
    assert.equal(state, null, 'no budget may exist outside a Logical Run')
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
}
