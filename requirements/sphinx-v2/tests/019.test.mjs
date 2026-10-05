import assert from 'node:assert/strict'
import test from 'node:test'
import * as persistence from '../../../dist/Sphinx/V2/Persistence/Surface.js'
import {
  store, digest, mustOk, body, envelope, goal, createdBody, work,
  transition, accepted, graphNode, seedBodies, batch, prepare, append, current, withStore,
} from './persistence-support.mjs'

const text = '\n原文保留\r\n  不归一化  '

const expectCut = async (handle, event, code) => {
  const receipt = await store.append(handle, [event])
  assert.equal(receipt.ok, true, JSON.stringify(receipt.error))
  assert.equal(receipt.cuts.length, 1)
  assert.equal(receipt.cuts[0].failedEventId, event.id)
  assert.equal(receipt.cuts[0].rule, 'SphinxV2')
  assert.match(receipt.cuts[0].reason, new RegExp(code))
  assert.equal(store.read(handle, event.id).id, event.id, 'bad fact is preserved, not deleted')
  const cut = store.read(handle, receipt.cuts[0].cutEventId)
  assert.equal(cut.type, 'ProjectionCutTail')
  return receipt.cuts[0]
}

test('WHAT[sphinx-v2-019] a rejected multi-body preparation never publishes its valid prefix', async () => {
  await withStore(async ({ open }) => {
    const handle = open()
    const creation = await append(handle, batch('atomic-inquiry', 'create', [createdBody(text)]))
    const before = current(handle, 'atomic-inquiry')
    const bad = batch('atomic-inquiry', 'bad', [
      body('CancelRequested', { reason: 'prefix must not publish' }),
      transition('missing-work', 'Planned', { case: 'Ready' }),
    ], creation)
    const result = persistence.prepareTransition(handle, digest, bad)
    assert.equal(result.ok, false)
    assert.match(result.error.message, /not planned/)
    assert.deepEqual(current(handle, 'atomic-inquiry'), before)
    assert.deepEqual(store.heads(handle, creation.stream), [creation.id])
    assert.equal(mustOk(before).commandReceipts.length, 1)
  })
})

test('WHAT[sphinx-v2-019] an unknown body after a valid body produces a durable semantic cut and no partial Current even after reopen', async () => {
  await withStore(async ({ open, close }) => {
    const first = open()
    const creation = await append(first, batch('bad-body', 'create', [createdBody(text)]))
    const candidate = prepare(first, batch('bad-body', 'bad', [body('CancelRequested', { reason: 'prefix' })], creation))
    candidate.payload.events.push(body('UnknownBody', { reason: 'must refuse' }))
    await expectCut(first, candidate, 'INVALID_TRANSITION_DTO')
    const rejected = current(first, 'bad-body')
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error.code, 'SemanticCut')
    assert.equal(Object.hasOwn(rejected, 'value'), false)
    assert.deepEqual(store.read(first, creation.id).payload.events, [createdBody(text)])
    close(first)
    const reopened = open()
    assert.deepEqual(current(reopened, 'bad-body'), rejected)
  })
})

test('WHAT[sphinx-v2-019] internal and external parent edges must agree and foreign valid history is not silently used as the base', async () => {
  await withStore(async ({ open, close }) => {
    const handle = open()
    const own = await append(handle, batch('own-inquiry', 'own', [createdBody(text)]))
    const foreign = await append(handle, batch('foreign-inquiry', 'foreign', [createdBody('foreign goal')]))
    const foreignBefore = current(handle, 'foreign-inquiry')
    const candidate = prepare(handle, batch('own-inquiry', 'bad-parent', [body('CancelRequested', { reason: 'stop' })], own))
    candidate.payload.previousHead = foreign.id
    await expectCut(handle, candidate, 'PARENT_MISMATCH')
    assert.equal(current(handle, 'own-inquiry').error.code, 'SemanticCut')
    assert.deepEqual(current(handle, 'foreign-inquiry'), foreignBefore)
    close(handle)
    const reopened = open()
    assert.equal(current(reopened, 'own-inquiry').error.code, 'SemanticCut')
    assert.deepEqual(current(reopened, 'foreign-inquiry'), foreignBefore)
  })
})

