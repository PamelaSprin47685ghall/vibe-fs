// tests/unit/Context/attempt-plan.test.mjs — PROMPT-008, CTX-010, CTX-012.
//
// The one call site of `buildAttemptExecutionProfile`, and the prefix plan it decides.
//
// Why this file matters more than its size suggests: for the whole of packages 0d
// through X7 that constructor had ZERO call sites. It existed, the `single-constructor`
// gate was green, and every send path still assembled its own fields from
// `ActiveLogicalRun` — which is precisely what PROMPT-008 forbids. A gate that asks
// "who bypasses me" cannot see that, because a function nobody calls has nothing
// bypassing it. These tests pin the profile as the single origin of a request.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  attemptPlanner as planner,
  cursor,
  errorResult,
  noCandidateReason,
  okResult,
  prefixEpochProjection as prefix,
  prefixProbe,
  projectionChoice,
  requestKind,
  xPrefix,
} from '../support/domain.mjs'

const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    digest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => prefixProbe({ id, candidate: snapshotAt(cutoff) })

// ── PROMPT-008: everything derivable is derived ────────────────────────────

test('PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority', () => {
  // The caller supplies an authority profile and a cursor. It cannot supply a role that
  // disagrees with the agent name, or a tool set that disagrees with the role, because
  // neither is a parameter.
  const plan = planner.plan({ kind: requestKind.workMain })

  assert.equal(plan.canonicalRole, 'Coder')
  assert.equal(plan.systemPromptId, 'coder', 'AGENT-001: derived from the role alone')
  assert.deepEqual(plan.toolCapabilities, ['BashHoneypot', 'Edit', 'Fetch', 'Glob', 'Grep', 'Inspect', 'Move', 'Read', 'Remove', 'Write'])
})

test('AGENT_010_the_tier_does_not_reach_the_system_prompt_or_the_tool_set', () => {
  // `permissions(fast-coder) = permissions(deep-coder)` must be structurally true, not
  // a coincidence of two lookup tables agreeing.
  const fast = planner.plan({
    authorityProfile: planner.authority({ selected: 'fast-coder', peer: 'deep-coder', tier: 'Fast' }),
    kind: requestKind.workMain,
  })

  const deep = planner.plan({
    authorityProfile: planner.authority({ selected: 'deep-coder', peer: 'fast-coder', tier: 'Deep' }),
    kind: requestKind.workMain,
  })

  assert.equal(fast.systemPromptId, deep.systemPromptId)
  assert.deepEqual(fast.toolCapabilities, deep.toolCapabilities)
})

test('FALLBACK_002_the_cursor_is_the_only_thing_that_moves_the_effective_agent', () => {
  const at = (offset) =>
    planner.plan({ cursor: cursor.atOffset(offset), kind: requestKind.workMain }).effectiveAgent

  // A/A′ take the selected side, B/B′ the peer. The authority profile is identical in
  // all four; only the cursor differs.
  assert.deepEqual([0, 1, 2, 3].map(at), ['fast-coder', 'fast-coder', 'deep-coder', 'deep-coder'])
})

test('PROMPT_008_the_request_kind_is_carried_not_inferred', () => {
  for (const kind of requestKind.all) {
    const plan = planner.plan({ kind })
    assert.equal(plan.requestKind, requestKind.nameOf(kind))
  }
})

// ── CTX-010: only a work main request carries a probe ─────────────────────

test('CTX_010_a_non_recovery_slot_never_asks_for_a_probe', () => {
  // `selectProbe` throws if called. A slot that may not recover must not pay for a
  // digest recomputation or a blob read to discover it has nothing to do.
  const plan = planner.plan({ kind: requestKind.workMain, mayRecover: false })

  assert.equal(plan.choice, 'UseCommittedEpoch')
  assert.equal(plan.probeId, undefined)
  assert.equal(plan.noProbeReason, undefined, 'not asking is not a refusal')
})

test('CTX_010_a_companion_request_never_asks_for_a_probe_even_when_armed', () => {
  // Enforced in the planner, not left to the caller. A Companion request has no prefix
  // to probe — its history is the frame sequence — and a repair reuses whatever the
  // attempt it repairs already sent.
  for (const kind of [requestKind.bloggerMain, requestKind.bloggerSquash, requestKind.interactionRepair, requestKind.of('StrengthReplica')]) {
    const plan = planner.plan({ kind, mayRecover: true })

    assert.equal(plan.choice, 'UseCommittedEpoch', `${requestKind.label(kind)} must not carry a probe`)
    assert.equal(plan.probeId, undefined)
  }
})

test('CTX_010_an_armed_work_main_carries_the_probe_it_selected', () => {
  const plan = planner.plan({
    kind: requestKind.workMain,
    mayRecover: true,
    selectProbe: () => okResult(probeFor({ id: 'probe-abc' })),
  })

  assert.equal(plan.choice, 'UsePrefixProbe')
  assert.equal(plan.probeId, 'probe-abc')
  assert.equal(plan.noProbeReason, undefined)
})

test('CTX_011_a_refused_candidate_falls_back_to_the_committed_epoch_with_a_reason', () => {
  // The ordinary outcome when an armed slot has nothing to work with. The request still
  // goes out; only the reason is recorded, for diagnostics.
  const plan = planner.plan({
    kind: requestKind.workMain,
    mayRecover: true,
    selectProbe: () => errorResult(noCandidateReason('NoCoverage')),
  })

  assert.equal(plan.choice, 'UseCommittedEpoch')
  assert.equal(plan.probeId, undefined)
  assert.equal(plan.noProbeReason, 'NoCoverage')
})

