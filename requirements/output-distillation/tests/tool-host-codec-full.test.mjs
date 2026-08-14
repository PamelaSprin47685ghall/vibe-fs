// Split from tests/unit/plugin/tool-host-codec-full.test.mjs (cutover Wave 2a);
// owner: output-distillation.
//
// DISTILL-012 (确定性留尾截断 / ToolResultBound): the register path is the one
// dynamic JS boundary where a tool definition's execute result crosses to the
// Host. The registered execute is uncurried (args, context) and its result
// passes ToolResultBound — bounded, not the raw 60k string.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  ToolHostCodec_factory: makeFactory,
  ToolHostCodec_register: register,
  ToolSpec,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')

test('CODEC_register_applies_tool_with_uncurried_execute_and_bounds_result', async () => {
  const registrations = []
  const fakeTool = (definition) => {
    registrations.push(definition)
    return { registered: definition.description, execute: definition.execute }
  }
  const factory = makeFactory({ tool: fakeTool })

  const spec = new ToolSpec('demo', 'a demo tool', [], async (_args, _ctx) => 'x'.repeat(60000))
  const registered = register(factory, spec)

  assert.equal(registrations.length, 1)
  assert.equal(registrations[0].description, 'a demo tool')

  // Execute is uncurried (args, context) and the result passes ToolResultBound.
  const output = await registered.execute({}, { sessionID: 'ses_demo' })
  assert.ok(output.length < 60000, 'output must be bounded, not the raw 60k string')
})
