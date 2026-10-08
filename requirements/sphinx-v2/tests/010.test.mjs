import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import { registerH0bRawAppendTests } from './support/h0b-raw-append-tests.mjs'
import {
  store, persistence, body, envelope, createdBody, work, transition,
  batch, append, current, mustOk, withStore, digest,
} from './persistence-support.mjs'

test('WHAT[sphinx-v2-010] recovery classification distinguishes unsent intent from an unrecorded Host receipt', () => {
  assert.equal(Loop.recoveryAction('DispatchPending'), 'dispatch')
  assert.equal(Loop.recoveryAction('ReceiptPending'), 'reconcile-by-intent')
  assert.equal(Loop.recoveryAction('RunningUnmarked'), 'reconcile-by-intent')
  assert.equal(Loop.recoveryMaySpend('DispatchPending'), true)
  assert.equal(Loop.recoveryMaySpend('ReceiptPending'), false)
})

const inquiry = 'dispatch-inquiry'
const workId = 'dispatch-work'
const plannedWork = work(workId)
const request = body('DispatchRequested', {
  work: plannedWork,
  dispatchIntentId: 'dispatch-1',
  publicEnvelope: envelope('{"question":"原文\\n保留"}'),
  privateTicket: envelope('{"labelMap":{"opaque-a":"source-1"}}'),
})
const receipt = body('DispatchReceiptRecorded', {
  workId, attempt: 1, fence: workId + ':1:logical', dispatchIntentId: 'dispatch-1',
  physicalRef: 'actual-host-child-1', receipt: envelope('{"accepted":true,"childId":"actual-host-child-1"}'),
})
const reservation = (resources = plannedWork.reserved, attempt = 1) => body('BudgetReserved', {
  reservation: { workId, attempt, resources, moneyMinor: null },
  renderReserve: [{ key: 'calls', value: 1 }],
})
const seed = ({ reserve = true, resources = plannedWork.reserved, reservationAttempt = 1, state = 'Leased' } = {}) => [
  createdBody('durable dispatch identity'),
  body('RoundOpened', { roundId: 'round-1', scopeId: 'scope-1', expectedWork: [workId] }),
  body('WorkPlanned', { work: [plannedWork] }),
  ...(reserve ? [reservation(resources, reservationAttempt)] : []),
  ...(state === 'Planned' ? [] : [transition(workId, 'Planned', { case: 'Ready' })]),
  ...(state === 'Leased' ? [transition(workId, 'Ready', { case: 'Leased', fence: workId + ':1:logical' })] : []),
]

registerH0bRawAppendTests({ inquiry, seed, request })

const expectRejected = (writer, previous, events) => {
  const before = current(writer, inquiry)
  const outcome = persistence.prepareTransition(writer, digest, batch(inquiry, 'refused', events, previous))
  assert.equal(outcome.ok, false, 'invalid dispatch facts must be refused, not accepted as no-ops')
  assert.equal(outcome.error.code, 'TRANSITION_REJECTED')
  assert.equal(typeof outcome.error.message, 'string')
  assert.notEqual(outcome.error.message.trim(), '')
  assert.equal(Object.hasOwn(outcome, 'value'), false)
  assert.deepEqual(current(writer, inquiry), before, 'neither a rejected body nor its valid prefix is published')
  assert.deepEqual(store.heads(writer, previous.stream), [previous.id])
}

test('WHAT[sphinx-v2-010] canonical Current freezes the complete durable intent and receipt without inventing Running', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const creation = await append(writer, batch(inquiry, 'seed', seed()))
    const leased = mustOk(current(writer, inquiry)).work
    const intent = await append(writer, batch(inquiry, 'intent', [request], creation))
    const pending = current(writer, inquiry)
    assert.deepEqual(mustOk(pending).physicalBindings, [{ key: 'dispatch-1', value: { request: request.payload, receipt: null } }])
    assert.deepEqual(mustOk(pending).work, leased)
    assert.deepEqual(store.read(writer, intent.id).payload.events, [request])
    close(writer)
    const recorder = open()
    assert.deepEqual(current(recorder, inquiry), pending)
    const recorded = await append(recorder, batch(inquiry, 'receipt', [receipt], intent))
    const bound = current(recorder, inquiry)
    assert.deepEqual(mustOk(bound).physicalBindings, [{ key: 'dispatch-1', value: { request: request.payload, receipt: receipt.payload } }])
    assert.deepEqual(mustOk(bound).work, leased, 'a receipt is an observed binding, not an implicit work-state event')
    assert.deepEqual(store.read(recorder, recorded.id).payload.events, [receipt])
    assert.notEqual(bound.stateHash, pending.stateHash)
    close(recorder)
    assert.deepEqual(current(open(), inquiry), bound)
  })
})

