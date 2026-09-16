// Chronicle tool identity and argument catalog belong to capability-enforcement;
// the provider-facing schema itself is exercised by the real plugin contract.

import assert from 'node:assert/strict'
import test from 'node:test'

import { installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { chronicleContract } from '../../../dist/OpenCode/Tools/ToolSurface.js'

installDefaultResources()

test('WHAT[ENF-006] CHRONICLE_spec_exposes_identity_and_argument_surface', () => {
  const contract = chronicleContract()
  assert.equal(contract.name, 'chronicle')
  assert.deepEqual(contract.argumentNames, ['entry', 'tip'])
  assert.equal(contract.tipCount, 120)
})
