import assert from 'node:assert/strict'
import { existsSync, readFileSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import path from 'node:path'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const id = (n) => n.toString(16).padStart(40, '0')

const canonicalize = (value) => {
  if (Array.isArray(value)) return value.map(canonicalize)
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonicalize(value[key])]))
  }
  return value
}

const canonicalEventLine = (event) => `${JSON.stringify(canonicalize(event))}\n`

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
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'append-proof')
  try {
    const e = event(1)
    const r = await eventStore.append(store, [e])
    assert.equal(r.ok, true)
    const found = eventStore.read(store, id(1))
    assert.ok(found)
    assert.equal(found.id, id(1))
    assert.deepEqual(found.payload, e.payload, 'read returns the event payload, not the canonical envelope')
    const text = await readFile(path.join(dir, 'wanxiang', 'events', 'append-proof.ndjson'), 'utf8')
    assert.equal(text.endsWith('\n'), true)
    assert.equal(text.trim().split('\n').length, 1)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-013] physical append failure leaves event and structural Current unchanged', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'append-failure-proof')
  try {
    const wanxiangDir = path.join(dir, 'wanxiang')
    mkdirSync(wanxiangDir, { recursive: true })
    writeFileSync(path.join(wanxiangDir, 'events'), 'not a directory')

    const rejected = await eventStore.append(store, [event(3)])

    assert.equal(rejected.ok, false)
    assert.equal(rejected.error.code, 'AppendFailed')
    assert.equal(eventStore.read(store, id(3)), null, 'failed durability must not publish the prepared event')
    assert.equal(eventStore.head(store, 'append/proof'), null, 'failed durability must not advance Current')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-021] semantic_failure_writes_cut_tail_reset_and_the_same_feature_can_succeed_next', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'semantic-cut-proof')
  try {
    const bad = event(10, [], 'InspectorCaseCaptured', {})
    const first = await eventStore.append(store, [bad])
    assert.equal(first.ok, true, 'semantic failure is durable, not a storage append failure')
    assert.equal(first.cuts.length, 1)
    assert.equal(first.cuts[0].failedEventId, id(10))
    assert.equal(first.cuts[0].rule, 'Casebook')

    const file = path.join(dir, 'wanxiang', 'events', 'semantic-cut-proof.ndjson')
    const afterBad = (await readFile(file, 'utf8')).trim().split('\n').map(JSON.parse)
    assert.equal(afterBad.length, 2, 'bad fact and reset fact are one durable append')
    assert.equal(afterBad[0].event_type, 'InspectorCaseCaptured')
    assert.equal(afterBad[1].event_type, 'ProjectionCutTail')
    assert.equal(afterBad[1].payload.failed_event_id, id(10))
    assert.equal(afterBad[1].payload.rule, 'Casebook')

    const good = event(11, [10], 'InspectorCaseAccessed', { session_id: 'case-after-cut' })
    const second = await eventStore.append(store, [good])
    assert.equal(second.ok, true)
    assert.equal(second.cuts.length, 0, 'cut is self-limited; future feature use retries normally')

    const reopened = eventStore.create(dir, 'semantic-cut-reopen')
    try {
      const replayed = eventStore.read(reopened, id(11))
      assert.ok(replayed)
      assert.equal(replayed.id, id(11), 'replay preserves bad → reset → good timeline')
    } finally {
      eventStore.dispose(reopened)
    }
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-021] an uncut historical Journal fault suppresses only its own journal stream', async () => {
  const dir = withTemp((base) => base)
  const eventsDir = path.join(dir, 'wanxiang', 'events')
  mkdirSync(eventsDir, { recursive: true })

  const incumbencyOpened = ({ session, inc, seq, id }) =>
    canonicalEventLine({
      event_id: id,
      event_type: 'JournalEnvelope',
      parents: [],
      payload: {
        EventId: ['EventId', id],
        Fact: [
          'Agent',
          [
            'Relay',
            [
              'TransactionCommitted',
              {
                RoadId: ['RoadId', session],
                Transaction: [
                  'RelayTransaction',
                  [
                    [
                      'RoadOpened',
                      ['RoadId', session],
                      ['AuthorityRevision', `rev-${session}`],
                      ['PhysicalUserMessageId', `user-${session}`],
                    ],
                    ['IncumbencyOpened', ['IncumbencyId', inc], ['WorkspaceSnapshotId', 'snapshot-root']],
                  ],
                ],
              },
            ],
          ],
        ],
        LocalSeq: ['LocalSeq', String(seq)],
        // This proof is about Journal fault-scope isolation, not writer retention.
        // A fixed far-future observation keeps the writer retained without reading
        // the wall clock or coupling the test to the 24h physical retention law.
        ObservedAt: `9999-01-01T00:00:0${seq}.000+00:00`,
        RuntimeId: ['RuntimeId', 'rt_journal_fault_scope'],
        Stream: ['Session', ['SessionId', session]],
      },
      payload_refs: [],
      stream_id: `journal/session/${session}`,
    })

  const historical = [
    incumbencyOpened({ session: 'ses_fault_a', inc: 'inc_a1', seq: 1, id: '1'.repeat(40) }),
    // A second open on the same road while active is deliberately invalid.
    incumbencyOpened({ session: 'ses_fault_a', inc: 'inc_a2', seq: 2, id: '2'.repeat(40) }),
    incumbencyOpened({ session: 'ses_healthy_b', inc: 'inc_b1', seq: 3, id: '3'.repeat(40) }),
  ]

  writeFileSync(path.join(eventsDir, 'historical.ndjson'), historical.join(''))

  try {
    const booted = await journal.JournalSurface_bootWithWriterId(
      dir,
      'reopen-proof',
      'rt_reopen',
      4242,
      '2026-01-02T00:00:00Z',
    )
    assert.equal(booted.ok, true, `historical replay should boot: ${JSON.stringify(booted.error)}`)
    assert.equal(journal.JournalSurface_hasSession(booted.journal, 'ses_fault_a'), true)
    assert.equal(
      journal.JournalSurface_hasSession(booted.journal, 'ses_healthy_b'),
      true,
      'a semantic fault in one Journal stream must not black out later independent session streams',
    )
    journal.JournalSurface_dispose(booted.journal)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-021] every_live_semantic_cut_boundary_trips_process_fatal_instead_of_returning_a_normal_error', async () => {
  const { spawnSync } = await import('node:child_process')
  const { setFatalTripHandler: setCasebookTrip, appendCaptured } = await import('../../../dist/Repository/Knowledge/Casebook/Store.js')
  const { Case } = await import('../../../dist/Repository/Knowledge/Casebook/Model.js')
  const { setFatalTripHandler: setStrengthTrip } = await import('../../../dist/Strength/Persistence/Durability.js')
  const { StrengthPreparedRequest } = await import('../../../dist/Strength/Persistence/DurabilityPort.js')
  const { SessionIdModule_create, StrengthDecisionIdModule_create, ProviderRunIdentityModule_create } = await import('../../../dist/Foundation/Identity.js')
  const { StrengthBudget } = await import('../../../dist/Strength/Budget.js')
  const { StrengthFrameBundle } = await import('../../../dist/Strength/Frame.js')
  const { ofArray } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/List.js')
  const strengthSurface = await import('../../../dist/Persistence/EventStore/StrengthSurface.js')

  // 1. Casebook semantic cut trips the injected fatal handler with typed "casebook-semantic-cut"
  const casebookDir = withTemp((base) => base)
  const casebookStore = eventStore.create(casebookDir, 'cb-cut-test')
  try {
    let casebookTrips = []
    setCasebookTrip((op, reason) => casebookTrips.push({ op, reason }))
    const badCase = new Case(undefined, 'q', 'a', ofArray([]), 0)
    const cbResult = await appendCaptured(casebookStore.store, badCase)
    assert.equal(cbResult.tag, 1, 'Casebook semantic cut returns Error result')
    assert.equal(casebookTrips.length, 1)
    assert.equal(casebookTrips[0].op, 'casebook-semantic-cut')
  } finally {
    setCasebookTrip(null)
    eventStore.dispose(casebookStore)
    rmSync(casebookDir, { recursive: true, force: true })
  }

  // 2. Strength semantic cut trips the injected fatal handler with typed "strength-prepared-semantic-cut"
  const strengthDir = withTemp((base) => base)
  const strengthStore = eventStore.create(strengthDir, 'str-cut-test')
  try {
    let strengthTrips = []
    setStrengthTrip((op, reason) => strengthTrips.push({ op, reason }))
    const durability = strengthSurface.durability(strengthStore)
    const req = new StrengthPreparedRequest(
      SessionIdModule_create('ses_1'),
      StrengthDecisionIdModule_create('dec_1'),
      ProviderRunIdentityModule_create('run_1'),
      SessionIdModule_create('rep_1'),
      new StrengthBudget(100, 10),
      'anchor',
      new StrengthFrameBundle('digest1', 10, ofArray([])),
    )
    const strResult = await durability.PublishPrepared(req)
    assert.equal(strResult.tag, 1, 'Strength semantic cut returns Rejected result')
    assert.equal(strengthTrips.length, 1)
    assert.equal(strengthTrips[0].op, 'strength-prepared-semantic-cut')
  } finally {
    setStrengthTrip(null)
    eventStore.dispose(strengthStore)
    rmSync(strengthDir, { recursive: true, force: true })
  }

  // 3. JsTransaction semantic cut in isolated process trips FatalProcess with "js-transaction-semantic-cut" and writes durable cut-tail
  const jsScript = `
    import * as eventStore from './dist/Persistence/EventStore/Surface.js';
    import { JsToolsTransactionStore_appendCommitted } from './dist/Repository/Programming/Js/TransactionStore.js';
    import { JsTransactionIdModule_create } from './dist/Repository/Programming/Js/Transaction.js';
    const storeHandle = eventStore.create(process.argv[1], 'js-writer');
    await JsToolsTransactionStore_appendCommitted(storeHandle.store, JsTransactionIdModule_create(null));
  `
  const jsDir = withTemp((base) => base)
  try {
    const run = spawnSync(process.execPath, ['--input-type=module', '-e', jsScript, jsDir], {
      encoding: 'utf8',
      env: { ...process.env, WANXIANGSHU_NO_FATAL_EXIT: '1' },
    })
    assert.match(run.stderr, /"operation":"js-transaction-semantic-cut"/, 'JsTransaction cut trips FatalProcess with exact operation')
    const ndjson = readFileSync(join(jsDir, 'wanxiang', 'events', 'js-writer.ndjson'), 'utf8').trim().split('\n').map(JSON.parse)
    assert.equal(ndjson.length, 2, 'durable aftermath must persist bad fact and ProjectionCutTail')
    assert.equal(ndjson[0].event_type, 'JsTransactionCommitted')
    assert.equal(ndjson[1].event_type, 'ProjectionCutTail')
  } finally {
    rmSync(jsDir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_missing_parent_without_writing_bytes', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'missing-parent-proof')
  try {
    const file = path.join(dir, 'wanxiang', 'events', 'missing-parent-proof.ndjson')
    assert.equal(existsSync(file), false)
    const r = await eventStore.append(store, [event(2, [99])])
    assert.equal(r.ok, false)
    assert.equal(existsSync(file), false, 'rejected structural append creates no writer file')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_cycle_in_one_batch_before_durability', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'cycle-proof')
  try {
    const a = event(1, [2])
    const b = event(2, [1])
    const r = await eventStore.append(store, [a, b])
    assert.equal(r.ok, false)
    const file = path.join(dir, 'wanxiang', 'events', 'cycle-proof.ndjson')
    assert.equal(existsSync(file), false, 'rejected structural append creates no writer file')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_unknown_event_type_fail_closed', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'unknown-type-proof')
  try {
    const r = await eventStore.append(store, [event(1, [], 'UnknownFutureEvent')])
    assert.equal(r.ok, false)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-004] append_task_does_not_return_until_the_cross_process_store_lock_is_released', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'lock-release-proof')
  try {
    const r = await eventStore.append(store, [event(1)])
    assert.equal(r.ok, true)
    assert.equal(
      existsSync(path.join(dir, 'wanxiang.lock')),
      false,
      'Append release must happen before lock file is removed',
    )
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

