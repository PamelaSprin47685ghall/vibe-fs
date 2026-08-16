import assert from 'node:assert/strict'
import test from 'node:test'
import {
  listItems,
  magicTodoHost,
} from '../../verification-system/tests/support/domain.mjs'

test('WHAT[OBLIGATION-LEDGER-002] decodes required planComplete plus obligations', () => {
  const decoded = magicTodoHost.decodeInput({
    planComplete: false,
    obligations: [
      { name: 'bridge', work: 'Review the bridge' },
      { name: 'proof', work: 'Close the proof' },
    ],
  })

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.value.PlanComplete, false)
  const rows = listItems(decoded.value.Obligations)
  assert.equal(rows[0].Name, 'bridge')
  assert.equal(rows[0].Work, 'Review the bridge')
  assert.equal(rows[1].Name, 'proof')
  assert.equal(rows[1].Work, 'Close the proof')

  const missingCommitment = magicTodoHost.decodeInput({ obligations: [{ name: 'bridge', work: 'x' }] })
  assert.equal(missingCommitment.ok, false)
  assert.equal(missingCommitment.error, 'todowrite.planComplete is required')

  const nonBooleanCommitment = magicTodoHost.decodeInput({ planComplete: 'false', obligations: [] })
  assert.equal(nonBooleanCommitment.ok, false)
  assert.equal(nonBooleanCommitment.error, 'todowrite.planComplete must be a boolean')

  const malformed = magicTodoHost.decodeInput({ planComplete: false, obligations: [{ name: 1, work: 'x' }] })
  assert.equal(malformed.ok, false)
  assert.equal(malformed.error, 'todowrite.name must be a string')

  const missingWork = magicTodoHost.decodeInput({ planComplete: false, obligations: [{ name: 'bridge' }] })
  assert.equal(missingWork.ok, false)
  assert.equal(missingWork.error, "todowrite obligation item requires field 'work'")

  const duplicateName = magicTodoHost.decodeInput({
    planComplete: true,
    obligations: [
      { name: 'same', work: 'first' },
      { name: 'same', work: 'second' },
    ],
  })
  assert.equal(duplicateName.ok, false)
  assert.equal(duplicateName.error, "todowrite duplicate obligation name 'same'")
})

test('WHAT[OBLIGATION-LEDGER-015] projects obligations into a non-enumerable V1 compatibility view', () => {
  const args = { planComplete: false, obligations: [{ name: 'provider-only', work: 'must remain durable provider input' }] }
  const output = { args }
  magicTodoHost.replaceCompatibilityArgs(output, [
    { Content: 'bridge: Review bridge', Status: 'in_progress', Priority: 'medium' },
  ])

  assert.equal(output.args, args, 'before must preserve the Host args object identity')
  assert.deepEqual(output.args, {
    planComplete: false,
    obligations: [{ name: 'provider-only', work: 'must remain durable provider input' }],
  })
  assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
  assert.deepEqual(output.args.todos, [
    { content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' },
  ])
})

test('WHAT[OBLIGATION-LEDGER-024] advertises planComplete in description, parameters, and jsonSchema', () => {
  const output = { description: '', parameters: {}, jsonSchema: {} }
  magicTodoHost.applyDefinition(output)

  assert.match(output.description, /owed-work|owed work|current.*account/i)
  assert.deepEqual(output.parameters.required, ['planComplete', 'obligations'])
  assert.deepEqual(output.jsonSchema.required, ['planComplete', 'obligations'])
  assert.equal(output.parameters.properties.planComplete.type, 'boolean')
  assert.match(output.parameters.properties.planComplete.description, /false/i)
  assert.match(output.parameters.properties.planComplete.description, /true/i)
  assert.match(output.parameters.properties.planComplete.description, /irreversible|cannot.*return|forever|不可逆|永久/i)
  assert.equal(output.jsonSchema.properties.planComplete.description, output.parameters.properties.planComplete.description)
  assert.deepEqual(output.parameters.properties.obligations.items.required, ['name', 'work'])
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /planComplete/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /planning/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /placeholder/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /planComplete/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /completion counterfactual/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /close|closure|闭环/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /TBD/i)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.name.description, output.parameters.properties.obligations.items.properties.name.description)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.work.description, output.parameters.properties.obligations.items.properties.work.description)
  assert.equal(output.parameters.properties.obligations.items.properties.status, undefined)
  assert.equal(output.parameters.properties.obligations.items.properties.id, undefined)
})