test('WHAT[sphinx-v2-019] an absent outer parent is StorageInvalid and cannot publish any transition', async () => {
  await withStore(async ({ open, close }) => {
    const handle = open()
    const creation = await append(handle, batch('missing-parent', 'create', [createdBody(text)]))
    const before = current(handle, 'missing-parent')
    const candidate = prepare(handle, batch('missing-parent', 'bad-parent', [body('CancelRequested', { reason: 'stop' })], creation))
    candidate.parents = ['absent-parent']
    const receipt = await store.append(handle, [candidate])
    assert.equal(receipt.ok, false)
    assert.equal(receipt.error.code, 'StorageInvalid')
    assert.equal(receipt.error.error.code, 'MissingParent')
    close(handle)
    const reopened = open()
    assert.deepEqual(current(reopened, 'missing-parent'), before)
    assert.equal(store.read(reopened, candidate.id), null)
  })
})

test('WHAT[sphinx-v2-019] identical event identity with changed payload is a storage collision rather than a rewrite', async () => {
  await withStore(async ({ open, close }) => {
    const handle = open()
    const first = await append(handle, batch('collision-inquiry', 'create', [createdBody(text)]))
    const before = current(handle, 'collision-inquiry')
    const changed = prepare(handle, batch('collision-inquiry', 'create', [createdBody('changed goal')]))
    assert.equal(changed.id, first.id, 'identity must not depend on post-state')
    assert.notEqual(changed.payload.postStateFingerprint, first.payload.postStateFingerprint)
    const receipt = await store.append(handle, [changed])
    assert.equal(receipt.ok, false)
    assert.equal(receipt.error.code, 'StorageInvalid')
    assert.equal(receipt.error.error.code, 'IdentityCollision')
    close(handle)
    const reopened = open()
    assert.deepEqual(current(reopened, 'collision-inquiry'), before)
    assert.deepEqual(store.read(reopened, first.id), first)
  })
})

test('WHAT[sphinx-v2-019] legal same-parent forks preserve both writers and canonical Current refuses to pick a winner', async () => {
  for (const order of [['left', 'right'], ['right', 'left']]) {
    await withStore(async ({ open, close }) => {
      const rootWriter = open()
      const creation = await append(rootWriter, batch('fork-inquiry', 'create', [createdBody(text)]))
      close(rootWriter)
      const leftWriter = open()
      const rightWriter = open()
      const left = prepare(leftWriter, batch('fork-inquiry', 'left', [body('CancelRequested', { reason: 'left' })], creation))
      const right = prepare(rightWriter, batch('fork-inquiry', 'right', [body('InquirySuspended', { reason: 'right' })], creation))
      const writers = { left: leftWriter, right: rightWriter }
      const events = { left, right }
      for (const name of order) {
        const receipt = await store.append(writers[name], [events[name]])
        assert.equal(receipt.ok, true, JSON.stringify(receipt.error))
        assert.deepEqual(receipt.cuts, [], 'a legitimate branch is not corruption or semantic rejection')
      }
      close(leftWriter)
      close(rightWriter)
      const reopened = open()
      const conflict = current(reopened, 'fork-inquiry')
      assert.equal(conflict.ok, false)
      assert.equal(conflict.error.code, 'DomainConflict')
      assert.deepEqual(conflict.error.heads, [left.id, right.id].sort())
      assert.deepEqual(store.heads(reopened, creation.stream), [left.id, right.id].sort())
      assert.deepEqual(store.read(reopened, left.id), left)
      assert.deepEqual(store.read(reopened, right.id), right)
      assert.equal(persistence.admitCancel(reopened, { inquiry: 'fork-inquiry', commandId: 'new', commandFingerprint: digest('new'), reason: 'stop' }).error.code, 'DomainConflict')
    })
  }
})

test('WHAT[sphinx-v2-019] a later legitimate child of an accepted ancestor is not rejected by a global last-state check', async () => {
  await withStore(async ({ open, close }) => {
    const handle = open()
    const creation = await append(handle, batch('ancestor-inquiry', 'create', [createdBody(text)]))
    const first = await append(handle, batch('ancestor-inquiry', 'first', [body('CancelRequested', { reason: 'first' })], creation))
    const sibling = await append(handle, batch('ancestor-inquiry', 'sibling', [body('InquirySuspended', { reason: 'sibling' })], creation))
    assert.deepEqual(current(handle, 'ancestor-inquiry').error.heads, [first.id, sibling.id].sort())
    close(handle)
    assert.deepEqual(current(open(), 'ancestor-inquiry').error.heads, [first.id, sibling.id].sort())
  })
})