test('WHAT[sphinx-v2-010] a Ready work with its own durable reservation can record an intent before acquiring a lease', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const creation = await append(writer, batch(inquiry, 'seed', seed({ state: 'Ready' })))
    const ready = mustOk(current(writer, inquiry)).work
    assert.deepEqual(ready[0].value.state, { case: 'Ready' })
    await append(writer, batch(inquiry, 'intent', [request], creation))
    const pending = current(writer, inquiry)
    assert.deepEqual(mustOk(pending).physicalBindings, [{ key: 'dispatch-1', value: { request: request.payload, receipt: null } }])
    assert.deepEqual(mustOk(pending).work, ready)
    close(writer)
    assert.deepEqual(current(open(), inquiry), pending)
  })
})

test('WHAT[sphinx-v2-010] exact intent and full receipt replay retain the original record after Running and reservation release', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const creation = await append(writer, batch(inquiry, 'seed', seed()))
    const recorded = await append(writer, batch(inquiry, 'bind', [request, receipt], creation))
    const running = await append(writer, batch(inquiry, 'running', [
      transition(workId, 'Leased', { case: 'Running', fence: receipt.payload.fence, physicalRef: receipt.payload.physicalRef }),
      body('ReservationReleased', { workId, attempt: 1 }),
    ], recorded))
    const before = mustOk(current(writer, inquiry))
    await append(writer, batch(inquiry, 'exact-replay', [request, receipt, request], running))
    const replayed = current(writer, inquiry)
    assert.deepEqual(mustOk(replayed).physicalBindings, before.physicalBindings)
    assert.deepEqual(mustOk(replayed).work, before.work)
    assert.deepEqual(mustOk(replayed).reservations, [])
    assert.equal(mustOk(replayed).physicalBindings.length, 1)
    assert.equal(mustOk(replayed).revision, '3')
    close(writer)
    assert.deepEqual(current(open(), inquiry), replayed)
  })
})

test('WHAT[sphinx-v2-010] new intent requires an existing ready attempt and its own sufficient persisted reservation', async t => {
  const refused = [
    { name: 'missing work', event: body('DispatchRequested', { ...request.payload, work: work('absent-work') }) },
    { name: 'different attempt', event: body('DispatchRequested', { ...request.payload, work: { ...plannedWork, attempt: 2, fence: workId + ':2:logical' } }) },
    { name: 'different fence', event: body('DispatchRequested', { ...request.payload, work: { ...plannedWork, fence: workId + ':1:foreign' } }) },
    { name: 'different immutable input', event: body('DispatchRequested', { ...request.payload, work: { ...plannedWork, input: envelope('{"rewritten":true}') } }) },
    { name: 'different resource declaration', event: body('DispatchRequested', { ...request.payload, work: { ...plannedWork, reserved: [] } }) },
    { name: 'invented physical reference', event: body('DispatchRequested', { ...request.payload, work: { ...plannedWork, physicalRef: 'caller-invented-child' } }) },
    { name: 'Planned work', options: { state: 'Planned' }, event: request },
    { name: 'no reservation', options: { reserve: false }, event: request },
    { name: 'insufficient reservation', options: { resources: [{ key: 'calls', value: 0.5 }] }, event: request },
    { name: 'another attempt reservation', options: { reservationAttempt: 2 }, event: request },
    { name: 'released reservation', prefix: [body('ReservationReleased', { workId, attempt: 1 })], event: request },
    { name: 'Running work', prefix: [transition(workId, 'Leased', { case: 'Running', fence: receipt.payload.fence, physicalRef: receipt.payload.physicalRef })], event: request },
    { name: 'input required work', prefix: [transition(workId, 'Leased', { case: 'InputRequired', fence: receipt.payload.fence })], event: request },
    { name: 'failed work', prefix: [transition(workId, 'Leased', { case: 'Failed', attempt: 1 })], event: request },
    { name: 'cancelled work', prefix: [transition(workId, 'Leased', { case: 'Cancelled', attempt: 1 })], event: request },
  ]
  for (const item of refused) {
    await t.test('WHAT[sphinx-v2-010] refuses ' + item.name + ' without publishing an intent', async () => {
      await withStore(async ({ open, close }) => {
        const writer = open()
        const previous = await append(writer, batch(inquiry, 'seed', [...seed(item.options), ...(item.prefix ?? [])]))
        expectRejected(writer, previous, [item.event])
        const before = current(writer, inquiry)
        close(writer)
        assert.deepEqual(current(open(), inquiry), before)
      })
    })
  }
})

