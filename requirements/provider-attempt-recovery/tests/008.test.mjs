import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as attemptPurpose from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const planner = compression.attemptPlanner

const { budget, providerFailureProjection } = failureOwner

const TOOL_CAPABILITIES = [
  'BashHoneypot',
  'Edit',
  'Fetch',
  'Fission',
  'Glob',
  'Grep',
  'Move',
  'Read',
  'Remove',
  'Sphinx',
  'Write',
]

test('WHAT[provider-attempt-recovery-008] an_invalid_terminal_earns_at_most_one_repair_and_never_advances', () => {
  // Empty and XML-only terminals are unusable content (production
  // TerminalValidity), not provider failures: the budget has no input for
  // terminal text, so recording nothing is structural.
  assert.deepEqual(compression.terminalValidity(''), { valid: false, rejection: 'Empty' })
  assert.deepEqual(compression.terminalValidity('<tool_call>do</tool_call>'), {
    valid: false,
    rejection: 'XmlOnly',
  })
  assert.deepEqual(compression.terminalValidity('a real answer'), { valid: true, rejection: null })

  assert.equal(budget.recordFailure.length, 1, 'budget advance takes only the budget, never terminal text')
})

test('WHAT[provider-attempt-recovery-008] an_errored_attempt_with_unusable_content_never_mints_a_provider_terminal', () => {
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
const { readFile } = await import("node:fs/promises");
const turns = await import("../../../dist/Interaction/Repair/CompletedTurnSurface.js");

const text = (value) => ({ type: 'text', text: value })
const reasoning = (value) => ({ type: 'reasoning', text: value })
const toolCall = (callID, tool, args) => ({ type: 'tool-call', callID, tool, args })
const toolResult = (callID, result) => ({ type: 'tool-result', callID, result })
const activity = (kind) => ({ type: kind })
const classify = (completed, finish, errorName, parts = []) => turns.classifyOutcome(completed, finish, errorName, parts)

test('WHAT[provider-attempt-recovery-008] RECON_formal_content_gate_is_shared_with_terminal_validity', () => {
  assert.equal(turns.formalContentUnusable(null), true)
  assert.equal(turns.formalContentUnusable([]), true)
  assert.equal(turns.formalContentUnusable([reasoning('only thoughts')]), true)
  assert.equal(turns.formalContentUnusable([text('   ')]), true)
  assert.equal(turns.formalContentUnusable([text('<tool_call>read</tool_call>')]), true)
  assert.equal(turns.formalContentUnusable([text('a real answer')]), false)
  assert.equal(turns.formalContentUnusable([text('a real answer'), reasoning('and thinking')]), false)
})

test('WHAT[provider-attempt-recovery-008] an_unfinished_manager_turn_reaches_bounded_interaction_repair', async () => {
  const managerSource = await readFile(
    new URL('../../../src/Wanxiangshu/Mission/Manager/Workflow.fs', import.meta.url),
    'utf8',
  )

  // A TurnNeedsContinuation without a failure witness is exactly the bounded
  // Interaction Repair this clause licenses — for the Manager role too. The
  // in-progress turn stays with the Host loop, but the unfinished turn must
  // never be dropped in silence.
  assert.match(
    managerSource,
    /\|\s*false, None, ReconcileProgram\.TurnInProgress\s*->\s*Task\.FromResult\(\)[\s\S]{0,400}?\|\s*false, None, ReconcileProgram\.TurnNeedsContinuation _\s*->\s*observeOrdinary context/,
  )
})
}
