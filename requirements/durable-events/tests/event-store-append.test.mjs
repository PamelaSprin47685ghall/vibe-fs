// FROZEN — 2026-08-14. Local EventStore append contract; online Git store removed.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import path from 'node:path'
import test from 'node:test'
import { eventId, idValue, listItems, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const Domain = await import('../../../dist/Persistence/EventStore/Model.js')
const streamId = (v) => Domain.EventStreamIdModule_create(v)
const id = (n) => n.toString(16).padStart(40, '0')
const event = (n, parents = [], type = 'JobRequested', payload = { n }) => new Domain.EventEnvelope(
  eventId(id(n)), streamId('append/proof'), type, toList(parents.map((p) => eventId(id(p)))), payload, toList([]),
)

test('WHAT[DURABLE-EVENTS-001] append_commits_complete_canonical_line_then_updates_Current', async () => {
  const local = createLocalEventStore({ writerId: 'append-proof' })
  try {
    const e = event(1)
    assert.equal(resultOf(await local.store.Append(toList([e]))).ok, true)
    const found = local.store.TryEvent(e.EventId)
    assert.equal(idValue.event(found.EventId), id(1))
    const text = await readFile(path.join(local.commonDir, 'wanxiang', 'events', 'append-proof.ndjson'), 'utf8')
    assert.equal(text.endsWith('\n'), true)
    assert.equal(text.trim().split('\n').length, 1)
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-021] semantic_failure_writes_cut_tail_reset_and_the_same_feature_can_succeed_next', async () => {
  const local = createLocalEventStore({ writerId: 'semantic-cut-proof' })
  try {
    const bad = event(10, [], 'InspectorCaseCaptured', {})
    const first = resultOf(await local.store.Append(toList([bad])))
    assert.equal(first.ok, true, 'semantic failure is durable, not a storage append failure')
    const cuts = listItems(first.value.Cuts)
    assert.equal(cuts.length, 1)
    assert.equal(idValue.event(cuts[0].FailedEventId), id(10))
    assert.equal(cuts[0].Rule, 'Casebook')

    const file = path.join(local.commonDir, 'wanxiang', 'events', 'semantic-cut-proof.ndjson')
    const afterBad = (await readFile(file, 'utf8')).trim().split('\n').map(JSON.parse)
    assert.equal(afterBad.length, 2, 'bad fact and reset fact are one durable append')
    assert.equal(afterBad[0].event_type, 'InspectorCaseCaptured')
    assert.equal(afterBad[1].event_type, 'ProjectionCutTail')
    assert.equal(afterBad[1].payload.failed_event_id, id(10))
    assert.equal(afterBad[1].payload.rule, 'Casebook')

    const good = event(11, [10], 'InspectorCaseAccessed', { session_id: 'case-after-cut' })
    const second = resultOf(await local.store.Append(toList([good])))
    assert.equal(second.ok, true)
    assert.equal(listItems(second.value.Cuts).length, 0, 'cut is self-limited; future feature use retries normally')

    const reopened = createLocalEventStore({ commonDir: local.commonDir, writerId: 'semantic-cut-reopen' })
    try {
      assert.equal(idValue.event(reopened.store.TryEvent(good.EventId).EventId), id(11), 'replay preserves bad → reset → good timeline')
    } finally {
      reopened.close()
    }
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_missing_parent_without_writing_bytes', async () => {
  const local = createLocalEventStore({ writerId: 'missing-parent-proof' })
  try {
    const file = path.join(local.commonDir, 'wanxiang', 'events', 'missing-parent-proof.ndjson')
    assert.equal(existsSync(file), false)
    const r = resultOf(await local.store.Append(toList([event(2, [99])])))
    assert.equal(r.ok, false)
    assert.equal(existsSync(file), false, 'rejected structural append creates no writer file')
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_cycle_in_one_batch_before_durability', async () => {
  const local = createLocalEventStore({ writerId: 'cycle-proof' })
  try {
    const a = event(1, [2])
    const b = event(2, [1])
    const r = resultOf(await local.store.Append(toList([a, b])))
    assert.equal(r.ok, false)
    const file = path.join(local.commonDir, 'wanxiang', 'events', 'cycle-proof.ndjson')
    assert.equal(existsSync(file), false, 'rejected structural append creates no writer file')
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_unknown_event_type_fail_closed', async () => {
  const local = createLocalEventStore()
  try {
    assert.equal(resultOf(await local.store.Append(toList([event(1, [], 'UnknownFutureEvent')]))).ok, false)
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-004] append_task_does_not_return_until_the_cross_process_store_lock_is_released', async () => {
  const local = createLocalEventStore({ writerId: 'lock-release-proof' })
  try {
    assert.equal(resultOf(await local.store.Append(toList([event(1)]))).ok, true)
    assert.equal(
      existsSync(path.join(local.commonDir, 'wanxiang.lock')),
      false,
      'Append completion must mean proper-lockfile has already released its physical gate',
    )
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-017] append_cost_contract_is_independent_of_history_and_EventId_distribution', async () => {
  const source = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/Store.fs', import.meta.url), 'utf8')
  const log = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source + log, /SegmentMaxBytes|EventIdShard|index\/|WriteTree|CompareAndSwapRef|materializeSnapshot/)
  assert.match(log, /appendFileSync|AppendAllText/)
})
