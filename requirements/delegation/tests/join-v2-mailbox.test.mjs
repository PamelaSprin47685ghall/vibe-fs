// Join interruption mailbox consequences are exposed as natural-language
// reasons; mailbox signaling and non-empty ordering stay in the owner.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const waitContract = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Session/Wait/CausalWait.fs', import.meta.url), 'utf8')

test('WHAT[DELEG-019] JOIN_MAILBOX_operator_abort_is_distinct', () => {
  assert.match(join.renderInterrupted('english', 'OperatorAbort'), /waiting was interrupted/)
  assert.match(waitContract, /OperatorAbort/)
})
test('WHAT[DELEG-019] JOIN_MAILBOX_user_message_signal_wakes_current_join', () => {
  assert.match(join.renderInterrupted('english', 'UserMessageArrived'), /Something nearer has arrived/)
  assert.match(waitContract, /UserMessageArrived/)
})
test('WHAT[DELEG-019] JOIN_MAILBOX_deadline_signal_is_not_operator_abort', () => {
  assert.match(join.renderInterrupted('english', 'DeadlineExpired'), /waiting ended/)
  assert.doesNotMatch(join.renderInterrupted('english', 'DeadlineExpired'), /operator/i)
})
test('WHAT[DELEG-019] JOIN_MAILBOX_completion_batch_preserves_order', () => {
  const wire = join.renderBatch('english', [
    { kind: 'completed', agentId: 'first', agentName: 'first', role: 'Coder', runId: 'run-first', workRecord: 'one' },
    { kind: 'completed', agentId: 'second', agentName: 'second', role: 'Coder', runId: 'run-second', workRecord: 'two' },
    { kind: 'completed', agentId: 'third', agentName: 'third', role: 'Coder', runId: 'run-third', workRecord: 'three' },
  ])
  assert.ok(wire.indexOf('first') < wire.indexOf('second'))
  assert.ok(wire.indexOf('second') < wire.indexOf('third'))
})
