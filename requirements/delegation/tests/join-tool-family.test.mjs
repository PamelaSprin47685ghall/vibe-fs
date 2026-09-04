// Family join branches use the same delegation-owned JoinSurface and retain
// the distinction between manager and orchestrator consequences.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

test('WHAT[DELEG-019] JOIN_TOOL_family_ready_published_batch_is_stable', () => {
  const wire = join.renderOrchestratorBatch('english', ['Published', 'NeedsReview'])
  assert.match(wire, /published|integrated|review/i)
  assert.doesNotMatch(wire, /\bstatus\s*=/)
})
test('WHAT[DELEG-019] JOIN_TOOL_family_empty_maps_to_nothing_to_join', () => {
  assert.equal(join.renderOrchestratorBatch('english', []), '')
})
test('WHAT[DELEG-019] JOIN_TOOL_family_error_precedence_is_natural_language', () => {
  for (const error of ['Cancelled', 'JoinInProgress', 'TimedOut', 'NotFound', 'Abandoned', 'TerminalMaterializationFailed']) {
    const wire = join.renderForkError('english', error)
    assert.ok(wire.length > 0, error)
    assert.doesNotMatch(wire, /\bstatus\s*=|\[error\]/)
  }
})