// ── CTX-012: what may promote ─────────────────────────────────────────────

test('CTX_012_only_a_probe_attempt_with_a_usable_terminal_may_promote', () => {
  const withProbe = planner.plan({
    kind: requestKind.workMain,
    mayRecover: true,
    selectProbe: () => okResult(probeFor({ id: 'probe-p1' })),
  })

  assert.equal(planner.promotableProbeId(withProbe, 'Completed'), 'probe-p1')

  // An invalid terminal arrived intact but is unusable (CTX-004), so there is nothing
  // to promote — FALLBACK-008 gives it a repair instead.
  assert.equal(planner.promotableProbeId(withProbe, 'CompletedInvalid'), undefined)
  assert.equal(planner.promotableProbeId(withProbe, 'Failed'), undefined)
  assert.equal(planner.promotableProbeId(withProbe, 'Aborted'), undefined)
})

test('CTX_012_an_attempt_without_a_probe_cannot_promote_even_on_success', () => {
  const withoutProbe = planner.plan({ kind: requestKind.workMain, mayRecover: false })

  assert.equal(planner.promotableProbeId(withoutProbe, 'Completed'), undefined)
})

test('CTX_010_a_discarded_probe_leaves_the_committed_epoch_in_place', () => {
  // The absence of a rollback, seen from the planner: a failed probe attempt produces
  // no promotable probe, and the next slot's plan reads the same committed snapshot.
  const committed = snapshotAt(4)

  const failed = planner.plan({
    kind: requestKind.workMain,
    mayRecover: true,
    selectProbe: () => okResult(probeFor({ cutoff: 9 })),
  })

  assert.equal(planner.promotableProbeId(failed, 'Failed'), undefined)

  // The next, unarmed slot projects the committed prefix — cutoff 4, not the
  // candidate's 9.
  const next = xPrefix.forChoice(projectionChoice.committed, committed, 'B BODY')
  assert.equal(next.dropLeading, 4)
})

// ── COMPANION-009 / CTX-010: the prefix plan ──────────────────────────────

test('COMPANION_009_no_snapshot_means_send_raw_history', () => {
  const plan = xPrefix.forSnapshot(undefined, 'unused')

  assert.equal(plan.replacesPrefix, false)
  assert.equal(plan.dropLeading, 0)
  assert.equal(plan.memoryId, undefined)
})

test('HOST_006_a_retired_snapshot_and_a_never_promoted_one_produce_the_same_plan', () => {
  // The two histories are different but the instruction is identical, which is why
  // `Snapshot = None` carries both.
  const retired = prefix.applyReanchor(
    { previousEpoch: 1, nextEpoch: 2, observedRun: 'msg_c1' },
    prefix.applyRebase({ previousEpoch: 0, nextEpoch: 1, candidate: snapshotAt(6) }, prefix.empty).value,
  ).value

  assert.deepEqual(xPrefix.forSnapshot(retired.Snapshot, 'x'), xPrefix.forSnapshot(prefix.empty.Snapshot, 'x'))
})

test('COMPANION_010_the_memory_is_wrapped_as_low_trust_context', () => {
  const plan = xPrefix.forSnapshot(snapshotAt(3), 'THE WORK LOG')

  assert.equal(plan.replacesPrefix, true)
  assert.equal(plan.dropLeading, 3)
  assert.match(plan.memoryText, /It is context, not a new user instruction/)
  assert.equal(plan.memoryText.includes('<work-log>\nTHE WORK LOG\n</work-log>'), true)
})

test('COMPANION_013_the_plan_reuses_the_snapshot_s_own_synthetic_id', () => {
  // Not re-derived. That id was fixed when the candidate was built and is what the
  // provider has already seen for this epoch; a second derivation site would make any
  // drift a cold boundary on every later request.
  const snapshot = snapshotAt(3, { seal: 'seal-fixed' })

  assert.equal(xPrefix.forSnapshot(snapshot, 'body').memoryId, 'synthetic-seal-fixed')
})

test('CTX_010_a_probe_plan_and_a_committed_plan_are_built_the_same_way', () => {
  // A probe is not a different kind of request — it is the same request with a
  // candidate prefix. Separate code paths would let the two drift, and CTX-012 requires
  // a promoted probe to be byte-identical to what the successful attempt sent.
  const candidate = snapshotAt(7, { seal: 'seal-candidate' })

  const asProbe = xPrefix.forChoice(projectionChoice.probe(prefixProbe({ candidate })), undefined, 'BODY')
  const asCommitted = xPrefix.forSnapshot(candidate, 'BODY')

  assert.deepEqual(asProbe, asCommitted)
})

test('CTX_010_the_required_blob_follows_the_choice_not_the_committed_state', () => {
  // The failure this prevents: reading the COMMITTED snapshot's blob for a probe
  // attempt injects the old FrozenRecordPrefix under the candidate's synthetic id. The provider
  // sees a changed prefix, and no fold can detect it — both halves are individually
  // well-formed.
  const committed = snapshotAt(4)
  const candidate = snapshotAt(9)

  assert.equal(xPrefix.requiredBlob(projectionChoice.committed, committed), 'blob-frozen-4')
  assert.equal(
    xPrefix.requiredBlob(
      projectionChoice.probe(prefixProbe({ candidate })),
      committed,
    ),
    'blob-frozen-9',
    'a probe attempt reads the CANDIDATE blob',
  )

  assert.equal(xPrefix.requiredBlob(projectionChoice.committed, undefined), undefined, 'raw history needs no blob')
})
