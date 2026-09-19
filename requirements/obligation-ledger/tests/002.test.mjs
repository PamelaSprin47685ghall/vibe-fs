import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const host = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");


test('WHAT[obligation-ledger-002] decodes required planComplete, workingOn, and obligations', () => {
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
test('WHAT[obligation-ledger-002] malformed provider wire is a typed provider rejection', () => {
  let caught = null
  try {
    host.decodeInputOrReject({ workingOn: '', obligations: [] })
  } catch (error) {
    caught = error
  }

  assert.equal(caught?.message, 'todowrite.planComplete is required')
  assert.equal(host.isProviderInputRejection(caught), true)
  assert.equal(host.isProviderInputRejection(new Error('todowrite.planComplete is required')), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const todoJournal = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");
const host = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");
const membrane = await import("../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js");
const locality = await import("../../../dist/Mission/Obligation/Todo/MagicTodoLocalitySurface.js");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')
const openJournal = async (runtime = 'rt_magic_todo_membrane') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-obligation-membrane-'))
  const boot = await journal.JournalSurface_boot(directory, runtime, 4242, '2026-08-11T00:00:00Z')
  assert.equal(boot.ok, true, boot.ok ? '' : boot.error)
  return {
    handle: boot.journal,
    close: () => {
      journal.JournalSurface_dispose(boot.journal)
      rmSync(directory, { recursive: true, force: true })
    },
  }
}
const withJournal = async (body, runtime = 'rt_magic_todo_membrane') => {
  const opened = await openJournal(runtime)
  try {
    return await body(opened.handle)
  } finally {
    opened.close()
  }
}
const openLife = async (handle, session, life) => {
  const result = await membrane.MagicTodoMembraneSurface_openLife(handle, session, life)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
}
const prepare = (handle, session, call, obligations, planComplete = true, state = 0) => {
  const args = { planComplete, workingOn: obligations[0]?.name ?? '', obligations }
  const canonical = host.canonicalInput(args)
  const digest = host.canonicalInputDigest(sha256Hex, args)
  return membrane.MagicTodoMembraneSurface_prepare(handle, session, call, canonical, digest, planComplete, obligations, state)
    .then((result) => ({ result, digest, args, canonical }))
}
const accept = async (handle, prepared, inputDigest, outputDigest) =>
  membrane.MagicTodoMembraneSurface_accept(handle, prepared, 'LiveAfterSuccess', inputDigest, outputDigest)
const assertOk = (result, message = '') => {
  assert.equal(result.ok, true, message || (result.ok ? '' : JSON.stringify(result.error)))
  return result.value
}
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const append = async (handle, session, caseName, payload) => {
  const result = await todoJournal.appendMagicTodo(handle, session, null, fact(caseName, payload))
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result
}
const acceptPlanningFalseCheckpoint = async (handle, session, life, callText) => {
  const planning = await prepare(handle, session, callText, [
    { name: 'inspect-startup', work: 'Inspect startup paths so the implementation plan can be completed.' },
  ], false)
  const prepared = assertOk(planning.result)
  const accepted = await accept(handle, prepared.bridge, planning.digest, sha256Hex('planning-false-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { planning, accepted }
}
const acceptT1Checkpoint = async (handle, session, callText) => {
  const t1 = await prepare(handle, session, callText, [{ name: 'diagnose', work: 'Establish why the first todowrite succeeds.' }])
  const prepared = assertOk(t1.result)
  const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('t1-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { t1, accepted }
}

test('WHAT[obligation-ledger-002] non-matching workingOn does not fail the membrane — all obligations are projected', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-workingon-repair'
    const life = 'life-workingon-repair'
    await openLife(handle, session, life)
    const obligations = [
      { name: 'synthesize-evidence-road', work: 'Synthesize the evidence.' },
      { name: 'ship', work: 'Ship the result.' },
    ]
    // V1 compatibility rows project all obligations regardless of workingOn match
    const rows = host.projectCompatibilityRows('synthesize-evidence-into-road', obligations)
    assert.deepEqual(rows, [
      { content: 'synthesize-evidence-road: Synthesize the evidence.', status: 'pending', priority: 'medium' },
      { content: 'ship: Ship the result.', status: 'pending', priority: 'medium' },
    ])
    // prepare succeeds with valid obligations even when workingOn doesn't match
    const args = { planComplete: false, workingOn: 'synthesize-evidence-into-road', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)
    const result = await membrane.MagicTodoMembraneSurface_prepare(handle, session, 'call-workingon-repair', canonical, digest, false, obligations, 0)
    assert.equal(result.ok, true, 'prepare must not fail for non-matching workingOn')
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const sha256 = (value) => `digest:${value}`
const life = 'manager-life'
const firstCall = 'first-call'
const secondCall = 'second-call'
const obligation = (name, work, horizon = 'near') => ({ name, horizon, work })
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.value
}
const rejected = (result) => {
  assert.equal(result.ok, false, 'expected rejection')
  return result.error
}
const localized = (callId, ordinal, frontier, digest) => ({
  toolCallId: callId,
  toolPartOrdinal: ordinal,
  todowriteCallIds: [callId],
  reviewFrontier: frontier,
  providerInputDigest: digest,
})
const items = [
  obligation('implementation', 'Implement the requested behavior.'),
  obligation('verification', 'Verify the behavior with evidence.', 'far'),
]

test('WHAT[obligation-ledger-002] canonical obligation wire is exactly name/horizon/work with stable digest input', () => {
  const wire = todo.canonicalObligationListWire(items)
  assert.equal(
    wire,
    '[{"name":"implementation","horizon":"near","work":"Implement the requested behavior."},{"name":"verification","horizon":"far","work":"Verify the behavior with evidence."}]',
  )
  assert.equal(todo.obligationListDigest(sha256, items), `digest:${wire}`)
})
}
