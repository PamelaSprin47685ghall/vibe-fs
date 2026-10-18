import assert from 'node:assert/strict'
import test from 'node:test'
import { toolSpecNames } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { requiredNames } from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[action-affordance-007] current static tool catalog does not reuse an active participant name', () => {
  const roles = new Set(requiredNames.map((name) => name.toLowerCase()))
  // todowrite-for-ROLE tools are namespaced by role; 'devops' is the bound DevOps consultation
  // tool awaiting a normative verb renaming under GAP-078.
  const tools = toolSpecNames().filter((tool) => !tool.startsWith('todowrite-for-') && tool !== 'devops')
  assert.ok(tools.length > 0)
  for (const tool of tools) assert.equal(roles.has(tool.toLowerCase()), false, tool)
})

test.todo('WHAT[action-affordance-007] all tool names are distinct semantic verbs; existing horizon and js names need a normative naming decision (GAP-078)')
