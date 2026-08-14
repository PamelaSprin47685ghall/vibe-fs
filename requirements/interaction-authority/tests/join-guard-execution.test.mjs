// Split from tests/unit/execution/join-guard.test.mjs (cutover Wave 2a);
// owner: interaction-authority. EXEC-016 的 continuation 半边
// （INTERACTION-AUTHORITY-014）：JoinGuard 是 Continuation，不创建新 Authority；
// guard 文本是 instruction-only 且必须稳定（outstanding-background 判定 →
// delegation / managed-session-lifecycle / change-integration）。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  continuationKind,
  promptOrigin,
  runtimeNudge,
} from '../../verification-system/tests/support/domain.mjs'

test('EXEC_016_join_guard_continuation_kind_is_parseable', () => {
  const kind = continuationKind.of('JoinGuard')
  assert.equal(caseOf(promptOrigin.continuation(kind)), 'Continuation')
})

test('EXEC_016_join_guard_text_demands_join_before_finish', () => {
  assert.deepEqual(runtimeNudge.backgroundJoinGuardInstructions, [
    'Work remains away.',
    '',
    'Receive arrived consequences before claiming completion.',
    '',
    'If useful independent work remains, continue it.',
    'Wait only when a real dependency makes waiting useful.',
    '',
    'Use horizon when orientation would change your next action.',
    'Use join when receiving an arrived consequence is useful now.',
  ])
  assert.match(runtimeNudge.backgroundJoinGuard, /Work remains away/)
  assert.match(runtimeNudge.backgroundJoinGuard, /Use join/)
})
