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

import assert from 'node:assert/strict'
import test from 'node:test'
import * as XWireSurface from '../../../dist/Context/Prefix/XWireSurface.js'
import * as RecoveryScope from '../../../dist/OpenCode/Host/PluginRecoveryScopeSurface.js'

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

test('WHAT[PAR-011] same-key same-plan replays the admitted plan', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const plan = buildPlan({})

  for (const key of ['failures', 'budget', 'consecutiveFailureCount', 'count', 'exhausted']) {
    assert.equal(key in plan.view, false, `pending plan must not carry a budget snapshot (${key})`)
  }

  const first = RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', plan.handle)
  assert.equal(first.outcome, 'Admitted')

  const second = RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', plan.handle)
  assert.equal(second.outcome, 'ReplayedExisting')
  assert.deepEqual(second.view, first.view)

  assert.doesNotThrow(() => {
    RecoveryScope.recordAttemptPlan(scope, 'ses-1', 'phys-1', plan.handle)
  })
})

test('WHAT[PAR-011] same-key different authority fails closed', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const original = buildPlan({})
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', original.handle).outcome, 'Admitted')

  const rival = buildPlan({ logicalRun: 'run-2' })
  const conflict = RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', rival.handle)
  assert.equal(conflict.outcome, 'PlanConflict')
  assert.equal(conflict.existing.logicalRunId, 'run-1')
  assert.equal(conflict.attempted.logicalRunId, 'run-2')

  assert.throws(
    () => RecoveryScope.recordAttemptPlan(scope, 'ses-1', 'phys-1', rival.handle),
    /HOST-BOUNDARY-008/,
  )
})

test('WHAT[PAR-011] same-key different probe fails closed', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const committed = buildPlan({})
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', committed.handle).outcome, 'Admitted')

  const probed = buildPlan({ probe: probeInput({ probeId: 'probe-99' }) })
  const conflict = RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', probed.handle)
  assert.equal(conflict.outcome, 'PlanConflict')
  assert.equal(conflict.existing.choice, 'UseCommittedEpoch')
  assert.equal(conflict.attempted.choice, 'UsePrefixProbe')
  assert.equal(conflict.attempted.probe.probeId, 'probe-99')
})

test('WHAT[PAR-011] same-key different request kind fails closed', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const work = buildPlan({ probe: probeInput() })
  assert.equal(work.view.choice, 'UsePrefixProbe')
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', work.handle).outcome, 'Admitted')

  const repair = buildPlan({ kind: 'InteractionRepair', probe: probeInput() })
  assert.equal(repair.view.choice, 'UseCommittedEpoch')
  const conflict = RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', repair.handle)
  assert.equal(conflict.outcome, 'PlanConflict')
  assert.equal(conflict.existing.choice, 'UsePrefixProbe')
  assert.equal(conflict.attempted.choice, 'UseCommittedEpoch')
})

test('WHAT[PAR-011] same admitted plan drives binding and the frozen candidate is retained', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const frozen = buildPlan({
    session: 'ses-frozen',
    physical: 'phys-frozen',
    probe: probeInput(),
  })
  const admitted = RecoveryScope.freezeAttemptPlan(scope, 'ses-frozen', 'phys-frozen', frozen.handle)
  assert.equal(admitted.outcome, 'Admitted')

  const bound = RecoveryScope.bindAttempt(scope, 'ses-frozen', 'phys-frozen', 'run-A')
  assert.equal(bound.bound, true)
  assert.equal(bound.view.physical, 'phys-frozen')
  assert.equal(bound.view.providerRun, 'run-A')
  assert.equal(bound.view.probe.probeId, admitted.view.probe.probeId)
  assert.equal(bound.view.probe.cutoff, 2)

  const peeked = RecoveryScope.peekAttempt(scope, 'ses-frozen', 'run-A')
  assert.equal(peeked.found, true)
  assert.deepEqual(peeked.view.probe, bound.view.probe)

  const later = buildPlan({
    session: 'ses-frozen',
    physical: 'phys-frozen',
    probe: probeInput({ probeId: 'probe-later', candidate: candidate({ cutoff: 5 }) }),
  })
  const refused = RecoveryScope.freezeAttemptPlan(scope, 'ses-frozen', 'phys-frozen', later.handle)
  assert.equal(refused.outcome, 'PlanConflict')

  const retained = RecoveryScope.peekAttempt(scope, 'ses-frozen', 'run-A')
  assert.equal(retained.found, true)
  assert.equal(retained.view.probe.probeId, 'probe-1')
  assert.equal(retained.view.probe.cutoff, 2)
})

