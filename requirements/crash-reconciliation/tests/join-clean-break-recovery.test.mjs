// Delayed clean-break recovery remains incomplete until proven terminal.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

test('WHAT[CRASH-009] P0_CLEAN_BREAK_delayed_recovery_before_ready_no_aborted_join_then_true_terminal', () => {
  const waiting = child.resolve('active', 'missing', ['aborted:interrupted tool', 'restore'], '')
  assert.equal(waiting.result, 'RecoveryIncomplete')
  assert.equal(handles.crashScenario('active').retired, false)
  const terminal = child.resolve('active', 'terminal', [], 'real work done')
  assert.equal(terminal.result, 'RecoveredTerminal')
  const wire = join.renderBatch('english', [{ kind: 'completed', agentId: 'h1', agentName: 'fast-coder', role: 'Coder', runId: 'run-h1', workRecord: 'real work done' }])
  assert.ok(!wire.includes('status = "aborted"'))
})

test('WHAT[CRASH-010] P0_CLEAN_BREAK_aborted_only_observation_is_incomplete_not_blocked', () => {
  const result = child.resolve('active', 'missing', ['aborted:interrupted tool', 'restore'], '')
  assert.equal(result.result, 'RecoveryIncomplete')
  assert.notEqual(result.result, 'RecoveryBlocked')
  assert.equal(handles.crashScenario('active').joinable, 0)
})
