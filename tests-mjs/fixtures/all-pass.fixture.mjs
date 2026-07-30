// A clean run, used to prove the supervisor does not delay one.
//
// Guards 「让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口」: with `unref` removed from
// `Watchdog._arm`, this run would still take a whole silence window to exit.
import test from 'node:test'
import assert from 'node:assert/strict'

test('finishes immediately', () => {
  assert.equal(1, 1)
})