test('WHAT[PAR-011] bind to the wrong parent or run is refused', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const plan = buildPlan({ session: 'ses-bind', physical: 'phys-parent-1' })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-bind', 'phys-parent-1', plan.handle).outcome, 'Admitted')

  const bound = RecoveryScope.bindAttempt(scope, 'ses-bind', 'phys-parent-1', 'run-A')
  assert.equal(bound.bound, true)
  assert.equal(bound.view.providerRun, 'run-A')

  const wrongParent = RecoveryScope.bindAttempt(scope, 'ses-bind', 'phys-parent-2', 'run-A')
  assert.equal(wrongParent.bound, false)
  assert.equal(wrongParent.view, null)
  assert.equal(wrongParent.handle, null)

  const other = buildPlan({ session: 'ses-bind', physical: 'phys-parent-2' })
  const rebound = XWireSurface.bindProviderRun(other.handle, 'run-A')
  assert.throws(
    () => RecoveryScope.recordBound(scope, 'ses-bind', 'run-A', rebound.handle),
    /HOST-BOUNDARY-008/,
  )
})

test('WHAT[PAR-011] terminal consumption is single-shot and never re-promotes', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const plan = buildPlan({
    session: 'ses-term',
    physical: 'phys-term',
    probe: probeInput({ probeId: 'probe-term' }),
  })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-term', 'phys-term', plan.handle).outcome, 'Admitted')
  assert.equal(RecoveryScope.bindAttempt(scope, 'ses-term', 'phys-term', 'run-term').bound, true)

  const first = RecoveryScope.consumeAttempt(scope, 'ses-term', 'run-term')
  assert.equal(first.consumed, true)
  assert.equal(first.view.providerRun, 'run-term')
  assert.equal(first.view.probe.probeId, 'probe-term')

  assert.deepEqual(RecoveryScope.peekAttempt(scope, 'ses-term', 'run-term'), {
    found: false,
    view: null,
  })
  assert.deepEqual(RecoveryScope.consumeAttempt(scope, 'ses-term', 'run-term'), {
    consumed: false,
    view: null,
  })
})

test('WHAT[PAR-011] freeze under the wrong session key is an identity mismatch', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const plan = buildPlan({})
  assert.equal(plan.view.session, 'ses-1')
  assert.equal(plan.view.agent, 'coder')
  assert.equal(plan.view.role, 'coder')

  const mismatch = RecoveryScope.freezeAttemptPlan(scope, 'ses-other', 'phys-1', plan.handle)
  assert.equal(mismatch.outcome, 'IdentityMismatch')
  assert.equal(mismatch.expected.session, 'ses-other')
  assert.equal(mismatch.expected.physical, 'phys-1')
  assert.equal(mismatch.attempted.session, 'ses-1')
  assert.equal(mismatch.attempted.physical, 'phys-1')

  assert.throws(
    () => RecoveryScope.recordAttemptPlan(scope, 'ses-other', 'phys-1', plan.handle),
    /HOST-BOUNDARY-008/,
  )

  const admitted = RecoveryScope.freezeAttemptPlan(scope, 'ses-1', 'phys-1', plan.handle)
  assert.equal(admitted.outcome, 'Admitted')
  assert.equal(admitted.view.session, 'ses-1')
  assert.equal(admitted.view.physical, 'phys-1')
})

test('WHAT[PAR-011] freeze under the wrong physical key is an identity mismatch', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const plan = buildPlan({ session: 'ses-phys', physical: 'phys-exact' })

  const mismatch = RecoveryScope.freezeAttemptPlan(scope, 'ses-phys', 'phys-other', plan.handle)
  assert.equal(mismatch.outcome, 'IdentityMismatch')
  assert.equal(mismatch.expected.session, 'ses-phys')
  assert.equal(mismatch.expected.physical, 'phys-other')
  assert.equal(mismatch.attempted.physical, 'phys-exact')

  assert.throws(
    () => RecoveryScope.recordAttemptPlan(scope, 'ses-phys', 'phys-other', plan.handle),
    /HOST-BOUNDARY-008/,
  )

  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-phys', 'phys-exact', plan.handle).outcome, 'Admitted')
})

