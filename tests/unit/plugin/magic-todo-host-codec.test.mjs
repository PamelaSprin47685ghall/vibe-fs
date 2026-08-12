import assert from 'node:assert/strict'
import test from 'node:test'
import {
  listItems,
  magicTodoHost,
} from '../support/domain.mjs'

test('TODO-002 decodes the clean-break obligations wire', () => {
  const decoded = magicTodoHost.decodeObligations({
    obligations: [
      { name: 'bridge', work: 'Review the bridge' },
      { name: 'proof', work: 'Close the proof' },
    ],
  })

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  const rows = listItems(decoded.value)
  assert.equal(rows[0].Name, 'bridge')
  assert.equal(rows[0].Work, 'Review the bridge')
  assert.equal(rows[1].Name, 'proof')
  assert.equal(rows[1].Work, 'Close the proof')

  const malformed = magicTodoHost.decodeObligations({ obligations: [{ name: 1, work: 'x' }] })
  assert.equal(malformed.ok, false)
  assert.equal(malformed.error, 'todowrite.name must be a string')
})

test('TODO-007 projects obligations into the V1 sink by in-place mutation only', () => {
  const args = { obligations: [{ name: 'provider-only', work: 'must disappear from sink args' }] }
  const output = { args }
  magicTodoHost.replaceCompatibilityArgs(output, [
    { Content: 'bridge: Review bridge', Status: 'in_progress', Priority: 'medium' },
  ])

  assert.equal(output.args, args, 'before must preserve the Host args object identity')
  assert.deepEqual(output.args, {
    todos: [{ content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' }],
  })
})

test('TODO-002 replaces description, parameters, and jsonSchema with obligations', () => {
  const output = { description: '', parameters: {}, jsonSchema: {} }
  magicTodoHost.applyDefinition(output)

  assert.match(output.description, /living obligation account/)
  assert.deepEqual(output.parameters.required, ['obligations'])
  assert.deepEqual(output.jsonSchema.required, ['obligations'])
  assert.deepEqual(output.parameters.properties.obligations.items.required, ['name', 'work'])
  assert.equal(output.parameters.properties.obligations.items.properties.status, undefined)
  assert.equal(output.parameters.properties.obligations.items.properties.id, undefined)
})
