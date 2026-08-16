// Split from tests/unit/execution/devops-join-timeout.test.mjs (cutover Wave 2a);
// owner: time-capability. EXEC-025 deadline 机制面：DevOps join 等待的 10s 预算到点 →
// JoinInterruptReason.DeadlineExpired，渲染为自然语言「waiting ended」（TIME-002；
// DTO/自然语言后果面 → participant-horizon）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { joinResultRenderer } from '../../verification-system/tests/support/domain.mjs'
import { JoinInterruptReason } from '../../../dist/Execution/Session/Wait/CompletionMailbox.js'

test('WHAT[TIME-002] EXEC_025_join_deadline_expired_renders_waiting_ended_natural_language', () => {
  // DeadlineExpired 是 join 等待的 deadline 机制触发点；其 wire 是自然语言，无 DTO。
  const wire = joinResultRenderer.renderInterrupted(JoinInterruptReason.DeadlineExpired)
  assert.match(wire, /No return reached you before your waiting ended/)
})
