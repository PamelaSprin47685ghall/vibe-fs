// VERIFY-004's 「禁止：让被遗弃的测试在稍后 reject，从而掩盖真正的失败」, under test.
//
// A overruns its per-test bound; B is trivially correct and runs after it. The clause names this the
// easiest mistake to make — a timeout that rejects a racing Promise without clearing the follow-on
// timer fires during the NEXT test and blames it. The clause also says 「运行器必须有测试覆盖这一点」,
// and before W4 no such test existed anywhere in this repository.
import test from 'node:test'
import assert from 'node:assert/strict'

import { PER_TEST_TIMEOUT_MS } from '../../e2e/support/time-budget.js'

test('A overruns its bound', { timeout: PER_TEST_TIMEOUT_MS }, async () => {
  // 1.5× the injected bound: overruns at whatever scale the gate injects, and the margin keeps
  // the verdict-fail strictly ahead of the sleep resolving, so B cannot inherit the blame.
  await new Promise((resolve) => setTimeout(resolve, Math.floor(PER_TEST_TIMEOUT_MS * 1.15)))
})

test('B is trivially correct', () => {
  assert.equal(1, 1)
})
