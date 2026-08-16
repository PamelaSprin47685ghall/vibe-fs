// Join entry rendering through the provider projection owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/OpenCode/JoinResultRendererSurface.js'

test('WHAT[PROVIDER-PROJECTION-009] MISC_join_render_batch_empty_work_record_no_comment', () => {
  const wire = join.renderAgentCompletion('english', 'x', '')
  assert.match(wire, /# x has returned\./)
  assert.equal(wire.trim().split('\n').length, 1)
})

test('WHAT[PROVIDER-PROJECTION-009] MISC_join_render_batch_child_to_parent_lwr_stays_entry_local_comment', () => {
  const wire = join.renderAgentCompletion('english', 'fast-coder', 'Chronicle\ndid the thing\n\nRecent work\nok')
  assert.match(wire, /# fast-coder has returned\./)
  assert.match(wire, /^# Chronicle$/m)
  assert.match(wire, /^# did the thing$/m)
  assert.equal(wire.includes('work_record ='), false)
  assert.equal(wire.includes("= '''"), false)
})
