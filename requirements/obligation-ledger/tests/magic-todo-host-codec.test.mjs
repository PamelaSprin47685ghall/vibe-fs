import assert from 'node:assert/strict'
import test from 'node:test'
import * as host from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'

test('WHAT[OBLIGATION-LEDGER-002] decodes required planComplete, workingOn, and obligations', () => {
  const decoded = host.decodeInput({
    planComplete: false,
    workingOn: 'bridge',
    obligations: [
      { name: 'bridge', horizon: 'near', work: 'Review the bridge' },
      { name: 'proof', horizon: 'mid', work: 'Close the proof' },
    ],
  })

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.value.planComplete, false)
  assert.equal(decoded.value.workingOn, 'bridge')
  assert.deepEqual(decoded.value.obligations, [
    { name: 'bridge', horizon: 'near', work: 'Review the bridge' },
    { name: 'proof', horizon: 'mid', work: 'Close the proof' },
  ])

  const missingCommitment = host.decodeInput({ workingOn: 'bridge', obligations: [{ name: 'bridge', horizon: 'near', work: 'x' }] })
  assert.equal(missingCommitment.ok, false)
  assert.equal(missingCommitment.error, 'todowrite.planComplete is required')

  const nonBooleanCommitment = host.decodeInput({ planComplete: 'false', workingOn: '', obligations: [] })
  assert.equal(nonBooleanCommitment.ok, false)
  assert.equal(nonBooleanCommitment.error, 'todowrite.planComplete must be a boolean')

  const missingWorkingOn = host.decodeInput({ planComplete: false, obligations: [{ name: 'bridge', horizon: 'near', work: 'x' }] })
  assert.equal(missingWorkingOn.ok, false)
  assert.equal(missingWorkingOn.error, 'todowrite.workingOn is required')

  const misspelledWorkingOn = host.decodeInput({
    planComplete: false,
    workingOn: 'synthesize-evidence-into-road',
    obligations: [
      { name: 'synthesize-evidence-road', horizon: 'near', work: 'x' },
      { name: 'ship', horizon: 'far', work: 'y' },
    ],
  })
  assert.equal(misspelledWorkingOn.ok, true, misspelledWorkingOn.ok ? '' : misspelledWorkingOn.error)
  assert.equal(misspelledWorkingOn.value.workingOn, 'synthesize-evidence-road')

  const tiedWorkingOn = host.decodeInput({
    planComplete: false,
    workingOn: 'cat',
    obligations: [
      { name: 'bat', horizon: 'near', work: 'first nearest' },
      { name: 'hat', horizon: 'near', work: 'second nearest' },
    ],
  })
  assert.equal(tiedWorkingOn.ok, true, tiedWorkingOn.ok ? '' : tiedWorkingOn.error)
  assert.equal(tiedWorkingOn.value.workingOn, 'bat')

  const zeroWork = host.decodeInput({ planComplete: true, workingOn: '', obligations: [] })
  assert.equal(zeroWork.ok, true, zeroWork.ok ? '' : zeroWork.error)

  const zeroWorkWithStrayFocus = host.decodeInput({ planComplete: true, workingOn: 'anything', obligations: [] })
  assert.equal(zeroWorkWithStrayFocus.ok, true, zeroWorkWithStrayFocus.ok ? '' : zeroWorkWithStrayFocus.error)
  assert.equal(zeroWorkWithStrayFocus.value.workingOn, '')

  const malformed = host.decodeInput({ planComplete: false, workingOn: 'bridge', obligations: [{ name: 1, horizon: 'near', work: 'x' }] })
  assert.equal(malformed.ok, false)
  assert.equal(malformed.error, 'todowrite.name must be a string')

  const missingWork = host.decodeInput({ planComplete: false, workingOn: 'bridge', obligations: [{ name: 'bridge', horizon: 'near' }] })
  assert.equal(missingWork.ok, false)
  assert.equal(missingWork.error, "todowrite obligation item requires field 'work'")

  const duplicateName = host.decodeInput({
    planComplete: true,
    workingOn: 'same',
    obligations: [
      { name: 'same', horizon: 'near', work: 'first' },
      { name: 'same', horizon: 'near', work: 'second' },
    ],
  })
  assert.equal(duplicateName.ok, false)
  assert.equal(duplicateName.error, "todowrite duplicate obligation name 'same'")

  const missingHorizon = host.decodeInput({
    planComplete: false,
    workingOn: 'bridge',
    obligations: [{ name: 'bridge', work: 'x' }],
  })
  assert.equal(missingHorizon.ok, false)
  assert.equal(missingHorizon.error, "todowrite obligation item requires field 'horizon'")

  const invalidHorizon = host.decodeInput({
    planComplete: false,
    workingOn: 'bridge',
    obligations: [{ name: 'bridge', horizon: 'urgent', work: 'x' }],
  })
  assert.equal(invalidHorizon.ok, false)
  assert.equal(invalidHorizon.error, "todowrite.horizon must be one of near, mid, far")

  const nonNearFocus = host.decodeInput({
    planComplete: true,
    workingOn: 'ship',
    obligations: [
      { name: 'prepare', horizon: 'near', work: 'Prepare the immediate change.' },
      { name: 'ship', horizon: 'far', work: 'Ship the complete result.' },
    ],
  })
  assert.equal(nonNearFocus.ok, true, nonNearFocus.ok ? '' : nonNearFocus.error)
  assert.equal(nonNearFocus.value.workingOn, 'ship')

  const noNear = host.decodeInput({
    planComplete: true,
    workingOn: 'ship',
    obligations: [{ name: 'ship', horizon: 'far', work: 'Ship the complete result.' }],
  })
  assert.equal(noNear.ok, true, noNear.ok ? '' : noNear.error)
  assert.equal(noNear.value.workingOn, 'ship')

  const misspelledFarFocus = host.decodeInput({
    planComplete: true,
    workingOn: 'shp',
    obligations: [
      { name: 'prepare', horizon: 'near', work: 'Prepare the immediate change.' },
      { name: 'ship', horizon: 'far', work: 'Ship the complete result.' },
    ],
  })
  assert.equal(misspelledFarFocus.ok, true, misspelledFarFocus.ok ? '' : misspelledFarFocus.error)
  assert.equal(misspelledFarFocus.value.workingOn, 'ship')
})

