// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: provider-attempt-recovery.
//
// AttemptExecutionProfile (SPLIT@cutover note in context-compression PROOF):
// the cursor is the only thing that moves the effective agent, and promotion is
// gated on a probe attempt with a usable terminal.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  attemptPlanner as planner,
  cursor,
  okResult,
  prefixEpochProjection as prefix,
  prefixProbe,
  requestKind,
} from '../../verification-system/tests/support/domain.mjs'

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

test('FALLBACK_002_the_cursor_is_the_only_thing_that_moves_the_effective_agent', () => {
  const at = (offset) =>
    planner.plan({ cursor: cursor.atOffset(offset), kind: requestKind.workMain }).effectiveAgent

  // A/A′ take the selected side, B/B′ the peer. The authority profile is identical in
  // all four; only the cursor differs.
  assert.deepEqual([0, 1, 2, 3].map(at), ['fast-coder', 'fast-coder', 'deep-coder', 'deep-coder'])
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