test('WHAT[PAR-011] same physical replay after binding returns the admitted plan', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const plan = buildPlan({
    session: 'ses-replay',
    physical: 'phys-replay',
    probe: probeInput({ probeId: 'probe-replay' }),
  })
  const admitted = RecoveryScope.freezeAttemptPlan(scope, 'ses-replay', 'phys-replay', plan.handle)
  assert.equal(admitted.outcome, 'Admitted')

  const bound = RecoveryScope.bindAttempt(scope, 'ses-replay', 'phys-replay', 'run-replay')
  assert.equal(bound.bound, true)
  assert.equal(bound.view.session, 'ses-replay')
  assert.equal(bound.view.physical, 'phys-replay')
  assert.equal(bound.view.providerRun, 'run-replay')
  assert.equal(bound.view.agent, admitted.view.agent)
  assert.equal(bound.view.role, admitted.view.role)

  const replayed = RecoveryScope.freezeAttemptPlan(scope, 'ses-replay', 'phys-replay', plan.handle)
  assert.equal(replayed.outcome, 'ReplayedExisting')
  assert.deepEqual(replayed.view, admitted.view)

  const retained = RecoveryScope.peekAttempt(scope, 'ses-replay', 'run-replay')
  assert.equal(retained.found, true)
  assert.deepEqual(retained.view, bound.view)

  const rebound = RecoveryScope.bindAttempt(scope, 'ses-replay', 'phys-replay', 'run-replay')
  assert.equal(rebound.bound, true)
  assert.deepEqual(rebound.view, bound.view)
})

test('WHAT[PAR-011] same run and root with a different participant conflicts', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const coderPlan = buildPlan({
    session: 'ses-who',
    logicalRun: 'run-same',
    root: 'root-same',
    physical: 'phys-who',
  })
  assert.equal(coderPlan.view.agent, 'coder')
  assert.equal(coderPlan.view.role, 'coder')
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-who', 'phys-who', coderPlan.handle).outcome, 'Admitted')

  const inspectorPlan = buildPlan({
    session: 'ses-who',
    logicalRun: 'run-same',
    root: 'root-same',
    physical: 'phys-who',
    agent: 'inspector',
  })
  assert.equal(inspectorPlan.ok, true)
  assert.equal(inspectorPlan.view.agent, 'inspector')
  assert.equal(inspectorPlan.view.role, 'inspector')
  assert.equal(inspectorPlan.view.logicalRunId, 'run-same')
  assert.equal(inspectorPlan.view.root, 'root-same')

  const conflict = RecoveryScope.freezeAttemptPlan(scope, 'ses-who', 'phys-who', inspectorPlan.handle)
  assert.equal(conflict.outcome, 'PlanConflict')
  assert.equal(conflict.existing.agent, 'coder')
  assert.equal(conflict.attempted.agent, 'inspector')
  assert.equal(conflict.existing.logicalRunId, 'run-same')
  assert.equal(conflict.attempted.logicalRunId, 'run-same')

  assert.throws(
    () => RecoveryScope.recordAttemptPlan(scope, 'ses-who', 'phys-who', inspectorPlan.handle),
    /HOST-BOUNDARY-008/,
  )

  const bound = RecoveryScope.bindAttempt(scope, 'ses-who', 'phys-who', 'run-who')
  assert.equal(bound.bound, true)
  assert.equal(bound.view.agent, 'coder')
})

test('WHAT[PAR-011] same run and root with a different role conflicts', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const coderPlan = buildPlan({
    session: 'ses-role',
    logicalRun: 'run-role',
    root: 'root-role',
    physical: 'phys-role',
  })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-role', 'phys-role', coderPlan.handle).outcome, 'Admitted')

  const devopsPlan = buildPlan({
    session: 'ses-role',
    logicalRun: 'run-role',
    root: 'root-role',
    physical: 'phys-role',
    role: 'devops',
  })
  assert.equal(devopsPlan.view.role, 'devops')
  const conflict = RecoveryScope.freezeAttemptPlan(scope, 'ses-role', 'phys-role', devopsPlan.handle)
  assert.equal(conflict.outcome, 'PlanConflict')
  assert.equal(conflict.existing.role, 'coder')
  assert.equal(conflict.attempted.role, 'devops')
})

