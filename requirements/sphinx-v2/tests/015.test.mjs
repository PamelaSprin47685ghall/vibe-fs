import assert from 'node:assert/strict'
import test from 'node:test'
import {inspectRegistry, declaration, manifest, schemaHash} from './registry-support.mjs'

// SUPERSEDES retains epistemic-reasoning WHAT020's complete plugin lock under this
// clause. These cases prove declaration/callable identity, not schema-document hashing.
test('WHAT[sphinx-v2-015] matching declared and executable plugin manifests preserve every locked field', () => {
  const value = manifest('matching')
  const result = inspectRegistry([declaration(value)])
  assert.equal(result.ok, true)
  assert.deepEqual(result.value.ordered, [value])
  assert.deepEqual(result.value.lock, [value])
})

for (const [field, replacement] of [
  ['id', 'different-id'],
  ['release', 'different-release@2'],
  ['implementationHash', 'different-implementation'],
  ['abiHash', 'different-abi'],
  ['capabilities', ['different-capability']],
  ['dependencies', ['different-dependency']],
  ['schemas', [{name: 'response', id: 'different.response@2', hash: schemaHash('locked')}]],
  ['schemas', [{name: 'response', id: 'fixture.response@2', hash: schemaHash('different-content')}]],
  ['schemas', [{name: 'different-name', id: 'fixture.response@2', hash: schemaHash('locked')}]],
]) {
  test(`WHAT[sphinx-v2-015] a declared plugin lock refuses different executable ${field} ${JSON.stringify(replacement)}`, () => {
    const declared = manifest('locked')
    const rejected = inspectRegistry([declaration(declared, {...declared, [field]: replacement})])
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error.code, 'plugin-manifest-mismatch')
    assert.notEqual(rejected.error.message.trim(), '')
  })
}
