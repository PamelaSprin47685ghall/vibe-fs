import assert from 'node:assert/strict'
import test from 'node:test'

import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'

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
const factory = { tool: { schema: fakeSchema } }

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_bookkeeper_provider_contract_is_one_program', () => {
  const tool = bookkeeper.contract(factory)
  assert.equal(tool.name, 'js-bookkeeper')
  assert.deepEqual(tool.argumentNames, ['program'])
  assert.match(tool.description, /one atomic JavaScript transformation|用一次原子的 JavaScript transformation/i)
  assert.match(tool.description, /setQuestion\(newText\)/)
  assert.match(tool.description, /setAnswer\(newText\)/)
})
