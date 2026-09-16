import assert from 'node:assert/strict'
import test from 'node:test'
import * as joinCompletion from '../../../dist/Execution/Delegation/JoinCompletionSurface.js'
import * as joinSurface from '../../../dist/Execution/Delegation/Fork/Host/JoinSurface.js'

test('WHAT[DELEG-013] JOIN_COMPLETION_completed_is_rendered_as_entry_local_work_record', () => {
  const rendered = joinCompletion.renderWorkRecord({ id: 'wr-1', summary: 'ok' })
  assert.equal(rendered.isEntryLocalComment, true)
  assert.doesNotMatch(rendered.wireText, /"dto_wrapper"/)
})
