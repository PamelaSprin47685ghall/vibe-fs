// FROZEN — 2026-08-14. Local EventStore append contract; online Git store removed.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { existsSync, readFileSync, mkdtempSync, rmSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import path from 'node:path'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

const id = (n) => n.toString(16).padStart(40, '0')

const withTemp = (fn) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-event-store-append-'))
  return fn(base)
}

const event = (n, parents = [], type = 'JobRequested', payload = { n }) => ({
  id: id(n),
  stream: 'append/proof',
  type,
  parents: parents.map((p) => id(p)),
  payload,
  payloadRefs: [],
})

test('WHAT[DURABLE-EVENTS-001] append_commits_complete_canonical_line_then_updates_Current', async () => {
  const local = withTemp((dir) => eventStore.createLocalStore(dir, 'append-proof'))
  try {
    const e = event(1)
    const r = await eventStore.append(local.store, [e])
    assert.equal(r.ok, true)
    const found = eventStore.tryEvent(local.store, id(1))
    assert.ok(found)
    assert.equal(found.id, id(1))
    const text = await readFile(path.join(local.commonDir, 'wanxiang', 'events', 'append-proof.ndjson'), 'utf8')
    assert.equal(text.endsWith('\n'), true)
    assert.equal(text.trim().split('\n').length, 1)
  } finally {
    rmSync(local.commonDir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-021] semantic_failure_writes_cut_tail_reset_and_the_same_feature_can_succeed_next', async () => {
  const local = withTemp((dir) => eventStore.createLocalStore(dir, 'semantic-cut-proof'))
  try {
    const bad = event(10, [], 'InspectorCaseCaptured', {})
    const first = await eventStore.append(local.store, [bad])
    assert.equal(first.ok, true, 'semantic failure is durable, not a storage append failure')
    assert.equal(first.cuts.length, 1)
    assert.equal(first.cuts[0].failedEventId, id(10))
    assert.equal(first.cuts[0].rule, 'Casebook')

    const file = path.join(local.commonDir, 'wanxiang', 'events', 'semantic-cut-proof.ndjson')
    const afterBad = (await readFile(file, 'utf8')).trim().split('\n').map(JSON.parse)
    assert.equal(afterBad.length, 2, 'bad fact and reset fact are one durable append')
    assert.equal(afterBad[0].event_type, 'InspectorCaseCaptured')
    assert.equal(afterBad[1].event_type, 'ProjectionCutTail')
    assert.equal(afterBad[1].payload.failed_event_id, id(10))
    assert.equal(afterBad[1].payload.rule, 'Casebook')

    const good = event(11, [10], 'InspectorCaseAccessed', { session_id: 'case-after-cut' })
    const second = await eventStore.append(local.store, [good])
    assert.equal(second.ok, true)
    assert.equal(second.cuts.length, 0, 'cut is self-limited; future feature use retries normally')

    const reopened = eventStore.createLocalStore(local.commonDir, 'semantic-cut-reopen')
    try {
      const replayed = eventStore.tryEvent(reopened.store, id(11))
      assert.ok(replayed)
      assert.equal(replayed.id, id(11), 'replay preserves bad → reset → good timeline')
    } finally {
      // reopened shares commonDir; do not remove here
    }
  } finally {
    rmSync(local.commonDir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-021] every_live_semantic_cut_boundary_trips_process_fatal_instead_of_returning_a_normal_error', () => {
  const root = new URL('../../../src/Wanxiangshu/', import.meta.url)
  const sources = {
    agentJournal: readFileSync(new URL('Persistence/Journal/AgentJournal.fs', root), 'utf8'),
    journalWriter: readFileSync(new URL('Persistence/Journal/EventStoreJournalWriter.fs', root), 'utf8'),
    casebook: readFileSync(new URL('Repository/Knowledge/Casebook/Store.fs', root), 'utf8'),
    jsTransactions: readFileSync(new URL('Repository/Programming/Js/TransactionStore.fs', root), 'utf8'),
    strengthDurability: readFileSync(new URL('Strength/Persistence/Durability.fs', root), 'utf8'),
    hostTurn: readFileSync(new URL('OpenCode/Host/HostTurnObserver.fs', root), 'utf8'),
  }

  assert.match(sources.agentJournal, /FatalProcess\.trip\s+"journal-semantic-cut"/)
  assert.match(sources.journalWriter, /FatalProcess\.trip\s+"runtime-started-semantic-cut"/)
  assert.match(sources.casebook, /FatalProcess\.trip\s+"casebook-semantic-cut"/)
  assert.match(sources.jsTransactions, /FatalProcess\.trip\s+"js-transaction-semantic-cut"/)
  assert.match(sources.strengthDurability, /FatalProcess\.trip\s+"strength-prepared-semantic-cut"/)
  assert.match(sources.hostTurn, /SemanticRejected error[\s\S]{0,500}Diagnostic\.fatal\s+"strength-semantic-cut"/)
  assert.doesNotMatch(sources.hostTurn, /SemanticRejected error[\s\S]{0,500}Diagnostic\.emit\s+"strength-semantic-cut"/)
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_missing_parent_without_writing_bytes', async () => {
  const local = withTemp((dir) => eventStore.createLocalStore(dir, 'missing-parent-proof'))
  try {
    const file = path.join(local.commonDir, 'wanxiang', 'events', 'missing-parent-proof.ndjson')
    assert.equal(existsSync(file), false)
    const r = await eventStore.append(local.store, [event(2, [99])])
    assert.equal(r.ok, false)
    assert.equal(existsSync(file), false, 'rejected structural append creates no writer file')
  } finally {
    rmSync(local.commonDir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_cycle_in_one_batch_before_durability', async () => {
  const local = withTemp((dir) => eventStore.createLocalStore(dir, 'cycle-proof'))
  try {
    const a = event(1, [2])
    const b = event(2, [1])
    const r = await eventStore.append(local.store, [a, b])
    assert.equal(r.ok, false)
    const file = path.join(local.commonDir, 'wanxiang', 'events', 'cycle-proof.ndjson')
    assert.equal(existsSync(file), false, 'rejected structural append creates no writer file')
  } finally {
    rmSync(local.commonDir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_unknown_event_type_fail_closed', async () => {
  const local = withTemp((dir) => eventStore.createLocalStore(dir, 'unknown-type-proof'))
  try {
    const r = await eventStore.append(local.store, [event(1, [], 'UnknownFutureEvent')])
    assert.equal(r.ok, false)
  } finally {
    rmSync(local.commonDir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-004] append_task_does_not_return_until_the_cross_process_store_lock_is_released', async () => {
  const local = withTemp((dir) => eventStore.createLocalStore(dir, 'lock-release-proof'))
  try {
    const r = await eventStore.append(local.store, [event(1)])
    assert.equal(r.ok, true)
    assert.equal(
      existsSync(path.join(local.commonDir, 'wanxiang.lock')),
      false,
      'Append release must happen before lock file is removed',
    )
  } finally {
    rmSync(local.commonDir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-017] append_cost_contract_is_independent_of_history_and_EventId_distribution', async () => {
  const source = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/Store.fs', import.meta.url), 'utf8')
  const log = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source + log, /SegmentMaxBytes|EventIdShard|index\/|WriteTree|CompareAndSwapRef|materializeSnapshot/)
  assert.match(log, /appendFileSync|AppendAllText/)
})
