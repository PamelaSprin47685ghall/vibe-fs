// Join v2 direct CE surface (EXEC-018) + interrupt ≠ HandleAbandoned.
//
// PR5: Domain JoinProgram AST deleted. Application/Reconciliation/Join.fs is the
// sole permit-gated join entry. Interrupt semantics stay pure projection checks.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  handleId,
  handleProjection,
  joinProgram,
  roles,
  sessionId,
} from '../support/domain.mjs'

test('EXEC_018_join_ops_publish_permit_gated_entrypoints', () => {
  assert.equal(typeof joinProgram.joinAny, 'function')
  assert.equal(typeof joinProgram.joinAvailable, 'function')
})

test('EXEC_018_join_module_has_no_command_reply_ast_exports', async () => {
  const mod = await import(new URL('../../../dist/Application/Reconciliation/Join.js', import.meta.url).pathname)
  const names = Object.keys(mod).filter((n) => !n.endsWith('_$reflection'))
  assert.equal(
    names.some((n) => /Command|Reply|JoinProgram|Interpreter|Step|Return/.test(n)),
    false,
    `second-runtime exports leaked: ${names.join(', ')}`,
  )
  assert.ok(names.some((n) => n.includes('joinAny') || n === 'joinAny'), names.join(', '))
  assert.ok(names.some((n) => n.includes('joinAvailable') || n === 'joinAvailable'), names.join(', '))
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
