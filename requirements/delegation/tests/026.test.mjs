import assert from 'node:assert/strict'
import test from 'node:test'
import * as rwu from '../../../dist/Execution/Delegation/ReusableWorkUnitSurface.js'
import * as forkTool from '../../../dist/Execution/Delegation/ForkToolSurface.js'

test('WHAT[DELEG-026] reusable delegation has no durable program-counter/state-machine vocabulary', () => {
  assert.equal(rwu.hasDurableProgramCounter(), false)
})

test('WHAT[DELEG-026] FORK_TOOL_acceptance_unknown_never_claims_charge_was_not_placed', () => {
  const res = forkTool.handleUnknownAcceptance()
  assert.equal(res.status, 'UnknownOutcome')
  assert.doesNotMatch(res.message, /not placed/)
})
