// Join v2 Domain JoinProgram constructors (EXEC-018) + interrupt ≠ HandleAbandoned.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  handleId,
  handleProjection,
  joinInterrupt,
  joinProgram,
  roles,
  sessionId,
} from '../support/domain.mjs'

// FamilyRecoveryPermit is opaque; joinAvailable only stores the permit value in the AST.
// A plain object stands in for the permit at the pure-constructor layer.
const FAKE_PERMIT = { __testPermit: true }

test('EXEC_018_join_available_program_case_is_join_available', () => {
  const interrupt = joinInterrupt.create()
  const program = joinProgram.joinAvailable(FAKE_PERMIT, 32, joinInterrupt.wait(interrupt))
  assert.equal(joinProgram.caseName(program), 'JoinAvailable')
  assert.equal(caseOf(program), 'JoinAvailable')
})

test('EXEC_018_join_any_program_case_is_join_any', () => {
  const program = joinProgram.joinAny(FAKE_PERMIT)
  assert.equal(joinProgram.caseName(program), 'JoinAny')
})

// Interrupted join must not abandon a still-active child handle (EXEC-017).
// Pure projection: Active stays Active after an "interrupted join" (no abandon call).

test('EXEC_017_interrupted_join_does_not_abandon_active_child_handle', () => {
  const HANDLE = handleId.agent('child-still-running')
  const CHILD = sessionId('ses_child_running')
  let projection = handleProjection.empty
  const linked = handleProjection.link(HANDLE, CHILD, 'fast-coder', roles.of('Coder'), projection)
  assert.equal(linked.ok, true)
  projection = linked.value

  // Simulate interrupted join: no HandleAbandoned / retire / complete.
  assert.equal(handleProjection.isAbandoned(HANDLE, projection), false)
  assert.equal(handleProjection.isRetired(HANDLE, projection), false)
  assert.equal(handleProjection.read(handleProjection.tryFind(HANDLE, projection)).lifecycle, 'Active')
  assert.deepEqual(
    handleProjection.activeHandles(projection).map((r) => handleProjection.read(r).handle),
    ['agent:child-still-running'],
  )
  assert.equal(handleProjection.joinable(projection).length, 0)
})
