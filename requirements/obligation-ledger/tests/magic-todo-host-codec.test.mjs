import assert from 'node:assert/strict'
import test from 'node:test'
import {
  listItems,
  magicTodoHost,
} from '../../../tests/unit/support/domain.mjs'

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

  const missingWork = magicTodoHost.decodeObligations({ obligations: [{ name: 'bridge' }] })
  assert.equal(missingWork.ok, false)
  assert.equal(missingWork.error, "todowrite obligation item requires field 'work'")

  const missingName = magicTodoHost.decodeObligations({ obligations: [{ work: 'some work' }] })
  assert.equal(missingName.ok, false)
  assert.equal(missingName.error, "todowrite obligation item requires field 'name'")

  const emptyName = magicTodoHost.decodeObligations({ obligations: [{ name: '   ', work: 'some work' }] })
  assert.equal(emptyName.ok, false)
  assert.equal(emptyName.error, 'todowrite obligation.name must be a non-empty string')

  const duplicateName = magicTodoHost.decodeObligations({
    obligations: [
      { name: 'same', work: 'first' },
      { name: 'same', work: 'second' },
    ],
  })
  assert.equal(duplicateName.ok, false)
  assert.equal(duplicateName.error, "todowrite duplicate obligation name 'same'")
})

test('TODO-007 projects obligations into a non-enumerable V1 compatibility view', () => {
  const args = { obligations: [{ name: 'provider-only', work: 'must remain durable provider input' }] }
  const output = { args }
  magicTodoHost.replaceCompatibilityArgs(output, [
    { Content: 'bridge: Review bridge', Status: 'in_progress', Priority: 'medium' },
  ])

  assert.equal(output.args, args, 'before must preserve the Host args object identity')
  assert.deepEqual(output.args, {
    obligations: [{ name: 'provider-only', work: 'must remain durable provider input' }],
  })
  assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
  assert.deepEqual(output.args.todos, [
    { content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' },
  ])
})

test('TODO-002 replaces description, parameters, and jsonSchema with obligations', () => {
  const output = { description: '', parameters: {}, jsonSchema: {} }
  magicTodoHost.applyDefinition(output)

  assert.match(output.description, /living obligation account/)
  assert.deepEqual(output.parameters.required, ['obligations'])
  assert.deepEqual(output.jsonSchema.required, ['obligations'])
  assert.deepEqual(output.parameters.properties.obligations.items.required, ['name', 'work'])
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /survey-startup-and-complexity/)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /placeholder/)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /completion counterfactual/)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /handoff-complete/)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /TBD/)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.name.description, output.parameters.properties.obligations.items.properties.name.description)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.work.description, output.parameters.properties.obligations.items.properties.work.description)
  assert.equal(output.parameters.properties.obligations.items.properties.status, undefined)
  assert.equal(output.parameters.properties.obligations.items.properties.id, undefined)
})
