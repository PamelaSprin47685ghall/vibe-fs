import assert from 'node:assert/strict'
import test from 'node:test'
import * as canaries from '../../../dist/OpenCode/Host/MagicTodoHostCanariesSurface.js'
import * as codec from '../../../dist/OpenCode/Host/MagicTodoHostCodecSurface.js'

test('WHAT[OBLIGATION-LEDGER-024] definition replaces description, parameters, and jsonSchema while the original decoder stays the execute-path decoder', async () => {
  const r = await canaries.testDefinitionReplacesDescriptionAndSchema()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-024] jsonSchema ternary: both parameters and jsonSchema are replaced together', async () => {
  const r = await canaries.testJsonSchemaTernary()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-024] advertises planComplete in description, parameters, and jsonSchema', () => {
  assert.equal(codec.testAdvertisesPlanComplete(), true)
})
