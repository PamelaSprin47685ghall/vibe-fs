// ENF-001: ToolCapabilitySet uniquely determined by CanonicalRole × RequestKind
import assert from 'node:assert/strict'
import test from 'node:test'
import { capabilityToolNames } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

const READ_TOOLS = ['read', 'glob', 'grep']

test('WHAT[ENF-001] Inquiry_toolCapabilitiesFor_WorkMain_matches_Roles_permissions', () => {
  const caps = capabilityToolNames('inquiry', 'work-main')
  assert.deepEqual(caps, ['fission', 'inspect', 'sphinx_*'])
  for (const tool of READ_TOOLS) {
    assert.equal(caps.includes(tool), false, `toolCapabilitiesFor must omit ${tool}`)
  }
})
