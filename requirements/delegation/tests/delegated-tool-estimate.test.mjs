import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import { ToolCallIdModule_create as toolCallId } from '../../../dist/Kernel/Identity.js'

const {
  DelegatedToolEstimateProjection_replace: replace,
  DelegatedToolEstimateProjection_observe: observe,
  DelegatedToolEstimateProjection_remaining: remaining,
  DelegatedToolEstimateProjection_countedCallCount: countedCallCount,
} = await import('../../../dist/Execution/Delegation/DelegatedToolEstimateProjection.js')

test('DELEG_022_replace_sets_exact_remaining_and_clears_prior_counted_calls', () => {
  let state = replace(3)
  state = observe(toolCallId('call-1'), state)
  state = observe(toolCallId('call-2'), state)
  assert.equal(remaining(state), 1)
  assert.equal(countedCallCount(state), 2)

  const replaced = replace(7)
  assert.equal(remaining(replaced), 7)
  assert.equal(countedCallCount(replaced), 0)
})

test('DELEG_022_each_distinct_real_tool_call_decrements_once_and_saturates_at_zero', () => {
  let state = replace(2)
  const first = toolCallId('call-1')

  state = observe(first, state)
  assert.equal(remaining(state), 1)
  assert.equal(countedCallCount(state), 1)

  state = observe(first, state)
  assert.equal(remaining(state), 1, 'same ToolCallId replay is idempotent')
  assert.equal(countedCallCount(state), 1)

  state = observe(toolCallId('call-2'), state)
  assert.equal(remaining(state), 0)
  assert.equal(countedCallCount(state), 2)

  state = observe(toolCallId('call-3'), state)
  assert.equal(remaining(state), 0, 'zero is saturating, never negative')
  assert.equal(countedCallCount(state), 2, 'zero stops dedupe evidence growth')
})

test('DELEG_022_projection_is_incremental_not_a_transcript_or_xtrace_scan', () => {
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Execution/Delegation/DelegatedToolEstimateProjection.fs', import.meta.url),
    'utf8',
  )

  for (const forbidden of ['XTrace', 'transcript', 'messages', 'Dictionary<', 'mutable ']) {
    assert.ok(!source.includes(forbidden), `projection must not depend on ${forbidden}`)
  }
  assert.match(source, /Set<ToolCallId>/, 'idempotence evidence is typed by ToolCallId')
})