test('WHAT[PAR-011] recordBound rejects a conflicting session, run, or physical parent', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const plan = buildPlan({ session: 'ses-rec', physical: 'phys-rec' })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-rec', 'phys-rec', plan.handle).outcome, 'Admitted')
  const bound = RecoveryScope.bindAttempt(scope, 'ses-rec', 'phys-rec', 'run-good')
  assert.equal(bound.bound, true)

  assert.throws(() => RecoveryScope.recordBound(scope, 'ses-wrong', 'run-good', bound.handle), /HOST-BOUNDARY-008/)
  assert.throws(() => RecoveryScope.recordBound(scope, 'ses-rec', 'run-wrong', bound.handle), /HOST-BOUNDARY-008/)

  const other = buildPlan({ session: 'ses-rec', physical: 'phys-other' })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-rec', 'phys-other', other.handle).outcome, 'Admitted')
  const rebound = XWireSurface.bindProviderRun(other.handle, 'run-good')
  assert.equal(rebound.view.physical, 'phys-other')
  assert.throws(() => RecoveryScope.recordBound(scope, 'ses-rec', 'run-good', rebound.handle), /HOST-BOUNDARY-008/)

  assert.doesNotThrow(() => RecoveryScope.recordBound(scope, 'ses-rec', 'run-good', bound.handle))
  const peeked = RecoveryScope.peekAttempt(scope, 'ses-rec', 'run-good')
  assert.equal(peeked.found, true)
  assert.equal(peeked.view.physical, 'phys-rec')
})

test('WHAT[PAR-011] same candidate probe id with a different digest conflicts', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const first = buildPlan({
    session: 'ses-digest',
    physical: 'phys-digest',
    probe: probeInput({ probeId: 'probe-same', candidate: candidate({ frozenDigest: 'sha256:aaa' }) }),
  })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-digest', 'phys-digest', first.handle).outcome, 'Admitted')

  const second = buildPlan({
    session: 'ses-digest',
    physical: 'phys-digest',
    probe: probeInput({ probeId: 'probe-same', candidate: candidate({ frozenDigest: 'sha256:bbb' }) }),
  })
  const conflict = RecoveryScope.freezeAttemptPlan(scope, 'ses-digest', 'phys-digest', second.handle)
  assert.equal(conflict.outcome, 'PlanConflict')
  assert.equal(conflict.existing.probe.frozenDigest, 'sha256:aaa')
  assert.equal(conflict.attempted.probe.frozenDigest, 'sha256:bbb')
})

test('WHAT[PAR-011] terminal consume clears both registries so a fresh physical plan may freeze', () => {
  const scope = RecoveryScope.createRecoveryScope()
  const first = buildPlan({
    session: 'ses-clear',
    physical: 'phys-clear',
    probe: probeInput({ probeId: 'probe-old' }),
  })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-clear', 'phys-clear', first.handle).outcome, 'Admitted')
  assert.equal(RecoveryScope.bindAttempt(scope, 'ses-clear', 'phys-clear', 'run-old').bound, true)

  const consumed = RecoveryScope.consumeAttempt(scope, 'ses-clear', 'run-old')
  assert.equal(consumed.consumed, true)
  assert.equal(consumed.view.probe.probeId, 'probe-old')
  assert.deepEqual(RecoveryScope.peekAttempt(scope, 'ses-clear', 'run-old'), { found: false, view: null })

  const fresh = buildPlan({
    session: 'ses-clear',
    physical: 'phys-clear',
    probe: probeInput({ probeId: 'probe-new' }),
  })
  const admitted = RecoveryScope.freezeAttemptPlan(scope, 'ses-clear', 'phys-clear', fresh.handle)
  assert.equal(admitted.outcome, 'Admitted')
  assert.equal(admitted.view.probe.probeId, 'probe-new')

  const rebound = RecoveryScope.bindAttempt(scope, 'ses-clear', 'phys-clear', 'run-new')
  assert.equal(rebound.bound, true)
  assert.equal(rebound.view.probe.probeId, 'probe-new')
  assert.equal(rebound.view.providerRun, 'run-new')
})

test('WHAT[PAR-020] recovery holds manual-only ownership and publishes no background resume', () => {
  const scope = RecoveryScope.createRecoveryScope()
  assert.deepEqual(RecoveryScope.recoveryOwnership(scope), { manuals: 0 })

  const plan = buildPlan({ session: 'ses-r10', physical: 'phys-r10' })
  assert.equal(RecoveryScope.freezeAttemptPlan(scope, 'ses-r10', 'phys-r10', plan.handle).outcome, 'Admitted')
  assert.equal(RecoveryScope.bindAttempt(scope, 'ses-r10', 'phys-r10', 'run-r10').bound, true)
  assert.equal(RecoveryScope.consumeAttempt(scope, 'ses-r10', 'run-r10').consumed, true)

  assert.deepEqual(RecoveryScope.recoveryOwnership(scope), { manuals: 0 })
})
