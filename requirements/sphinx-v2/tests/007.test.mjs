import assert from 'node:assert/strict'
import test from 'node:test'
import {
  store, persistence, digest, body, envelope, createdBody, work, transition,
  batch, append, current, mustOk, withStore,
} from './persistence-support.mjs'
import {
  interpretationInquiry, interpretationPending, interpretationApplied,
  interpretationFailed, interpretationRunningBodies, interpretationSeedBodies,
  rawObservation, interpretationRecord, pendingInterpretationRecord,
  expectInterpretationRefusal,
} from './interpretation-support.mjs'

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

test('WHAT[sphinx-v2-007] InterpretationPending requires an accepted observation and its exact work and attempt', async t => {
  const invalid = [
    { name: 'missing observation', fields: { observationId: 'missing-observation' }, message: /observation/i },
    { name: 'missing work', fields: { workId: 'missing-work' }, message: /work/i },
    { name: 'different existing work', fields: { workId: 'other-work' }, message: /work/i },
    { name: 'different attempt', fields: { attempt: 2 }, message: /attempt/i },
  ]
  for (const scenario of invalid) {
    await t.test('WHAT[sphinx-v2-007] refuses a pending interpretation with ' + scenario.name, async () => {
      await withStore(async ({ open, close, commonDir }) => {
        const writer = open()
        const previous = await append(writer, batch(interpretationInquiry, 'accepted-without-pending', [
          ...interpretationRunningBodies(), rawObservation,
          body('WorkPlanned', { work: [{ ...work('other-work'), roundId: null }] }),
        ]))
        const before = current(writer, interpretationInquiry)
        assert.deepEqual(mustOk(before).interpretations, [])
        expectInterpretationRefusal(writer, previous, [
          body('CancelRequested', { reason: 'this valid prefix must remain private' }),
          body('InterpretationPending', { ...interpretationPending.payload, ...scenario.fields }),
        ], commonDir, scenario.message)
        close(writer)
        assert.deepEqual(current(open(), interpretationInquiry), before)
      })
    })
  }
})

test('WHAT[sphinx-v2-007] Applied and Failed require an explicitly recorded Pending rather than creating one', async t => {
  for (const outcome of [interpretationApplied, interpretationFailed]) {
    await t.test('WHAT[sphinx-v2-007] refuses ' + outcome.case + ' without pending', async () => {
      await withStore(async ({ open, close, commonDir }) => {
        const writer = open()
        const previous = await append(writer, batch(interpretationInquiry, 'accepted-without-pending', [
          ...interpretationRunningBodies(), rawObservation,
        ]))
        const before = current(writer, interpretationInquiry)
        expectInterpretationRefusal(writer, previous, [
          body('CancelRequested', { reason: 'do not publish this prefix' }), outcome,
        ], commonDir, /interpretation|pending/i)
        assert.deepEqual(mustOk(current(writer, interpretationInquiry)).interpretations, [])
        close(writer)
        assert.deepEqual(current(open(), interpretationInquiry), before)
      })
    })
  }
})

test('WHAT[sphinx-v2-007] ResultAccepted before Pending supplies the reference in one atomic batch without placeholders', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const previous = await append(writer, batch(interpretationInquiry, 'running-work', interpretationRunningBodies()))
    const accepted = await append(writer, batch(interpretationInquiry, 'accept-then-pending', [
      rawObservation, interpretationPending,
    ], previous))
    const live = current(writer, interpretationInquiry)
    assert.deepEqual(interpretationRecord(live), pendingInterpretationRecord)
    assert.deepEqual(mustOk(live).observations, [{ key: rawObservation.payload.observationId, value: rawObservation.payload }])
    assert.deepEqual(store.read(writer, accepted.id).payload.events, [rawObservation, interpretationPending])
    close(writer)
    assert.deepEqual(current(open(), interpretationInquiry), live)
  })
})

test('WHAT[sphinx-v2-007] Pending before ResultAccepted refuses the whole batch instead of reordering facts', async () => {
  await withStore(async ({ open, close, commonDir }) => {
    const writer = open()
    const previous = await append(writer, batch(interpretationInquiry, 'running-work', interpretationRunningBodies()))
    const before = current(writer, interpretationInquiry)
    expectInterpretationRefusal(writer, previous, [interpretationPending, rawObservation], commonDir, /observation/i)
    assert.deepEqual(mustOk(current(writer, interpretationInquiry)).observations, [])
    assert.deepEqual(mustOk(current(writer, interpretationInquiry)).interpretations, [])
    close(writer)
    assert.deepEqual(current(open(), interpretationInquiry), before)
  })
})

test('WHAT[sphinx-v2-007] an existing Pending identity cannot silently change its work or attempt', async t => {
  for (const [name, fields] of [['work', { workId: 'other-work' }], ['attempt', { attempt: 2 }]]) {
    await t.test('WHAT[sphinx-v2-007] refuses a changed pending ' + name + ' without replacing its original reference', async () => {
      await withStore(async ({ open, close, commonDir }) => {
        const writer = open()
        const previous = await append(writer, batch(interpretationInquiry, 'pending-observation', [
          ...interpretationSeedBodies(),
          body('WorkPlanned', { work: [{ ...work('other-work'), roundId: null }] }),
        ]))
        const before = current(writer, interpretationInquiry)
        expectInterpretationRefusal(writer, previous, [
          body('CancelRequested', { reason: 'the invalid suffix must reject this prefix' }),
          body('InterpretationPending', { ...interpretationPending.payload, ...fields }),
        ], commonDir, /interpretation|pending|work|attempt/i)
        assert.deepEqual(interpretationRecord(current(writer, interpretationInquiry)), pendingInterpretationRecord)
        close(writer)
        assert.deepEqual(current(open(), interpretationInquiry), before)
      })
    })
  }
})
