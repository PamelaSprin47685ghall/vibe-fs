// tests/unit/persist/event-store-fold.test.mjs
// Phase 2 Wave C — §5.1/§5.3 DAG topo fold: StorageInvalid fail-closed + DomainConflict.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, listItems, mapEntries, payloadOf, toList } from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Fold = await import('../../../dist/Infrastructure/Persist/EventStoreFold.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const payloadRef = (v) => Domain.PayloadRefModule_create(v)

const envelope = ({
  id = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
  payloadRefs = [],
} = {}) =>
  new Domain.EventEnvelope(
    eventId(id),
    streamId(stream),
    eventType,
    toList(parents.map(eventId)),
    payload,
    toList(payloadRefs.map(payloadRef)),
  )

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}`)
  return payloadOf(result)
}

const mustErr = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Error', `${label} should be Error`)
  return payloadOf(result)
}

const foldOrderIds = (projection) => listItems(projection.FoldOrder).map((id) => idValue.event(id))

const streamState = (projection, stream) => {
  const entry = mapEntries(projection.Streams).find(([key]) => key === stream)
  assert.ok(entry, `missing stream ${stream}`)
  return entry[1]
}

test('AuthoritativeEventTypes_isKnown_includes_JournalEnvelope_and_Job_types', () => {
  assert.equal(Fold.AuthoritativeEventTypes_isKnown('JournalEnvelope'), true)
  assert.equal(Fold.AuthoritativeEventTypes_isKnown('JobRequested'), true)
  assert.equal(Fold.AuthoritativeEventTypes_isKnown('JobAccepted'), true)
  assert.equal(Fold.AuthoritativeEventTypes_isKnown('JobRejected'), true)
  assert.equal(Fold.AuthoritativeEventTypes_isKnown('JobConflictResolved'), true)
  assert.equal(Fold.AuthoritativeEventTypes_isKnown('TotallyUnknownEventType'), false)
})

test('fold_JournalEnvelope_validates', () => {
  const journal = envelope({
    id: 'dddddddddddddddddddddddddddddddddddddddd',
    stream: 'journal/main',
    eventType: 'JournalEnvelope',
    payload: { kind: 'entry' },
  })
  const projection = mustOk(Fold.EventStoreFold_fold(toList([journal])))
  assert.deepEqual(foldOrderIds(projection), ['dddddddddddddddddddddddddddddddddddddddd'])
  assert.equal(listItems(projection.Conflicts).length, 0)
  const state = streamState(projection, 'journal/main')
  assert.equal(caseOf(state), 'Unique')
})

test('fold_unknown_authoritative_event_type_fail_closed', () => {
  const unknown = envelope({
    id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    eventType: 'TotallyUnknownEventType',
  })
  const err = mustErr(Fold.EventStoreFold_fold(toList([unknown])))
  assert.equal(caseOf(err), 'StorageInvalid')
  assert.equal(caseOf(payloadOf(err)), 'UnknownEventType')
  assert.equal(payloadOf(payloadOf(err)), 'TotallyUnknownEventType')
})

test('fold_missing_parent_fail_closed', () => {
  const child = envelope({
    id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    parents: ['aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'],
  })
  const err = mustErr(Fold.EventStoreFold_fold(toList([child])))
  assert.equal(caseOf(err), 'StorageInvalid')
  assert.equal(caseOf(payloadOf(err)), 'MissingParent')
  assert.equal(idValue.event(payloadOf(payloadOf(err))), 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
})

test('fold_cyclic_parents_fail_closed', () => {
  const a = envelope({
    id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    parents: ['bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'],
  })
  const b = envelope({
    id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    parents: ['aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'],
  })
  const err = mustErr(Fold.EventStoreFold_fold(toList([a, b])))
  assert.equal(caseOf(err), 'StorageInvalid')
  assert.equal(caseOf(payloadOf(err)), 'CyclicParents')
})

test('fold_deterministic_topological_order_with_EventId_tiebreak', () => {
  const root = envelope({ id: '0000000000000000000000000000000000000000' })
  const late = envelope({
    id: 'cccccccccccccccccccccccccccccccccccccccc',
    parents: ['0000000000000000000000000000000000000000'],
    eventType: 'JobAccepted',
  })
  const early = envelope({
    id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    parents: ['0000000000000000000000000000000000000000'],
    eventType: 'JobRejected',
  })

  const orders = [
    [root, late, early],
    [early, root, late],
    [late, early, root],
  ].map((batch) => foldOrderIds(mustOk(Fold.EventStoreFold_fold(toList(batch)))))

  for (const order of orders) {
    assert.deepEqual(order, [
      '0000000000000000000000000000000000000000',
      'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
      'cccccccccccccccccccccccccccccccccccccccc',
    ])
  }
})

test('fold_concurrent_heads_are_DomainConflict_not_StorageInvalid', () => {
  const parent = envelope({ id: '0000000000000000000000000000000000000000' })
  const a1 = envelope({
    id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    parents: ['0000000000000000000000000000000000000000'],
    eventType: 'JobAccepted',
  })
  const b1 = envelope({
    id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    parents: ['0000000000000000000000000000000000000000'],
    eventType: 'JobRejected',
  })

  const projection = mustOk(Fold.EventStoreFold_fold(toList([parent, a1, b1])))
  assert.equal(listItems(projection.Conflicts).length, 1)
  const conflict = listItems(projection.Conflicts)[0]
  assert.equal(caseOf(conflict), 'ConcurrentHeads')
  const [, heads] = payloadOf(conflict)
  const headIds = listItems(heads).map((id) => idValue.event(id)).sort()
  assert.deepEqual(headIds, [
    'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
  ])

  const state = streamState(projection, 'job/main')
  assert.equal(caseOf(state), 'Conflict')
})

test('fold_resolution_with_all_competing_heads_as_parents_leaves_conflict', () => {
  const parent = envelope({ id: '0000000000000000000000000000000000000000' })
  const a1 = envelope({
    id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    parents: ['0000000000000000000000000000000000000000'],
    eventType: 'JobAccepted',
  })
  const b1 = envelope({
    id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    parents: ['0000000000000000000000000000000000000000'],
    eventType: 'JobRejected',
  })
  const resolved = envelope({
    id: 'cccccccccccccccccccccccccccccccccccccccc',
    parents: [
      'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    ],
    eventType: 'JobConflictResolved',
  })

  const projection = mustOk(Fold.EventStoreFold_fold(toList([parent, a1, b1, resolved])))
  assert.equal(listItems(projection.Conflicts).length, 0)
  assert.equal(foldOrderIds(projection).at(-1), 'cccccccccccccccccccccccccccccccccccccccc')

  const state = streamState(projection, 'job/main')
  assert.equal(caseOf(state), 'Unique')
  assert.equal(idValue.event(payloadOf(state)), 'cccccccccccccccccccccccccccccccccccccccc')
})

test('fold_empty_history_ok', () => {
  const projection = mustOk(Fold.EventStoreFold_fold(toList([])))
  assert.deepEqual(foldOrderIds(projection), [])
  assert.equal(listItems(projection.Conflicts).length, 0)
})

test('validate_matches_fold_StorageInvalid', () => {
  const unknown = envelope({ eventType: 'NoSuchType' })
  const foldErr = mustErr(Fold.EventStoreFold_fold(toList([unknown])))
  const validateErr = mustErr(Fold.EventStoreFold_validate(toList([unknown])))
  assert.equal(caseOf(payloadOf(foldErr)), caseOf(payloadOf(validateErr)))
})
