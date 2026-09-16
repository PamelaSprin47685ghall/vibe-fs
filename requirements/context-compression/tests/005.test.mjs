import assert from 'node:assert/strict'
import test from 'node:test'
import * as hostCompaction from '../../../dist/Context/Companion/HostCompactionPolicySurface.js'
import * as retryPolicy from '../../../dist/Context/Companion/RetryPolicySurface.js'
import * as term from '../../../dist/Context/Companion/TerminalValiditySurface.js'

test('WHAT[CONTEXT-COMPRESSION-005] CTX_005_containment_does_not_discriminate_by_source', () => {
  assert.equal(hostCompaction.containmentSourceIndifferent, true)
})

test('WHAT[CONTEXT-COMPRESSION-005] every recorded failure consumes exactly one budget unit', () => {
  assert.equal(retryPolicy.failureBudgetIncrement, 1)
})

test('WHAT[CONTEXT-COMPRESSION-005] CTX_005_validity_does_not_depend_on_failure_cause', () => {
  assert.equal(term.validityIgnoresFailureCause, true)
})