test('WHAT[sphinx-v2-019] post-state verification checks actual complete state, not array sizes or the old incomplete stateHash', async () => {
  await withStore(async ({ open, close }) => {
    const handle = open()
    const creation = await append(handle, batch('fingerprint-inquiry', 'create', [createdBody(text)]))
    // A same-length change in a formerly omitted field must invalidate the sealed state.
    const candidate = prepare(handle, batch('fingerprint-inquiry', 'invalid-hash', [body('CancelRequested', { reason: 'stop' })], creation))
    candidate.payload.postStateFingerprint = digest('not the complete state')
    await expectCut(handle, candidate, 'post-state-mismatch')
    assert.equal(current(handle, 'fingerprint-inquiry').error.code, 'SemanticCut')
    close(handle)
    assert.equal(current(open(), 'fingerprint-inquiry').error.code, 'SemanticCut')
  })
  const hashes = []
  for (const configHash of [digest('configuration-a'), digest('configuration-b')]) {
    await withStore(async ({ open }) => {
      const handle = open()
      const created = createdBody(text)
      created.payload.configHash = configHash
      await append(handle, batch('same-hash-input', 'same-command', [created]))
      hashes.push(current(handle, 'same-hash-input').stateHash)
    })
  }
  assert.notEqual(hashes[0], hashes[1], 'full materialized config is not erased by equal-sized projections')
})

test('WHAT[sphinx-v2-019] canonical replay honors the parent when a child identity sorts before it', async () => {
  await withStore(async ({ open, close }) => {
    const rootWriter = open()
    const creation = await append(rootWriter, batch('causal-inquiry', 'create', [createdBody(text)]))
    close(rootWriter)
    const childWriter = open()
    let child = null
    // Input selection only: the assertion below still reads the real canonical owner.
    for (let index = 0; index < 128 && child === null; index += 1) {
      const candidate = prepare(childWriter, batch('causal-inquiry', 'child-' + index, [body('InquirySuspended', { reason: 'causal child' })], creation))
      if (candidate.id < creation.id) child = candidate
    }
    assert.notEqual(child, null, 'fixture must contain a lexically earlier child')
    const receipt = await store.append(childWriter, [child])
    assert.equal(receipt.ok, true)
    assert.deepEqual(receipt.cuts, [])
    close(childWriter)
    const replayed = mustOk(current(open(), 'causal-inquiry'))
    assert.equal(replayed.eventHead, child.id)
    assert.equal(replayed.revision, '1')
    assert.deepEqual(replayed.status, { case: 'Suspended', reason: 'causal child' })
    assert.equal(replayed.goal.originalText, text)
  })
})

test('WHAT[sphinx-v2-019] strict ingress rejects malformed fields and arrays without silently dropping them', () => {
  const valid = batch('ingress', 'create', [createdBody(text)])
  const invalid = [
    { ...valid, revision: 0 }, { ...valid, revision: '01' }, { ...valid, revision: '9223372036854775808' },
    { ...valid, events: {} }, { ...valid, events: [] }, { ...valid, extra: true },
    { ...valid, events: [body('InquiryCreated', { ...createdBody(text).payload, resourceSpecs: {} })] },
    { ...valid, events: [body('WorkPlanned', { work: [{ ...work('work-1'), attempt: 1.5 }] })] },
    { ...valid, events: [body('WorkPlanned', { work: [{ ...work('work-1'), dependencies: ['same', 'same'] }] })] },
    { ...valid, events: [body('CancelRequested', { reason: 17 })] },
    { ...valid, events: [body('CancelRequested', { reason: 'stop', certificatePatches: [] })] },
  ]
  for (const value of invalid) {
    const result = persistence.canonicalizeTransition(value)
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'INVALID_TRANSITION_DTO')
    assert.equal(Object.hasOwn(result, 'value'), false)
  }
  assert.deepEqual(mustOk(persistence.canonicalizeTransition(valid)), valid)
})

test('WHAT[sphinx-v2-019] frozen @1 material is preserved and explicitly cut, never silently restored as an empty inquiry', async () => {
  await withStore(async ({ open, close }) => {
    const first = open()
    const old = { id: 'legacy-transition', stream: 'sphinx-v2/legacy-inquiry', type: 'sphinx/v2-transition@1',
      parents: [], payload: { frozenHistoricalMaterial: true }, payloadRefs: [] }
    await expectCut(first, old, 'LEGACY_TRANSITION_UNSUPPORTED')
    const rejected = current(first, 'legacy-inquiry')
    assert.equal(rejected.error.code, 'SemanticCut')
    assert.match(rejected.error.message, /LEGACY_TRANSITION_UNSUPPORTED/)
    close(first)
    const reopened = open()
    assert.deepEqual(store.read(reopened, old.id), old)
    assert.deepEqual(current(reopened, 'legacy-inquiry'), rejected)
  })
})

