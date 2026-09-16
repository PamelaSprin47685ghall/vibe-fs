import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const {
  roleLabel,
  managedAgentName,
  isInternalRuntime,
  canBeForkedOrHorizonTarget,
  hasBloggerCompanion,
  contract,
} = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')

test('WHAT[DISTILL-009] distiller_is_private_leaf_runtime_without_public_target_or_blogger_companion', () => {
  assertJsData(contract, 'distiller contract')
  assertJsData(roleLabel, 'roleLabel')
  assertJsData(managedAgentName, 'managedAgentName')
  assertJsData(isInternalRuntime, 'isInternalRuntime')
  assertJsData(canBeForkedOrHorizonTarget, 'canBeForkedOrHorizonTarget')
  assertJsData(hasBloggerCompanion, 'hasBloggerCompanion')

  assert.equal(roleLabel, 'distiller')
  assert.equal(canBeForkedOrHorizonTarget, false)
  assert.equal(managedAgentName, 'distiller')
  assert.equal(isInternalRuntime, true)
  assert.equal(contract.internalRuntime, true)
  assert.equal(contract.publicTarget, false)
  assert.equal(contract.managedAgent, 'distiller')
  assert.equal(hasBloggerCompanion, false)
  assert.equal(contract.bloggerCompanion, false)
})
