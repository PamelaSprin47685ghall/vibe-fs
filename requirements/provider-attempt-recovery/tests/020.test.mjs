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

test('WHAT[provider-attempt-recovery-020] budget_replay_exposes_domain_evidence_without_resume_authority', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const XWireSurface = await import("../../../dist/Context/Prefix/XWireSurface.js");
const RecoveryScope = await import("../../../dist/OpenCode/Host/PluginRecoveryScopeSurface.js");

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

test('WHAT[provider-attempt-recovery-020] recovery holds manual-only ownership and publishes no background resume', () => {
  const scope = RecoveryScope.createRecoveryScope()
  assert.deepEqual(RecoveryScope.recoveryOwnership(scope), { manuals: 0 })

  const plan = buildPlan({ session: 'ses-r10', physical: 'phys-r10' })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-r10', 'phys-r10', plan.handle).outcome, 'Admitted')
  assert.equal(RecoveryScope.bindAttempt(scope, 'ses-r10', 'phys-r10', 'run-r10').bound, true)
  assert.equal(RecoveryScope.consumeAttempt(scope, 'ses-r10', 'run-r10').consumed, true)

  assert.deepEqual(RecoveryScope.recoveryOwnership(scope), { manuals: 0 })
})
}