test('WHAT[sphinx-v2-019] creation and multi-body work transition append atomically at one revision and survive a new writer', async () => {
  await withStore(async ({ open, close }) => {
    const first = open()
    const creation = await append(first, batch('inquiry-1', 'create', [createdBody(text)]))
    const original = mustOk(current(first, 'inquiry-1'))
    assert.equal(original.goal.originalText, text)
    assert.equal(original.revision, '0')
    assert.equal(original.eventHead, creation.id)
    const planned = await append(first, batch('inquiry-1', 'plan', [
      body('RoundOpened', { roundId: 'round-1', scopeId: 'scope-1', expectedWork: ['work-1'] }),
      body('WorkPlanned', { work: [work('work-1')] }),
      transition('work-1', 'Planned', { case: 'Ready' }),
      transition('work-1', 'Ready', { case: 'Leased', fence: 'work-1:1:logical' }),
      transition('work-1', 'Leased', { case: 'Running', fence: 'work-1:1:logical', physicalRef: 'adapter-receipt-1' }),
    ], creation))
    const live = current(first, 'inquiry-1')
    const value = mustOk(live)
    assert.equal(value.revision, '1', 'all bodies are one atomic logical revision')
    assert.equal(value.eventHead, planned.id)
    assert.deepEqual(value.work[0].value.state, { case: 'Running', fence: 'work-1:1:logical', physicalRef: 'adapter-receipt-1' })
    assert.equal(value.work[0].value.spec.physicalRef, 'adapter-receipt-1')
    assert.equal(value.commandReceipts.length, 2)
    assert.deepEqual(value.commandReceipts.find(entry => entry.key === 'plan').value, {
      fingerprint: digest('command:plan'), revision: '1', eventId: planned.id,
    })
    assert.equal(original.work.length, 0, 'preparation/fold never mutates an earlier native snapshot')
    assert.deepEqual(mustOk(persistence.canonicalizeTransition(store.read(first, planned.id).payload)), planned.payload)
    close(first)
    const reopened = open()
    assert.deepEqual(current(reopened, 'inquiry-1'), live)
  })
})

test('WHAT[sphinx-v2-019] cancellation and a late cancel remain honest across writer close and canonical cold replay', async () => {
  await withStore(async ({ open, close }) => {
    const first = open()
    const creation = await append(first, batch('cancel-inquiry', 'create', [createdBody(text)]))
    const request = await append(first, batch('cancel-inquiry', 'cancel', [body('CancelRequested', { reason: 'user stop' })], creation))
    const cancelling = current(first, 'cancel-inquiry')
    assert.deepEqual(mustOk(cancelling).status, { case: 'Cancelling' })
    close(first)
    const second = open()
    assert.deepEqual(current(second, 'cancel-inquiry'), cancelling)
    const terminal = await append(second, batch('cancel-inquiry', 'terminal', [body('InquiryCancelled', { reason: 'settled' })], request))
    await append(second, batch('cancel-inquiry', 'late-cancel', [body('CancelRequested', { reason: 'late' })], terminal))
    const cancelled = current(second, 'cancel-inquiry')
    assert.deepEqual(mustOk(cancelled).status, { case: 'Cancelled', reason: 'settled' })
    assert.equal(mustOk(cancelled).revision, '3')
    close(second)
    assert.deepEqual(current(open(), 'cancel-inquiry'), cancelled)
  })
})

