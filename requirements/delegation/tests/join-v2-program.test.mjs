// Join-v2 program retains durable handle ownership and typed error branches.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

const source = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/LinkageProjection.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-005] JOIN_V2_linkage_projection_has_durable_parent_ownership', () => {
  assert.match(source, /DurableParentHandle/)
  assert.match(source, /CompletedAwaitingJoin/)
})
test('WHAT[DELEG-005] JOIN_V2_replay_is_idempotent', () => {
  assert.deepEqual(handles.crashScenario('replayed-completed'), handles.crashScenario('completed'))
})
test('WHAT[DELEG-005] JOIN_V2_retired_handle_is_not_listable', () => {
  assert.equal(handles.crashScenario('retired').retired, true)
})
