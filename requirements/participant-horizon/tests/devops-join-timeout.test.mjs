// Split from tests/unit/execution/devops-join-timeout.test.mjs (cutover Wave 2a);
// owner: participant-horizon. DTO 面：DevOps join 超时（10s 预算）与 ForkError 超时的
// wire 是自然语言后果，无 status/error 状态机 DTO 词汇（participant-horizon 003；
// 时间预算/机制面 → time-capability）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { joinResultRenderer } from '../../verification-system/tests/support/domain.mjs'
import { ForkError } from '../../../dist/Session/ForkTypes.js'
import { JoinInterruptReason } from '../../../dist/Session/CompletionMailbox.js'

test('devops_join_deadline_renders_natural_language_not_timed_out_dto', () => {
  const wire = joinResultRenderer.renderInterrupted(JoinInterruptReason.DeadlineExpired)
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
  assert.equal(parseToml(wire).error, undefined)
})

test('devops_join_timed_out_fork_error_also_natural_language', () => {
  const wire = joinResultRenderer.renderForkError(ForkError.TimedOut)
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
})