const usage = { workId: 'work-2', attempt: 1, resources: [{ key: 'calls', value: 1 }], moneyMinor: '19', usageUnresolved: false, overrun: false }
const amendedGoal = {
  ...goal(text), revision: '1', constraints: ['user constraint'],
  amendments: [{ authorizedBy: 'user', revision: '1', addedConstraints: ['user constraint'], replacedText: null }],
}
// Public @2 vocabulary, not compiler tag numbers. No-op handlers are only tested for
// faithful durable representation here, not claimed as completed business operations.
const cases = [
  createdBody(text),
  body('GoalAmended', amendedGoal),
  body('SnapshotRegistered', envelope('{}')),
  body('DecisionScopeOpened', envelope('{}')),
  body('RoundOpened', { roundId: 'round-2', scopeId: 'scope-1', expectedWork: ['work-2'] }),
  body('WorkPlanned', { work: [work('work-3')] }),
  body('RoundClosed', { roundId: 'round-1', outcome: 'complete' }),
  body('BudgetReserved', { reservation: { workId: 'work-2', attempt: 1, resources: [{ key: 'calls', value: 1 }], moneyMinor: '19' }, renderReserve: [{ key: 'calls', value: 1 }] }),
  body('UsageSettled', { usage }),
  body('ReservationReleased', { workId: 'work-2', attempt: 1 }),
  body('UsageOverrunRecorded', { usage: { ...usage, overrun: true } }),
  body('DispatchRequested', { work: work('work-2'), dispatchIntentId: 'dispatch-1', publicEnvelope: envelope('{}'), privateTicket: envelope('{}') }),
  body('DispatchReceiptRecorded', { workId: 'work-2', attempt: 1, fence: 'work-2:1:logical', dispatchIntentId: 'dispatch-1', physicalRef: 'adapter-receipt-2', receipt: envelope('{}') }),
  transition('work-2', 'Running', { case: 'Failed', attempt: 1 }),
  body('HostTerminalRecorded', { workId: 'work-2', attempt: 1, fence: 'work-2:1:logical', terminal: 'stopped', receipt: envelope('{}') }),
  accepted('work-2', 'observation-2'),
  body('InterpretationPending', { observationId: 'observation-1', workId: 'work-1', attempt: 1 }),
  body('InterpretationApplied', { observationId: 'observation-1', interpretationId: 'interpretation-1', pluginRef: 'proof-plugin', delta: envelope('{}') }),
  body('InterpretationFailed', { observationId: 'observation-1', interpretationId: 'interpretation-1', pluginRef: 'proof-plugin', reason: 'plugin failed' }),
  body('GraphPatched', { pluginRef: 'proof-plugin', patch: { upsertNodes: [graphNode('node-2')], removeNodes: [], upsertEdges: [{ id: 'edge-1', tails: ['node-1'], heads: ['node-2'], relation: 'links', payload: envelope('{}'), revision: '0' }], removeEdges: [] } }),
  body('CertificateSlotsPatched', { patches: [{ certificateId: 'certificate-1', targetRef: 'node-1', valueSpaceId: 'value-1', scopeId: 'scope-1', semanticsModelRef: 'model-1', expectedSlotRevision: '0', slot: { slot: 'summary', producer: 'proof-plugin', schema: envelope('{}').schema, canonicalPayload: '{}', revision: '1', guarantee: { case: 'EmpiricalSummary', assumptions: ['declared'] }, status: { case: 'Current' } } }] }),
  body('CertificateInvalidated', { invalidation: envelope('{}'), reason: 'goal changed' }),
  body('DecisionRecorded', { decision: envelope('{}') }),
  body('AnswerPrepared', { renderWorkId: 'work-1', draftRef: 'draft-1' }),
  body('AnswerCommitted', { renderWorkId: 'work-1', resultObservationId: 'observation-1', answerRef: 'answer-1', stopReason: 'resource-limited' }),
  body('CancelRequested', { reason: 'user stop' }),
  body('InquiryCancelled', { reason: 'settled' }),
  body('InquirySuspended', { reason: 'missing capability' }),
  body('InquiryFailed', { reason: 'failed' }),
  body('InquiryStatusChanged', { status: 'input-required', reason: 'authorization needed' }),
]

test('WHAT[sphinx-v2-019] all 30 body cases cross real encoding append and new-writer canonical replay without losing public DTO fields', async () => {
  assert.equal(cases.length, 30)
  assert.equal(new Set(cases.map(value => value.case)).size, 30)
  for (const value of cases) {
    assert.deepEqual(mustOk(persistence.canonicalizeBody(value)), value, value.case)
    await withStore(async ({ open, close }) => {
      const first = open()
      const creation = await append(first, batch('body-inquiry', 'seed', value.case === 'InquiryCreated' ? [value] : seedBodies(text)))
      const encoded = value.case === 'InquiryCreated' ? creation
        : await append(first, batch('body-inquiry', 'body', [value], creation))
      const durable = store.read(first, encoded.id)
      assert.deepEqual(durable.payload.events.at(-1), value, value.case)
      const live = current(first, 'body-inquiry')
      assert.equal(mustOk(live).eventHead, encoded.id)
      assert.equal(mustOk(live).revision, value.case === 'InquiryCreated' ? '0' : '1')
      close(first)
      const reopened = open()
      assert.deepEqual(current(reopened, 'body-inquiry'), live, value.case)
      assert.deepEqual(store.read(reopened, encoded.id), durable, value.case)
    })
  }
})