test('WHAT[OBLIGATION-LEDGER-015] workingOn projects to in_progress and every other obligation to pending', () => {
  assert.deepEqual(
    host.projectCompatibilityRows('proof', [
      { name: 'bridge', horizon: 'near', work: 'Review bridge' },
      { name: 'proof', horizon: 'near', work: 'Close proof' },
      { name: 'ship', horizon: 'far', work: 'Ship result' },
    ]),
    [
      { content: 'bridge: Review bridge', status: 'pending', priority: 'medium' },
      { content: 'proof: Close proof', status: 'in_progress', priority: 'medium' },
      { content: 'ship: Ship result', status: 'pending', priority: 'medium' },
    ],
  )
})

test('WHAT[OBLIGATION-LEDGER-015] projects obligations into a non-enumerable V1 compatibility view', () => {
  const args = { planComplete: false, workingOn: 'provider-only', obligations: [{ name: 'provider-only', horizon: 'near', work: 'must remain durable provider input' }] }
  const output = { args }
  host.replaceCompatibilityArgs(output, [
    { content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' },
  ])

  assert.equal(output.args, args, 'before must preserve the Host args object identity')
  assert.deepEqual(output.args, {
    planComplete: false,
    workingOn: 'provider-only',
    obligations: [{ name: 'provider-only', horizon: 'near', work: 'must remain durable provider input' }],
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
  assert.match(output.parameters.properties.planComplete.description, /coverage|覆盖/i)
  assert.match(output.parameters.properties.planComplete.description, /uniform|均匀/i)
  assert.equal(output.jsonSchema.properties.planComplete.description, output.parameters.properties.planComplete.description)
  assert.deepEqual(output.parameters.properties.obligations.items.required, ['name', 'horizon', 'work'])
  assert.deepEqual(output.parameters.properties.obligations.items.properties.horizon.enum, ['near', 'mid', 'far'])
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /frontier|前沿/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /not.*status|不是.*status|不是.*状态/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /near/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /mid/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /far/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /planComplete/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /planning/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /placeholder/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /planComplete/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /completion counterfactual/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /close|closure|闭环/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /TBD/i)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.name.description, output.parameters.properties.obligations.items.properties.name.description)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.work.description, output.parameters.properties.obligations.items.properties.work.description)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.horizon.description, output.parameters.properties.obligations.items.properties.horizon.description)
  assert.equal(output.parameters.properties.obligations.items.properties.status, undefined)
  assert.equal(output.parameters.properties.obligations.items.properties.id, undefined)
})
