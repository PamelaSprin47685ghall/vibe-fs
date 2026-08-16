// requirements/output-distillation/tests/distiller-role-contract.test.mjs
//
// Owner: output-distillation.
//
// DISTILL-009: the Distiller mapping sub-session is a private runtime. Its
// managed identity must never become a public `fork` / `horizon` target.
// DISTILL-010: the role has no execution, mutation, or judgement permissions;
// the provider-visible `run` verb is the only execution surface and invokes
// distillation internally.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const {
  roleLabel,
  managedAgentName,
  isInternalRuntime,
  canBeForkedOrHorizonTarget,
  permissionLabels,
  executionToolName,
  contract,
} = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')

test('WHAT[DISTILL-009] distiller_is_private_internal_runtime_not_a_public_fork_or_horizon_target', () => {
  assertJsData(contract, 'distiller contract')
  assertJsData(roleLabel, 'roleLabel')
  assertJsData(managedAgentName, 'managedAgentName')
  assertJsData(isInternalRuntime, 'isInternalRuntime')
  assertJsData(canBeForkedOrHorizonTarget, 'canBeForkedOrHorizonTarget')
  assertJsData(permissionLabels, 'permissionLabels')
  assertJsData(executionToolName, 'executionToolName')

  assert.equal(roleLabel, 'distiller')
  assert.equal(canBeForkedOrHorizonTarget, false)
  assert.equal(managedAgentName, 'fast-distiller')
  assert.equal(isInternalRuntime, true)
  assert.equal(contract.internalRuntime, true)
  assert.equal(contract.publicTarget, false)
  assert.equal(contract.managedAgent, 'fast-distiller')
})

test('WHAT[DISTILL-010] distiller_carries_no_execution_or_judgement_permissions_and_run_is_the_only_execution_surface', () => {
  assert.deepEqual(permissionLabels, [], 'Distiller must carry zero tool permissions')
  assert.deepEqual(contract.permissions, [])
  assert.equal(executionToolName, 'run', 'the execution tool surface is `run`; distill is not a separate provider tool')
  assert.equal(contract.executionTool, 'run')
})
