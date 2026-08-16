// behavior-diagnosis: canonical single-call cycle domain.
// Provider cardinality (exactly one chronicle) is proven by enforcer-cycle-protocol.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcer } from '../../verification-system/tests/support/domain.mjs'

const tip = () => enforcer.fieldNames()[0]
const call = (text, evidence) => ({
  text,
  tipField: tip(),
  ...(evidence === undefined ? {} : { evidence }),
})

test('WHAT[BD-009] ENFORCER_042_domain_has_no_multi_call_merge_surface', () => {
  assert.equal(enforcer.mergeCalls, undefined)
})

test('WHAT[BD-009] ENFORCER_025_single_call_preserves_canonical_tip_text_and_evidence', () => {
  const field = tip()
  const rule = enforcer.tryFindByField(field)
  const cycle = enforcer.canonicalCycle({ text: 'observation', tipField: field, evidence: 'evidence' })

  assert.equal(cycle.mergedText, 'observation')
  assert.equal(cycle.mergedEvidence, 'evidence')
  assert.deepEqual(cycle.tip, {
    ruleId: rule.ruleId,
    fieldName: rule.fieldName,
    lexicalOrder: rule.lexicalOrder,
  })
})

test('WHAT[BD-010] ENFORCER_043_valid_cycle_requires_nonempty_text', () => {
  assert.equal(enforcer.isValidCycle(enforcer.canonicalCycle(call('content'))), true)
  assert.equal(enforcer.isValidCycle(enforcer.canonicalCycle(call('   '))), false)
})
