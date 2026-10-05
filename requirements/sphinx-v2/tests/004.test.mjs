import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import {
  body, createdBody, work, batch, append, current, mustOk, withStore,
} from './persistence-support.mjs'

test('WHAT[sphinx-v2-004] provider receipt classification preserves unresolved usage rather than billing it as zero', () => {
  assert.equal(Loop.providerUsageUnresolved(Loop.providerOutcome('', 0n, 0n, 0n, true)), true)
  const settled = Loop.providerOutcome('', 100n, 200n, 3n, false)
  assert.equal(Loop.providerUsageUnresolved(settled), false)
  assert.deepEqual(Loop.providerUsageCounts(settled), [100n, 200n, 3n])
})

const reserved = (id, amount) => body('BudgetReserved', {
  reservation: { workId: id, attempt: 1, resources: [{ key: 'calls', value: amount }], moneyMinor: null },
  renderReserve: [{ key: 'calls', value: 1 }],
})

test('WHAT[sphinx-v2-004] canonical reservations retain each work amount rather than the accumulated pool through cold replay', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    await append(writer, batch('separate-reservations', 'reserve', [
      createdBody('separate reservation ownership'),
      body('WorkPlanned', { work: [work('work-1'), { ...work('work-2'), reserved: [{ key: 'calls', value: 2 }] }] }),
      reserved('work-1', 1), reserved('work-2', 2),
    ]))
    const live = current(writer, 'separate-reservations')
    assert.deepEqual(mustOk(live).reservations, [
      { key: 'work-1|1', value: { workId: 'work-1', attempt: 1, resources: [{ key: 'calls', value: 1 }] } },
      { key: 'work-2|1', value: { workId: 'work-2', attempt: 1, resources: [{ key: 'calls', value: 2 }] } },
    ])
    assert.deepEqual(mustOk(live).settledUsage, [])
    close(writer)
    assert.deepEqual(current(open(), 'separate-reservations'), live)
  })
})

test('WHAT[sphinx-v2-004] reservations that exactly fill the authorized pool do not charge outstanding amounts twice', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const creation = createdBody('exact authorized pool')
    creation.payload.resourceSpecs[0].authorizedLimit = 3
    await append(writer, batch('exact-reservations', 'reserve', [
      creation,
      body('WorkPlanned', { work: [work('work-1'), { ...work('work-2'), reserved: [{ key: 'calls', value: 2 }] }] }),
      reserved('work-1', 1), reserved('work-2', 2),
    ]))
    const live = current(writer, 'exact-reservations')
    const outstanding = mustOk(live).reservations.flatMap(entry => entry.value.resources)
      .reduce((amount, entry) => amount + entry.value, 0)
    assert.equal(outstanding, 3)
    assert.deepEqual(mustOk(live).settledUsage, [])
    close(writer)
    assert.deepEqual(current(open(), 'exact-reservations'), live)
  })
})

test.todo('WHAT[sphinx-v2-004] actual durable reservations survive unresolved usage and settle cancellation duplicate calls and observed overrun exactly once')
