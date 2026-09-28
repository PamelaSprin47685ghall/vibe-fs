import assert from 'node:assert/strict'
import test from 'node:test'
import { capabilities, exactReadonlyHostToolMap, isAllowedTool } from '../../../dist/Strength/Surface.js'

test('WHAT[capability-enforcement-005] replica projections and tool policy allow only the three read-only operations', () => {
  assert.deepEqual(exactReadonlyHostToolMap, [
    { tool: '*', allowed: false },
    { tool: 'glob', allowed: true },
    { tool: 'grep', allowed: true },
    { tool: 'read', allowed: true },
  ])
  for (const role of ['engineer', 'devops']) {
    assert.deepEqual(capabilities(role), ['Glob', 'Grep', 'Read'])
  }
  assert.deepEqual(capabilities('manager'), [])
  for (const tool of ['read', 'glob', 'grep']) assert.equal(isAllowedTool(tool), true, tool)
  for (const tool of ['write', 'edit', 'run', 'fork', 'resume', 'join', 'network', 'bash', 'horizon', 'fission', 'unknown']) {
    assert.equal(isAllowedTool(tool), false, tool)
  }
})
