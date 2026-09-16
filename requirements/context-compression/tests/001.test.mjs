import assert from 'node:assert/strict'
import test from 'node:test'
import * as forbidden from '../../../dist/Context/Companion/ForbiddenCapacitySurface.js'
import * as companion from '../../../dist/Context/Companion/CompanionSurface.js'

test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_context_compression_owner_never_observes_forbidden_capacity_synonyms', () => {
  assert.equal(forbidden.checkSourceCode(), true)
})

test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_the_only_allowed_byte_metric_is_the_delta_input_contract', () => {
  assert.equal(forbidden.deltaInputContractBytes, 200 * 1024)
})

test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_no_prompt_carries_a_token_count_or_output_budget', () => {
  const doc = companion.renderInstructionDoc({ hasFrames: false })
  assert.doesNotMatch(doc, /token count|output budget|token budget/i)
})
