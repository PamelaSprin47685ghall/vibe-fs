import assert from 'node:assert/strict'
import test from 'node:test'
import * as host from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'

test('WHAT[OBLIGATION-LEDGER-002] decodes required planComplete, workingOn, and obligations', () => {
  const decoded = host.decodeInput({
    planComplete: false,
    workingOn: 'bridge',
    obligations: [
      { name: 'bridge', work: 'Review the bridge' },
      { name: 'proof', work: 'Close the proof' },
    ],
  })

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.value.planComplete, false)
  assert.equal(decoded.value.workingOn, 'bridge')
  assert.deepEqual(decoded.value.obligations, [
    { name: 'bridge', work: 'Review the bridge' },
    { name: 'proof', work: 'Close the proof' },
  ])

  const missingCommitment = host.decodeInput({ workingOn: 'bridge', obligations: [{ name: 'bridge', work: 'x' }] })
  assert.equal(missingCommitment.ok, false)
  assert.equal(missingCommitment.error, 'todowrite.planComplete is required')

  const nonBooleanCommitment = host.decodeInput({ planComplete: 'false', workingOn: '', obligations: [] })
  assert.equal(nonBooleanCommitment.ok, false)
  assert.equal(nonBooleanCommitment.error, 'todowrite.planComplete must be a boolean')

  const missingWorkingOn = host.decodeInput({ planComplete: false, obligations: [{ name: 'bridge', work: 'x' }] })
  assert.equal(missingWorkingOn.ok, false)
  assert.equal(missingWorkingOn.error, 'todowrite.workingOn is required')

  const unknownWorkingOn = host.decodeInput({ planComplete: false, workingOn: 'proof', obligations: [{ name: 'bridge', work: 'x' }] })
  assert.equal(unknownWorkingOn.ok, false)
  assert.equal(unknownWorkingOn.error, "todowrite.workingOn must match an obligation name; got 'proof'")

  const zeroWork = host.decodeInput({ planComplete: true, workingOn: '', obligations: [] })
  assert.equal(zeroWork.ok, true, zeroWork.ok ? '' : zeroWork.error)

  const malformed = host.decodeInput({ planComplete: false, workingOn: 'bridge', obligations: [{ name: 1, work: 'x' }] })
  assert.equal(malformed.ok, false)
  assert.equal(malformed.error, 'todowrite.name must be a string')

  const missingWork = host.decodeInput({ planComplete: false, workingOn: 'bridge', obligations: [{ name: 'bridge' }] })
  assert.equal(missingWork.ok, false)
  assert.equal(missingWork.error, "todowrite obligation item requires field 'work'")

  const duplicateName = host.decodeInput({
    planComplete: true,
    workingOn: 'same',
    obligations: [
      { name: 'same', work: 'first' },
      { name: 'same', work: 'second' },
    ],
  })
  assert.equal(duplicateName.ok, false)
  assert.equal(duplicateName.error, "todowrite duplicate obligation name 'same'")
})

test('WHAT[OBLIGATION-LEDGER-015] workingOn projects to in_progress and every other obligation to pending', () => {
  assert.deepEqual(
    host.projectCompatibilityRows('proof', [
      { name: 'bridge', work: 'Review bridge' },
      { name: 'proof', work: 'Close proof' },
      { name: 'ship', work: 'Ship result' },
    ]),
    [
      { content: 'bridge: Review bridge', status: 'pending', priority: 'medium' },
      { content: 'proof: Close proof', status: 'in_progress', priority: 'medium' },
      { content: 'ship: Ship result', status: 'pending', priority: 'medium' },
    ],
  )
})

test('WHAT[OBLIGATION-LEDGER-015] projects obligations into a non-enumerable V1 compatibility view', () => {
  const args = { planComplete: false, workingOn: 'provider-only', obligations: [{ name: 'provider-only', work: 'must remain durable provider input' }] }
  const output = { args }
  host.replaceCompatibilityArgs(output, [
    { content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' },
  ])

  assert.equal(output.args, args, 'before must preserve the Host args object identity')
  assert.deepEqual(output.args, {
    planComplete: false,
    workingOn: 'provider-only',
    obligations: [{ name: 'provider-only', work: 'must remain durable provider input' }],
  })
  assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
  assert.deepEqual(output.args.todos, [
    { content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' },
  ])
})

test('WHAT[OBLIGATION-LEDGER-024] advertises planComplete in description, parameters, and jsonSchema', () => {
  const output = { description: '', parameters: {}, jsonSchema: {} }
  host.applyDefinition(output)

  assert.match(output.description, /owed-work|owed work|current.*account/i)
  assert.deepEqual(output.parameters.required, ['planComplete', 'workingOn', 'obligations'])
  assert.deepEqual(output.jsonSchema.required, ['planComplete', 'workingOn', 'obligations'])
  assert.equal(output.parameters.properties.planComplete.type, 'boolean')
  assert.equal(output.parameters.properties.workingOn.type, 'string')
  assert.match(output.parameters.properties.workingOn.description, /obligation|name/i)
  assert.match(output.parameters.properties.workingOn.description, /empty|string|空字符串/i)
  assert.equal(output.jsonSchema.properties.workingOn.description, output.parameters.properties.workingOn.description)
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
