import assert from 'node:assert/strict'
import test from 'node:test'

import { listItems } from '../support/domain.mjs'

const { ToolHostCodec_factory } = await import(
  '../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js'
)
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsBookkeeperTool.js')

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => schemaNode('string'),
  enum: (values) => schemaNode('enum', { values }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

test('CASE006_bookkeeper_provider_contract_is_one_program', () => {
  const tool = spec(factory)
  assert.equal(tool.Name, 'js-bookkeeper')
  assert.deepEqual(listItems(tool.Arguments).map((pair) => pair[0]), ['program'])
  assert.match(tool.Description, /one atomic JavaScript transformation/i)
  assert.match(tool.Description, /setQuestion\(newText\)/)
  assert.match(tool.Description, /setAnswer\(newText\)/)
})
