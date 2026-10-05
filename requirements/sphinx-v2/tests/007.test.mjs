import assert from 'node:assert/strict'
import test from 'node:test'
import {
  store, persistence, digest, body, envelope, createdBody, work, transition,
  batch, append, current, mustOk, withStore,
} from './persistence-support.mjs'

const inquiry = 'dispatch-round-inquiry'
const workId = 'round-work'
const roundId = 'round-1'
const requested = spec => body('DispatchRequested', {
  work: spec, dispatchIntentId: 'round-intent',
  publicEnvelope: envelope('{"question":"round reference"}'),
  privateTicket: envelope('{"workToken":"round-work:1:logical"}'),
})
const roundOpened = body('RoundOpened', { roundId, scopeId: 'scope-1', expectedWork: [workId] })
const readyBodies = spec => [
  body('WorkPlanned', { work: [spec] }),
  transition(workId, 'Planned', { case: 'Ready' }),
  body('BudgetReserved', {
    reservation: { workId, attempt: 1, resources: spec.reserved, moneyMinor: null },
    renderReserve: [{ key: 'calls', value: 1 }],
  }),
]
const expectRoundRefusal = (writer, previous, events) => {
  const before = current(writer, inquiry)
  const result = persistence.prepareTransition(writer, digest, batch(inquiry, 'refused-round', events, previous))
  assert.equal(result.ok, false, 'an absent round cannot be filled in or used by a dispatch')
  assert.equal(result.error.code, 'TRANSITION_REJECTED')
  assert.match(result.error.message, /round/)
  assert.equal(Object.hasOwn(result, 'value'), false)
  assert.deepEqual(current(writer, inquiry), before, 'the valid prefix and dispatch must remain unpublished')
  assert.deepEqual(store.heads(writer, previous.stream), [previous.id])
}

test('WHAT[sphinx-v2-007] a fresh dispatch with a missing Some round refuses its whole batch without creating a placeholder', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const spec = work(workId)
    const previous = await append(writer, batch(inquiry, 'existing-work', [
      createdBody('missing round reference'), ...readyBodies(spec),
    ]))
    assert.deepEqual(mustOk(current(writer, inquiry)).rounds, [])
    expectRoundRefusal(writer, previous, [
      body('CancelRequested', { reason: 'a valid prefix must not become Current' }), requested(spec),
    ])
    const unchanged = current(writer, inquiry)
    assert.deepEqual(mustOk(unchanged).rounds, [])
    assert.deepEqual(mustOk(unchanged).physicalBindings, [])
    assert.deepEqual(mustOk(unchanged).status, { case: 'Active' })
    close(writer)
    assert.deepEqual(current(open(), inquiry), unchanged)
  })
})

test('WHAT[sphinx-v2-007] an independent work with RoundId None records its intent without inventing a round and survives cold replay', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const spec = { ...work(workId), roundId: null }
    const request = requested(spec)
    const encoded = await append(writer, batch(inquiry, 'independent-work', [
      createdBody('independent work'), ...readyBodies(spec), request,
    ]))
    const live = current(writer, inquiry)
    assert.deepEqual(mustOk(live).rounds, [])
    assert.deepEqual(mustOk(live).physicalBindings, [{ key: 'round-intent', value: { request: request.payload, receipt: null } }])
    assert.equal(mustOk(live).work[0].value.spec.roundId, null)
    assert.deepEqual(store.read(writer, encoded.id).payload.events.at(-1), request)
    close(writer)
    assert.deepEqual(current(open(), inquiry), live)
  })
})

test('WHAT[sphinx-v2-007] RoundOpened before DispatchRequested in the same atomic batch supplies the explicit round reference', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const spec = work(workId)
    const creation = await append(writer, batch(inquiry, 'create', [createdBody('build then refer')]))
    const request = requested(spec)
    const encoded = await append(writer, batch(inquiry, 'open-and-dispatch', [
      roundOpened, ...readyBodies(spec), request,
    ], creation))
    const live = current(writer, inquiry)
    assert.equal(mustOk(live).revision, '1')
    assert.equal(mustOk(live).eventHead, encoded.id)
    assert.equal(mustOk(live).rounds.length, 1)
    assert.equal(mustOk(live).rounds[0].key, roundId)
    assert.deepEqual(mustOk(live).physicalBindings, [{ key: 'round-intent', value: { request: request.payload, receipt: null } }])
    assert.deepEqual(store.read(writer, encoded.id).payload.events, [roundOpened, ...readyBodies(spec), request])
    close(writer)
    assert.deepEqual(current(open(), inquiry), live)
  })
})

test('WHAT[sphinx-v2-007] DispatchRequested before RoundOpened is refused without reordering or publishing any part of the batch', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const spec = work(workId)
    const creation = await append(writer, batch(inquiry, 'create', [createdBody('a future round is not present')]))
    expectRoundRefusal(writer, creation, [...readyBodies(spec), requested(spec), roundOpened])
    const unchanged = current(writer, inquiry)
    assert.deepEqual(mustOk(unchanged).work, [])
    assert.deepEqual(mustOk(unchanged).reservations, [])
    assert.deepEqual(mustOk(unchanged).rounds, [])
    assert.deepEqual(mustOk(unchanged).physicalBindings, [])
    assert.equal(mustOk(unchanged).revision, '0')
    close(writer)
    assert.deepEqual(current(open(), inquiry), unchanged)
  })
})
