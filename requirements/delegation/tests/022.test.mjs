import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { renderToolEstimate } from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'
import * as estimate from '../../../dist/Execution/Delegation/DelegatedToolEstimateSurface.js'

test('WHAT[delegation-022] estimate prose selects the whole advisory resource in each provider language', () => {
  for (const [language, file] of [['English', 'en.md'], ['SimplifiedChinese', 'zh-CN.md']]) {
    const rendered = renderToolEstimate(language, 4)
    const resource = readFileSync(new URL(`../../../resources/provider/host/pair-programming-tool-estimate/${file}`, import.meta.url), 'utf8')
    assert.equal(rendered, resource.trim().replaceAll('{{remaining}}', '4'))
  }
})

test('WHAT[delegation-022] actual estimate projection deduplicates calls and saturates at zero', () => {
  assert.deepEqual(estimate.replay(3, []), { remaining: 3, countedCalls: 0 })
  assert.deepEqual(estimate.replay(3, ['one', 'one']), { remaining: 2, countedCalls: 1 })
  assert.deepEqual(estimate.replay(3, ['one', 'two', 'one', 'three']), { remaining: 0, countedCalls: 3 })
  assert.deepEqual(estimate.replay(3, ['one', 'two', 'three', 'four']), { remaining: 0, countedCalls: 3 })
})

test.todo('WHAT[delegation-022] real tool execution remains allowed after estimate exhaustion and replacement resets the same durable estimate through reopen (GAP-153)')
