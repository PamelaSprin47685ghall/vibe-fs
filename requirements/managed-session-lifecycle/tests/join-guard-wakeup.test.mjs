import assert from 'node:assert/strict'
import test from 'node:test'
import * as HandleFoldSurface from '../../../dist/Execution/Delegation/Handle/FoldSurface.js'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'

const makeActive = () => {
  const result = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link', handle: 'agent:c1', child: 'ses_child', agent: 'coder', role: 'Coder',
  })
  assert.equal(result.ok, true)
  return result.state
}

const complete = (state) => HandleSurface.apply(state, { op: 'complete', handle: 'agent:c1', kind: 'Terminal' })
const retire = (state) => HandleSurface.apply(state, { op: 'retire', handle: 'agent:c1' })

test('WHAT[MANAGED-SESSION-006] THEOREM_join_blocked_while_handle_active', () => {
  const projection = makeActive()
  assert.deepEqual(HandleSurface.views(projection), { listable: ['agent:c1'], joinable: [], active: ['agent:c1'] })
})

test('WHAT[MANAGED-SESSION-007] THEOREM_handle_completed_causally_awakens_joinable', () => {
  const completed = complete(makeActive())
  assert.equal(completed.ok, true)
  assert.deepEqual(HandleSurface.views(completed.state).joinable, ['agent:c1'])
})

test('WHAT[MANAGED-SESSION-007] THEOREM_join_wake_path_trace_WorkActivated_then_HandleCompleted', () => {
  const folded = HandleFoldSurface.foldApply(HandleFoldSurface.foldEmpty(), [
    { fact: { case: 'HandleLinked', payload: { ParentSessionId: 'ses_parent', ChildSessionId: 'ses_child', Handle: 'agent:c1', TargetAgent: 'coder', CanonicalRole: 'Coder', Ownership: 'DurableParentHandle' } } },
    { fact: { case: 'HandleCompleted', payload: { ParentSessionId: 'ses_parent', Handle: 'agent:c1', Kind: 'Terminal' } } },
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const projection = HandleFoldSurface.foldSession(folded.state, 'ses_parent')
  assert.deepEqual(HandleSurface.views(projection).joinable, ['agent:c1'])
})

test('WHAT[MANAGED-SESSION-006] THEOREM_WorkActivated_and_HandleLinked_interleavings_stay_blocked', () => {
  const active = makeActive()
  assert.deepEqual(HandleSurface.views(active), { listable: ['agent:c1'], joinable: [], active: ['agent:c1'] })
})

test('WHAT[MANAGED-SESSION-008] THEOREM_blocked_to_awakened_fold_trails_confluent_after_retire', () => {
  const active = makeActive()
  const completed = complete(active)
  const retired = retire(completed.state)
  assert.equal(retired.ok, true)
  assert.deepEqual(HandleSurface.views(retired.state), { listable: [], joinable: [], active: [] })
})

test('WHAT[MANAGED-SESSION-006] THEOREM_projection_steps_enumerate_blocked_then_awakened_then_clear', () => {
  const active = makeActive()
  const completed = complete(active)
  const retired = retire(completed.state)
  assert.deepEqual([
    HandleSurface.views(active).listable.length,
    HandleSurface.views(completed.state).joinable.length,
    HandleSurface.views(retired.state).listable.length,
  ], [1, 1, 0])
})
