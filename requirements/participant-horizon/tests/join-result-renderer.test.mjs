// Join consequences cross the delegation-owned JoinSurface as plain data.
import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]/
const completed = (over = {}) => ({ kind: 'completed', agentId: 'a1', agentName: 'fast-coder', workRecord: '', ...over })
const failed = (over = {}) => ({ kind: 'failed', agentId: 'a1', agentName: 'deep-reviewer', code: 'E1', message: 'boom', ...over })
const pty = (kind, over = {}) => ({ kind, ptyId: 'pty-1', terminalLabel: 'npm test', outcome: 'exit 0', code: '', message: '', ...over, ...(kind ? { kind } : {}) })

const assertClean = (wire, label) => assert.ok(!LEGACY_DTO.test(wire), `${label}: ${wire}`)

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_completed_natural_language_and_work_record', () => {
  const wire = join.renderBatch('english', [completed({ workRecord: 'did the thing' })])
  assert.match(wire, /# fast-coder has returned\./)
  assert.match(wire, /# did the thing/)
  assertClean(wire, 'completed')
  assert.ok(!wire.includes('work_record ='))
})

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_failed_natural_language_consequence', () => {
  const wire = join.renderBatch('english', [failed({ message: 'no' })])
  assert.match(wire, /# deep-reviewer could not complete the charge\./)
  assert.match(wire, /# no/)
  assertClean(wire, 'failed')
})

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_abandoned_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a1', agentName: 'deep-reviewer', reason: 'operator abort' }])
  assert.match(wire, /# deep-reviewer did not return from this charge\./)
  assertClean(wire, 'abandoned')
})

test('WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_batch_pty_exit_code_observation', () => {
  const wire = join.renderBatch('english', [pty('pty-exited')])
  assert.match(wire, /# npm test has ended\./)
  assert.match(wire, /exit_code = 0/)
  assert.ok(!wire.includes('pty_id'))
})

test('WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_batch_pty_failure_output_observation', () => {
  const wire = join.renderBatch('english', [pty('pty-failed', { ptyId: 'pty-2', outcome: 'crash', code: 'RC', message: 'kaboom' })])
  assert.match(wire, /# npm test has ended\./)
  assert.match(wire, /output = "kaboom"/)
  assert.ok(!wire.includes('code ='))
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_batch_pty_aborted_natural_language', () => {
  const wire = join.renderBatch('english', [pty('pty-aborted', { ptyId: 'pty-3', outcome: 'interrupted', code: 'AB', message: 'esc' })])
  assert.match(wire, /# npm test was interrupted\./)
  assert.match(wire, /output = "esc"/)
  assert.ok(!wire.includes('pty_id'))
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_batch_multiple_items_stable_order', () => {
  const wire = join.renderBatch('english', [
    failed({ message: 'boom' }),
    pty('pty-aborted', { ptyId: 'p', terminalLabel: 'Terminal', outcome: 'x', code: 'C', message: 'm' }),
    completed({ workRecord: 'done' }),
  ])
  assert.equal([...wire.matchAll(/could not complete/g)].length, 1)
  assert.equal([...wire.matchAll(/has returned\./g)].length, 1)
  assert.equal([...wire.matchAll(/was interrupted\./g)].length, 1)
  assertClean(wire, 'multiple')
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_interrupted_natural_language', () => {
  const operatorWire = join.renderInterrupted('english', 'OperatorAbort')
  assert.match(operatorWire, /# Your waiting was interrupted\./)
  assertClean(operatorWire, 'operator')

  const userWire = join.renderInterrupted('english', 'UserMessageArrived')
  assert.match(userWire, /# Something nearer has arrived\./)
  assertClean(userWire, 'user')

  const deadlineWire = join.renderInterrupted('english', 'DeadlineExpired')
  assert.match(deadlineWire, /# No return reached you before your waiting ended\./)
  assertClean(deadlineWire, 'deadline')
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_fork_error_natural_language', () => {
  const cases = [
    ['Empty', /nothing away to receive/],
    ['NothingToJoin', /nothing away to receive/],
    ['Cancelled', /wait was cancelled/],
    ['JoinInProgress', /already in progress/],
    ['Abandoned', /did not return from this charge/],
    ['NotFound', /No one by that name is away/],
    ['TimedOut', /waiting ended/],
    ['TerminalMaterializationFailed', /return could not be gathered/],
  ]
  for (const [error, pattern] of cases) {
    const wire = join.renderForkError('english', error)
    assert.match(wire, pattern, error)
    assertClean(wire, error)
    assert.equal(parseToml(wire).status, undefined, error)
  }
})

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_completed_managed_agent_name_and_raw_resolve', () => {
  for (const agentName of ['fast-coder', 'deep-inspector', 'weird raw name']) {
    const wire = join.renderBatch('english', [completed({ agentName })])
    assert.match(wire, new RegExp(`# ${agentName} has returned\\.`))
  }
})

test('WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_completed_pty_exit_observation', () => {
  const wire = join.renderBatch('english', [pty('pty-exited', { ptyId: 'pty-9', terminalLabel: 'shell' })])
  assert.match(wire, /# shell has ended\./)
  assert.ok(!wire.includes('pty_id'))
})
