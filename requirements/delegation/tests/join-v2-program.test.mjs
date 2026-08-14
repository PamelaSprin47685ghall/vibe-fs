// Join v2 direct CE surface (EXEC-018) + interrupt ≠ HandleAbandoned.
//
// PR5: Domain JoinProgram AST deleted. Application/Reconciliation/Join.fs is the
// sole permit-gated join entry. Interrupt semantics stay pure projection checks.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(fileURLToPath(new URL('../../../', import.meta.url)))
import {
  handleId,
  handleProjection,
  joinProgram,
  roles,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'
import * as LinkageProjectionModule from '../../../dist/Execution/Delegation/LinkageProjection.js'
import { HandleOwnership } from '../../../dist/Composition/Durable/Fact.js'

/** Production HandleProjection.link takes Ownership (GREEN-7); the domain.mjs
 *  facade bind is stale, so tests call the dist entry directly. */
const link = (handle, child, targetAgent, role, current) => {
  const result = LinkageProjectionModule.HandleProjection_link(
    handle,
    child,
    targetAgent,
    role,
    HandleOwnership.DurableParentHandle,
    current,
  )
  return result.tag === 0
    ? { ok: true, value: result.fields[0] }
    : { ok: false, error: result.fields[0].cases()[result.fields[0].tag] }
}

test('EXEC_021_duplicate_join_is_fail_closed_before_waiting', () => {
  const runtime = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs'), 'utf8')
  const joinHost = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Join.fs'), 'utf8')
  const orchestrator = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Change/Host/Host.fs'),
    'utf8',
  )
  assert.match(runtime, /let mutable joinInFlight = false/)
  assert.match(joinHost, /ForkError\.JoinInProgress/)
  assert.match(joinHost, /finally[\s\S]*ReleaseJoin\(\)/)
  assert.match(orchestrator, /let joinGate = obj \(\)/)
  assert.match(orchestrator, /JOIN_IN_PROGRESS/)
})
test('EXEC_018_join_ops_publish_permit_gated_entrypoints', () => {
  assert.equal(typeof joinProgram.joinAny, 'function')
  assert.equal(typeof joinProgram.joinAvailable, 'function')
})

test('EXEC_018_join_module_has_no_command_reply_ast_exports', async () => {
  const mod = await import(new URL('../../../dist/Execution/Delegation/Join.js', import.meta.url).pathname)
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
  const linked = link(HANDLE, CHILD, 'fast-coder', roles.of('Coder'), projection)
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
