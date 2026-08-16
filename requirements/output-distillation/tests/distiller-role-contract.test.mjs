// requirements/output-distillation/tests/distiller-role-contract.test.mjs
//
// Owner: output-distillation (Wave 2a split leftovers; trace migration NEW).
//
// DISTILL-009: the Distiller mapping sub-session is a private runtime. Its
// managed agent identity lives in the internal role vocabulary (AGENT-008) and
// must never surface as a public `fork` / `horizon` target.
//
// DISTILL-010: the Distiller role does not execute, does not change the world,
// and does not judge acceptance. The role's permission catalog is empty; the
// provider tool surface for execution is the `run` tool only — distillation is
// invoked inside it, never exposed as a separate provider tool.
//
// Contract tests read the registered surfaces (RolesSurface +
// ExecutorToolSurface); Role/AgentTier/ToolSpec never cross as Fable values
// (JS-SEMANTIC-SURFACE-002/003/005).

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const { allPublicRoleLabels, allInternalRoleLabels, managedAgentName, permissions } = await import(
  '../../../dist/Foundation/RolesSurface.js'
)
const { runToolName } = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')

test('WHAT[DISTILL-009] distiller_is_private_internal_runtime_not_a_public_fork_or_horizon_target', () => {
  // AGENT-008 public vocabulary = the only roles a caller may fork to or put on
  // the horizon; the Distiller must never appear there.
  assertJsData(allPublicRoleLabels, 'allPublicRoleLabels')
  assert.ok(
    !allPublicRoleLabels.includes('distiller'),
    `Distiller must not be a public fork/horizon target; public roles are: ${allPublicRoleLabels.join(', ')}`,
  )

  assertJsData(allInternalRoleLabels, 'allInternalRoleLabels')
  assert.ok(
    allInternalRoleLabels.includes('distiller'),
    'Distiller must be an internal role (private runtime), not part of the public vocabulary',
  )

  // The machine Assignment identity of the map/reduce sub-session is the
  // internal `fast-distiller` handle — a Host-owned private agent, not a
  // provider-visible persona.
  const name = managedAgentName('fast', 'distiller')
  assertJsData(name, 'managedAgentName')
  assert.equal(
    name,
    'fast-distiller',
    'Distiller map/reduce forks use the internal fast-distiller managed agent name',
  )
})

test('WHAT[DISTILL-010] distiller_carries_no_execution_or_judgement_permissions_and_run_is_the_only_execution_surface', () => {
  // The role's tool-permission catalog is empty: it cannot run commands
  // (Exec/Pty), cannot change the world (Write/Edit/Move/Remove), and cannot
  // judge acceptance (Judge/Behavior).
  const perms = permissions('distiller')
  assertJsData(perms, 'permissions(distiller)')
  assert.deepEqual(perms, [], 'Distiller must carry zero tool permissions')

  // Execution and distillation are different office surfaces: the provider
  // tool registry exposes the `run` tool; distillation is orchestrated inside
  // it and is not a separate provider tool.
  assertJsData(runToolName, 'runToolName')
  assert.equal(runToolName, 'run', 'the execution tool surface is `run`; distill is not a separate provider tool')
})
