import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const planner = compression.attemptPlanner

test('WHAT[provider-attempt-recovery-008] terminal validity distinguishes unusable content from an ordinary answer', () => {
  assert.deepEqual(compression.terminalValidity(''), { valid: false, rejection: 'Empty' })
  assert.deepEqual(compression.terminalValidity('<tool_call>do</tool_call>'), {
    valid: false,
    rejection: 'XmlOnly',
  })
  assert.deepEqual(compression.terminalValidity('a real answer'), { valid: true, rejection: null })

})

test('WHAT[provider-attempt-recovery-008] unusable content alone cannot substitute for a confirmed provider failure', () => {
  // A confirmed provider class still terminalizes the attempt; the policy owns
  // whether that becomes a licensed retry or a terminal.
  assert.equal(reconcile.failureWitnessMintsTerminal('ProviderTransient', false), true)
  assert.equal(reconcile.failureWitnessMintsTerminal('ProviderPermanent', false), true)

  // No confirmed provider class plus unusable formal content: the witness must
  // not mint TurnFailed — the turn stays with bounded Interaction Repair.
  for (const label of [
    'ProtocolRejection',
    'LocalInvariant',
    'StreamInterruptedAfterFirstToken',
    'AuthorizationDenied',
    'UserCancelled',
    'Superseded',
  ]) {
    assert.equal(reconcile.failureWitnessMintsTerminal(label, false), false, label)
    assert.equal(reconcile.failureWitnessMintsTerminal(label, true), true, label)
  }
})

test('WHAT[provider-attempt-recovery-008] only_a_probe_attempt_with_a_usable_terminal_may_promote', () => {
  const withProbe = planner.plan({
    role: 'engineer',
    kind: 'work-main',
    policyAllowsProbe: true,
    probe: {
      probeId: 'probe-p1',
      basedOnEpoch: 0,
      candidate: {
        ref: 'blob-frozen-5',
        frozenDigest: 'frozen-5',
        cutoff: 5,
        prefixDigest: 'prefix-5',
        sealRoot: 'seal-5',
        syntheticId: 'synthetic-seal-5',
      },
    },
  })

  assert.equal(planner.promotableProbeId(withProbe, 'Completed'), 'probe-p1')
  assert.equal(planner.promotableProbeId(withProbe, 'CompletedInvalid'), null)
  assert.equal(planner.promotableProbeId(withProbe, 'Failed'), null)
  assert.equal(planner.promotableProbeId(withProbe, 'Aborted'), null)
})

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const turns = await import("../../../dist/Interaction/Repair/CompletedTurnSurface.js");

const text = (value) => ({ type: 'text', text: value })
const reasoning = (value) => ({ type: 'reasoning', text: value })

test('WHAT[provider-attempt-recovery-008] RECON_formal_content_gate_is_shared_with_terminal_validity', () => {
  assert.equal(turns.formalContentUnusable(null), true)
  assert.equal(turns.formalContentUnusable([]), true)
  assert.equal(turns.formalContentUnusable([reasoning('only thoughts')]), true)
  assert.equal(turns.formalContentUnusable([text('   ')]), true)
  assert.equal(turns.formalContentUnusable([text('<tool_call>read</tool_call>')]), true)
  assert.equal(turns.formalContentUnusable([text('a real answer')]), false)
  assert.equal(turns.formalContentUnusable([text('a real answer'), reasoning('and thinking')]), false)
})

test.todo('WHAT[provider-attempt-recovery-008] actual unfinished Manager and other repairable roles reach one repair without spending provider budget (GAP-139)')
}
