// Tool-host truncation semantics through the owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'

const { registerBounded } = await import('../../../dist/OpenCode/Codec/ToolHostSurface.js')

test('WHAT[DISTILL-012] CODEC_register_applies_tool_with_uncurried_execute_and_bounds_result', async () => {
  const registrations = []
  const fakeTool = (definition) => {
    registrations.push(definition)
    return { registered: definition.description, execute: definition.execute }
  }

  const registered = registerBounded(
    { tool: fakeTool },
    'demo',
    'a demo tool',
    () => Promise.resolve('x'.repeat(60000)),
  )

  assert.equal(registrations.length, 1)
  assert.equal(registrations[0].description, 'a demo tool')

  // Execute is uncurried (args, context) and the result passes ToolResultBound.
  const output = await registered.execute({}, { sessionID: 'ses_demo' })
  assert.ok(output.length < 60000, 'output must be bounded, not the raw 60k string')
})
