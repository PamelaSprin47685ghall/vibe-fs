import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { renderBudget } from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'
import * as estimate from '../../../dist/Execution/Delegation/DelegatedToolEstimateSurface.js'

test('WHAT[delegation-022] calibration prose states the four numbers from the whole authored resource', () => {
  const cases = [
    ['English', 'en.md', 4, 6, 125000, '2 minutes 5 seconds', '3 minutes 7 seconds'],
    ['SimplifiedChinese', 'zh-CN.md', 4, 6, 125000, '2 分钟 5 秒', '3 分钟 7 秒'],
    ['English', 'en.md', 0, 3, 60000, '1 minute 0 seconds', '3 minutes 0 seconds'],
  ]

  for (const [language, file, turnsUsed, turnsLeft, elapsedMs, timeUsed, timeLeft] of cases) {
    const rendered = renderBudget(language, turnsUsed, turnsLeft, elapsedMs)
    const resource = readFileSync(
      new URL(`../../../resources/provider/host/pair-programming-budget/${file}`, import.meta.url),
      'utf8',
    )
    const expected = resource
      .trim()
      .replaceAll('{{turns_used}}', String(turnsUsed))
      .replaceAll('{{turns_left}}', String(turnsLeft))
      .replaceAll('{{time_used}}', timeUsed)
      .replaceAll('{{time_left}}', timeLeft)
    assert.equal(rendered, expected, `${language}: turns ${turnsUsed}/${turnsLeft} at ${elapsedMs}ms`)
  }
})

test('WHAT[delegation-022] actual estimate projection deduplicates calls and saturates at zero', () => {
  assert.deepEqual(estimate.replay(3, []), { remaining: 3, countedCalls: 0 })
  assert.deepEqual(estimate.replay(3, ['one', 'one']), { remaining: 2, countedCalls: 1 })
  assert.deepEqual(estimate.replay(3, ['one', 'two', 'one', 'three']), { remaining: 0, countedCalls: 3 })
  assert.deepEqual(estimate.replay(3, ['one', 'two', 'three', 'four']), { remaining: 0, countedCalls: 3 })
})

test.todo('WHAT[delegation-022] real tool execution remains allowed after estimate exhaustion and replacement resets the same durable estimate through reopen (GAP-153)')
