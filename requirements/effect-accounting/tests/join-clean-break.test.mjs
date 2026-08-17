// Clean-break false-finality laws through delegation-owned surfaces.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as clean from '../../../dist/Execution/Delegation/Fork/CleanBreakSurface.js'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

const legacy = () => clean.legacyBody('run-legacy-abort')

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_agent_join_wire_never_renders_aborted', () => {
  const wire = clean.joinWire('fast-coder', 'host abort was observation, not finality')
  assert.ok(!wire.includes('status = "aborted"'))
  assert.match(wire, /# fast-coder could not complete the charge\./)
  assert.match(wire, /host abort was observation, not finality/)
  assert.ok(!/\bstatus\s*=/.test(wire))
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_tryFromDurableCompleted_refuses_send_failure_aborted_body', () => {
  const result = clean.tryDecode('h-false-abort', legacy())
  assert.equal(result.ok, false)
  assert.match(result.error, /legacy false abort|not a joinable completion/i)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_legacy_aborted_blob_decodes_without_run_completion', () => {
  assert.deepEqual(clean.decode(legacy()), { case: 'LegacyFalseAbort' })
  assert.equal(clean.tryDecode('h-false-abort', legacy()).ok, false)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_v2_terminal_decodes_as_joinable_completion', () => {
  const body = JSON.stringify({
    schemaVersion: 2,
    finality: 'completed',
    run_id: 'run-2',
    work_record: 'ok',
    child_session_id: 'child',
    authority_root: 'root',
    provider_run: 'provider',
    directory: '',
  })
  assert.deepEqual(clean.decode(body), { case: 'Current' })
  assert.equal(clean.tryDecode('h-v2', body).ok, true)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_retired_legacy_abort_refuses_without_replacement', () => {
  // Decode permanently detects legacy false abort (EFFECT-ACCOUNTING-007).
  assert.equal(clean.decode(legacy()).case, 'LegacyFalseAbort')
  // No replacement surface — retired path refuses, does not mint recovery:<agent>:<digest>.
  assert.equal(typeof clean.replacement, 'undefined')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_retired_legacy_abort_never_surfaces_aborted', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a1', agentName: 'fast-coder', reason: 'legacy abort' }])
  assert.ok(!wire.includes('aborted'))
  assert.ok(!wire.includes('status ='))
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_fold_replay_keeps_retired_terminal_tombstone', () => {
  const state = handles.crashScenario('replayed-retired')
  assert.equal(state.lifecycle, 'Retired')
  assert.equal(state.retired, true)
  assert.equal(state.joinable, 0)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_CLEAN_BREAK_invalid_blob_keeps_join_waiting', () => {
  assert.deepEqual(clean.decode('{not-json'), { case: 'Invalid' })
  assert.equal(clean.tryDecode('h-invalid', '{not-json').ok, false)
})