test('WHAT[sphinx-v2-010] accepted intent identity binds all request contents and admits only one intent for a work attempt', async t => {
  const changed = [
    { name: 'public envelope', event: body('DispatchRequested', { ...request.payload, publicEnvelope: envelope('{"changed":true}') }) },
    { name: 'private ticket', event: body('DispatchRequested', { ...request.payload, privateTicket: envelope('{"labelMap":{"opaque-a":"other-source"}}') }) },
    { name: 'public schema', event: body('DispatchRequested', { ...request.payload, publicEnvelope: { ...request.payload.publicEnvelope, schema: { id: 'other@2', hash: digest('other schema') } } }) },
    { name: 'work content', event: body('DispatchRequested', { ...request.payload, work: { ...plannedWork, producer: 'another-producer' } }) },
    { name: 'second intent identity', event: body('DispatchRequested', { ...request.payload, dispatchIntentId: 'dispatch-2' }) },
  ]
  for (const item of changed) {
    await t.test('WHAT[sphinx-v2-010] refuses changed ' + item.name + ' atomically', async () => {
      await withStore(async ({ open }) => {
        const writer = open()
        const creation = await append(writer, batch(inquiry, 'seed', seed()))
        const intent = await append(writer, batch(inquiry, 'intent', [request], creation))
        expectRejected(writer, intent, [body('CancelRequested', { reason: 'must not publish prefix' }), item.event])
        assert.deepEqual(mustOk(current(writer, inquiry)).physicalBindings, [{ key: 'dispatch-1', value: { request: request.payload, receipt: null } }])
      })
    })
  }
})

test('WHAT[sphinx-v2-010] a receipt requires the accepted intent and matching work attempt fence and physical binding', async t => {
  const changed = [
    { name: 'absent intent', intent: false, event: receipt },
    { name: 'another intent', event: body('DispatchReceiptRecorded', { ...receipt.payload, dispatchIntentId: 'unknown-intent' }) },
    { name: 'another work', event: body('DispatchReceiptRecorded', { ...receipt.payload, workId: 'absent-work' }) },
    { name: 'another attempt', event: body('DispatchReceiptRecorded', { ...receipt.payload, attempt: 2 }) },
    { name: 'another fence', event: body('DispatchReceiptRecorded', { ...receipt.payload, fence: workId + ':1:foreign' }) },
    { name: 'conflicting actual physical reference', prefix: [transition(workId, 'Leased', { case: 'Running', fence: receipt.payload.fence, physicalRef: 'actual-host-child-2' })], event: receipt },
    { name: 'conflicting Running reference with absent spec reference', prefix: [body('WorkAttemptTransitioned', {
      workId, attempt: 1, fence: receipt.payload.fence, fromState: 'Leased',
      nextState: { case: 'Running', fence: receipt.payload.fence, physicalRef: 'actual-host-child-2' },
      physicalRef: null,
    })], event: receipt },
  ]
  for (const item of changed) {
    await t.test('WHAT[sphinx-v2-010] refuses receipt for ' + item.name, async () => {
      await withStore(async ({ open }) => {
        const writer = open()
        const previous = await append(writer, batch(inquiry, 'seed', [
          ...seed(), ...(item.intent === false ? [] : [request]), ...(item.prefix ?? []),
        ]))
        expectRejected(writer, previous, [item.event])
      })
    })
  }
})

test('WHAT[sphinx-v2-010] the complete recorded receipt is immutable for its accepted intent', async t => {
  const changed = [
    { name: 'physical reference', payload: { ...receipt.payload, physicalRef: 'actual-host-child-2' } },
    { name: 'receipt payload', payload: { ...receipt.payload, receipt: envelope('{"accepted":false,"childId":"actual-host-child-1"}') } },
    { name: 'receipt schema', payload: { ...receipt.payload, receipt: { ...receipt.payload.receipt, schema: { id: 'other-receipt@2', hash: digest('other receipt schema') } } } },
  ]
  for (const item of changed) {
    await t.test('WHAT[sphinx-v2-010] refuses a changed ' + item.name + ' without replacing the original receipt', async () => {
      await withStore(async ({ open, close }) => {
        const writer = open()
        const previous = await append(writer, batch(inquiry, 'seed', [...seed(), request, receipt]))
        expectRejected(writer, previous, [body('DispatchReceiptRecorded', item.payload)])
        const bound = current(writer, inquiry)
        assert.deepEqual(mustOk(bound).physicalBindings, [{ key: 'dispatch-1', value: { request: request.payload, receipt: receipt.payload } }])
        close(writer)
        assert.deepEqual(current(open(), inquiry), bound)
      })
    })
  }
})

test('WHAT[sphinx-v2-010] terminal inquiry admission rejects new dispatch facts including event-level replay', async t => {
  for (const event of [request, receipt]) {
    await t.test('WHAT[sphinx-v2-010] terminal inquiry refuses ' + event.case, async () => {
      await withStore(async ({ open }) => {
        const writer = open()
        const terminal = await append(writer, batch(inquiry, 'seed', [
          ...seed(), request, receipt, body('InquiryCancelled', { reason: 'settled' }),
        ]))
        expectRejected(writer, terminal, [event])
        assert.deepEqual(mustOk(current(writer, inquiry)).physicalBindings, [{ key: 'dispatch-1', value: { request: request.payload, receipt: receipt.payload } }])
      })
    })
  }
})

test.todo('WHAT[sphinx-v2-010] actual durable append failure prevents Host dispatch and receipt-loss recovery reconciles the same intent without creating a second child')
