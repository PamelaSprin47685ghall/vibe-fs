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
