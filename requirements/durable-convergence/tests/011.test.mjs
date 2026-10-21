import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, utimesSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import * as retention from '../../../dist/Persistence/EventStore/RetentionSurface.js'
import * as store from '../../../dist/Persistence/EventStore/Surface.js'
import * as codec from '../../../dist/Persistence/EventStore/CodecSurface.js'

const day = 86400000
const id = n => n.toString(16).padStart(40, '0')
const event = (n, type = 'JobRequested', payload = {}, parents = []) => ({
  id: id(n), stream: 'proof/retention', type, parents, payload, payloadRefs: [],
})
const journal = (n, at) => event(n, 'JournalEnvelope', { ObservedAt: at })
const cut = (n, parent) => event(n, 'ProjectionCutTail', {
  rule: 'Journal', failed_event_id: id(parent), reason: 'retention proof', reset_json: '{}',
}, [id(parent)])
const fixture = t => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-retention-011-'))
  const directory = join(root, 'wanxiangshu', 'events')
  mkdirSync(directory, { recursive: true })
  t.after(() => rmSync(root, { recursive: true, force: true }))
  return { root, directory }
}
const write = ({ directory }, name, facts, at) => {
  const file = join(directory, name)
  writeFileSync(file, typeof facts === 'string' ? facts : facts.map(codec.encode).join(''))
  utimesSync(file, at / 1000, at / 1000)
  return file
}
const disk = ({ directory }) => Object.fromEntries(readdirSync(directory).sort().map(name => {
  const file = join(directory, name)
  return [name, { bytes: readFileSync(file).toString('base64'), mtimeMs: statSync(file).mtimeMs }]
}))

test('WHAT[durable-convergence-011] non-finite cutoffs reject even with no writers instead of hiding the retained union', t => {
  const state = fixture(t)
  for (const invalid of [NaN, Infinity, -Infinity]) {
    assert.throws(() => retention.retainedWriterIdsAt(state.root, invalid), /finite/)
  }
  write(state, 'finite-0000.ndjson', [event(1)], Date.now())
  const before = disk(state)
  for (const invalid of [NaN, Infinity, -Infinity]) {
    assert.throws(() => retention.retainedWriterIdsAt(state.root, invalid), /finite/)
  }
  assert.deepEqual(disk(state), before)
})

test('WHAT[durable-convergence-011] numeric zero ObservedAt cannot hide a fresh writer from the actual EventStore', t => {
  const state = fixture(t)
  const fact = journal(1, 0)
  const now = Date.now()
  write(state, 'numeric-zero-0000.ndjson', [fact], now)
  const before = disk(state)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), ['numeric-zero'])
  const reader = store.create(state.root, 'numeric-zero-reader')
  try {
    assert.deepEqual(store.read(reader, fact.id), fact)
  } finally {
    store.dispose(reader)
  }
  assert.deepEqual(disk(state), before)
})

test('WHAT[durable-convergence-011] numeric future ObservedAt cannot revive an expired writer', t => {
  const state = fixture(t)
  const now = Date.now()
  const fact = journal(1, new Date(now).getUTCFullYear() + 2)
  write(state, 'numeric-future-0000.ndjson', [fact], now - 3 * day)
  const before = disk(state)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), [])
  const reader = store.create(state.root, 'numeric-future-reader')
  try {
    assert.equal(store.read(reader, fact.id), null)
  } finally {
    store.dispose(reader)
  }
  assert.deepEqual(disk(state), before)
})

test('WHAT[durable-convergence-011] UTC calendar boundaries retain today and all of yesterday as whole writer series', t => {
  const state = fixture(t)
  const today = Date.UTC(2026, 9, 21)
  const now = today + 23 * 60 * 60 * 1000
  write(state, 'today-0000.ndjson', [event(1)], today)
  write(state, 'yesterday-0000.ndjson', [event(2)], today - day)
  write(state, 'yesterday-0001.ndjson', [event(3)], today - day)
  write(state, 'expired-0000.ndjson', [event(4)], today - day - 1)
  write(state, 'expired-0001.ndjson', [event(5)], today - day - 1)
  const before = disk(state)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), ['today', 'yesterday'])
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, today + day), ['today'])
  assert.deepEqual(disk(state), before)
})

test('WHAT[durable-convergence-011] valid Journal observation wins over producer mtime in either direction', t => {
  const state = fixture(t)
  const now = Date.now()
  write(state, 'durable-old-0000.ndjson', [journal(1, new Date(now - 3 * day).toISOString())], now)
  write(state, 'durable-new-0000.ndjson', [journal(2, new Date(now).toISOString())], now - 3 * day)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), ['durable-new'])
})

test('WHAT[durable-convergence-011] a cut-tail chain crosses segment seams without refreshing old Journal activity', t => {
  const state = fixture(t)
  const now = Date.now()
  write(state, 'cut-chain-0000.ndjson', [journal(1, new Date(now - 3 * day).toISOString())], now)
  write(state, 'cut-chain-0001.ndjson', [cut(2, 1)], now)
  write(state, 'cut-chain-0002.ndjson', [cut(3, 2)], now)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), [])
  write(state, 'cut-chain-0000.ndjson', [journal(1, new Date(now).toISOString())], now - 3 * day)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), ['cut-chain'])
  assert.deepEqual(retention.writerEventIds(state.root), [{ writerId: 'cut-chain', eventIds: [id(1), id(2), id(3)] }])
})

test('WHAT[durable-convergence-011] silent tails stop before older Journal and use producer activity without modifying it', t => {
  const state = fixture(t)
  const now = Date.now()
  write(state, 'silent-tail-0000.ndjson', [journal(1, new Date(now - 3 * day).toISOString())], now - 3 * day)
  write(state, 'silent-tail-0001.ndjson', [event(2), cut(3, 2)], now)
  const before = disk(state)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), ['silent-tail'])
  assert.deepEqual(disk(state), before)
})

test('WHAT[durable-convergence-011] exhausted cut tails fall back to the newest producer file activity', t => {
  const state = fixture(t)
  const now = Date.now()
  write(state, 'cuts-only-0000.ndjson', [cut(1, 9)], now)
  write(state, 'cuts-only-0001.ndjson', [cut(2, 1)], now - 3 * day)
  assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), ['cuts-only'])
})

test('WHAT[durable-convergence-011] unusable Journal fields use mtime and retained malformed bytes still fail canonical validation', t => {
  const now = Date.now()
  for (const observedAt of [0, 2027, true, null, [], {}, 'not a date']) {
    const state = fixture(t)
    write(state, 'invalid-field-0000.ndjson', [journal(1, observedAt)], now)
    assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), ['invalid-field'])
  }
  for (const bytes of ['null\n', '{bad json}\n', codec.encode(event(1)).slice(0, -1)]) {
    const state = fixture(t)
    write(state, 'malformed-0000.ndjson', bytes, now)
    const before = disk(state)
    assert.throws(() => retention.retainedWriterIdsAt(state.root, now), /Malformed|NonCanonical|incomplete|trailing/i)
    assert.deepEqual(disk(state), before)
    utimesSync(join(state.directory, 'malformed-0000.ndjson'), (now - 3 * day) / 1000, (now - 3 * day) / 1000)
    assert.deepEqual(retention.retainedWriterIdsAt(state.root, now), [])
  }
})
