import assert from 'node:assert/strict'
import test from 'node:test'
import { toolSpecNames } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { requiredNames } from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[action-affordance-007] current static tool catalog does not reuse an active participant name', () => {
  const roles = new Set(requiredNames.map((name) => name.toLowerCase()))
  const tools = toolSpecNames()
  assert.ok(tools.length > 0)
  for (const tool of tools) assert.equal(roles.has(tool.toLowerCase()), false, tool)
})

test.todo('WHAT[action-affordance-007] all tool names are distinct semantic verbs; existing horizon and js names need a normative naming decision (GAP-078)')
