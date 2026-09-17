import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const planner = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const xwire = await import("../../../dist/Context/Prefix/XWireSurface.js");
const { budget } = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const requestKind = prefix.requestKind
const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })
const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => ({
  probeId: id,
  basedOnEpoch: 0,
  candidate: snapshotAt(cutoff),
})

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_a_probe_denied_policy_never_asks_for_a_probe', () => {
  // `selectProbe` throws if called. A denied policy must not pay for a
  // digest recomputation or a blob read to discover it has nothing to do.
  const plan = planner.attemptPlan({ kind: requestKind.workMain, mayRecover: false })

  assert.equal(plan.choice, 'UseCommittedEpoch')
  assert.equal(plan.probeId, null)
  assert.equal(plan.noProbeReason, null, 'not asking is not a refusal')
})
test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_a_companion_request_never_asks_for_a_probe_even_when_allowed', () => {
  // Enforced in the planner, not left to the caller. A Companion request has no prefix
  // to probe — its history is the frame sequence — and a repair reuses whatever the
  // attempt it repairs already sent.
  for (const kind of [
    requestKind.bloggerMain,
    requestKind.bloggerSquash,
    requestKind.interactionRepair,
    requestKind.strengthReplica,
  ]) {
    const plan = planner.attemptPlan({ kind, mayRecover: true })

    assert.equal(plan.choice, 'UseCommittedEpoch', `${kind} must not carry a probe`)
    assert.equal(plan.probeId, null)
  }
})
test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_an_allowed_work_main_carries_the_probe_it_selected', () => {
  const plan = planner.attemptPlan({
    kind: requestKind.workMain,
    mayRecover: true,
    probe: probeFor({ id: 'probe-abc' }),
  })

  assert.equal(plan.choice, 'UsePrefixProbe')
  assert.equal(plan.probeId, 'probe-abc')
  assert.equal(plan.noProbeReason, null)
})
test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_invalid_role_and_kind_fail_closed', () => {
  // CompressionSurface.attemptPlanCore validates only role+kind; tier was dropped
  // from the surface (participantIdentityToJs pins selectedTier:"deep"), so a
  // stray tier field is ignored rather than rejected.
  for (const input of [{ role: 'unknown' }, { kind: 'unknown' }]) {
    const result = planner.attemptPlan(input)
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|request kind)/)
  }
  const tierIgnored = planner.attemptPlan({ tier: 'unknown' })
  assert.equal(tierIgnored.choice, 'UseCommittedEpoch')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const requestKind = prefix.requestKind
const budget = failureOwner.budget

test('WHAT[CONTEXT-COMPRESSION-008] only work_main requests may carry prefix probe', () => {
  assert.equal(requestKind.mayCarryProbe(requestKind.workMain), true)
  assert.equal(requestKind.mayCarryProbe(requestKind.bloggerMain), false)
  assert.equal(requestKind.mayCarryProbe(requestKind.bloggerSquash), false)
  assert.equal(requestKind.mayCarryProbe(requestKind.interactionRepair), false)
})
}
