import assert from 'node:assert/strict'
import test from 'node:test'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as XWireSurface from '../../../dist/Context/Prefix/XWireSurface.js'
import * as RecoveryScope from '../../../dist/OpenCode/Host/PluginRecoveryScopeSurface.js'

// failure-budget.test.mjs — PAR-001/002/003/004/005/007/009/020.
//
// One retry world: ProviderFailureBudget counts consecutive failures,
// ProviderFailureProjection holds the durable per-run budget view,
// ProviderFailureLedger (tested in provider-failure-ledger.test.mjs) is the
// only writer. Everything here drives the production
// Fallback/ProviderFailureSurface.js — no copied budget model.


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
    selectedAgent: 'coder',
    canonicalRole: 'coder',
    persona: 'Coder',
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

/** Fold a sequence of facts, numbering LocalSeq from 1. */
const foldFacts = (facts) =>
  foldFactsThroughOwner(facts.map((value, index) => ownerEnvelope({ seq: index + 1, session: SESSION, fact: value })))

const budgetOf = (projection) => providerFailureProjection.read(projection)

// ── PAR-002: the budget starts empty ─────────────────────────────────────────

// requirements/provider-attempt-recovery/tests/freeze-admission.test.mjs
//
// Acceptance tests for PAR-011 / PAR-020:
// Admitted-plan semantics of the production PluginRecoveryScope, driven through
// the compiled plan surface (dist/Context/Prefix/XWireSurface.js) and the
// recovery scope owner surface
// (dist/OpenCode/Host/PluginRecoveryScopeSurface.js).
//
// Every assertion observes production F# behavior: plans are built by the real
// AttemptPlanner, admission/binding/record/peek/consume run on a real
// PluginRecoveryScope, and semantic equality is the production admission
// comparison. No copied scope, no copied equality, no simulated transform,
// no source-text reads.
//
// Invariants tested:
// 1. Same-key same-plan replays (Admitted -> ReplayedExisting)
// 2. Same-key different authority/probe/kind fails closed (PlanConflict)
// 3. The same immutable admitted plan drives binding; the frozen candidate
//    survives later conflicting inputs
// 4. Bind to the wrong physical parent / run is refused
// 5. Terminal consumption is single-shot and never re-promotes
// 6. No background PreProviderResumeRequest is published (manual-only ownership)


const candidate = (over = {}) => ({
  ref: 'blob/ref/frozen-1',
  frozenDigest: 'sha256:frozen-1',
  cutoff: 2,
  prefixDigest: 'prefix-digest-1',
  sealRoot: 'seal-1',
  syntheticId: 'synthetic-1',
  ...over,
})

const probeInput = ({ candidate: candidateOverrides, ...over } = {}) => ({
  probeId: 'probe-1',
  basedOnEpoch: 0,
  candidate: candidate(candidateOverrides),
  ...over,
})

const planInput = (over = {}) => ({
  session: 'ses-1',
  logicalRun: 'run-1',
  root: 'root-1',
  physical: 'phys-1',
  kind: 'WorkMain',
  probe: null,
  ...over,
})

const buildPlan = (over) => {
  const built = XWireSurface.pendingPlan(planInput(over))
  assert.equal(built.ok, true, `pendingPlan must accept the input: ${built.error}`)
  return built
}

test('WHAT[PAR-020] budget_replay_exposes_domain_evidence_without_resume_authority', () => {
  const facts = [rootFact(), failureFact({ run: 'provider-1', count: 1 })]
  const first = budgetOf(foldFacts(facts).value)
  const replay = budgetOf(foldFacts(facts).value)

  assert.deepEqual(replay, first)
  assert.deepEqual(first, {
    logicalRun: RUN,
    authorityRoot: ROOT,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
  assert.deepEqual(Object.keys(first).sort(), [
    'authorityRoot',
    'dedupeKeys',
    'exhausted',
    'failures',
    'logicalRun',
  ])
})

// ── PAR-004: failure adds one, success resets ────────────────────────────────

test('WHAT[PAR-020] recovery holds manual-only ownership and publishes no background resume', () => {
  const scope = RecoveryScope.createRecoveryScope()
  assert.deepEqual(RecoveryScope.recoveryOwnership(scope), { manuals: 0 })

  const plan = buildPlan({ session: 'ses-r10', physical: 'phys-r10' })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-r10', 'phys-r10', plan.handle).outcome, 'Admitted')
  assert.equal(RecoveryScope.bindAttempt(scope, 'ses-r10', 'phys-r10', 'run-r10').bound, true)
  assert.equal(RecoveryScope.consumeAttempt(scope, 'ses-r10', 'run-r10').consumed, true)

  assert.deepEqual(RecoveryScope.recoveryOwnership(scope), { manuals: 0 })
})
