import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

// One of the nine LifecycleEventTypes names the pre-migration kernel persisted
// into the ndjson event stream. It no longer belongs to the authoritative
// vocabulary, so retained lines carrying it must fail closed on reload.
const RETIRED_EVENT_TYPE = 'IncumbencyRetired'

const known = {
  id: 'a'.repeat(40),
  stream: 'durable-events-027/known',
  type: 'JobRequested',
  parents: [],
  payload: { status: 'open' },
  payloadRefs: [],
}

const retired = {
  id: 'b'.repeat(40),
  stream: 'durable-events-027/retired',
  type: RETIRED_EVENT_TYPE,
  parents: [],
  payload: { IncumbencyId: 'inc-retired-proof', Outcome: 'Relayed' },
  payloadRefs: [],
}

test('WHAT[durable-events-027] a retained line of a retired lifecycle event type fails closed on reload; removing it restores loading', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-durable-retired-vocabulary-'))
  try {
    const events = join(root, 'wanxiangshu/events')
    mkdirSync(events, { recursive: true })
    const writer = join(events, 'retained-history.ndjson')
    const knownLine = eventCodec.encode(known)
    const retiredLine = eventCodec.encode(retired)
    assert.equal(JSON.parse(knownLine).event_type, 'JobRequested')
    assert.equal(JSON.parse(retiredLine).event_type, RETIRED_EVENT_TYPE)
    writeFileSync(writer, knownLine + retiredLine)

    assert.throws(
      () => eventStore.create(root, 'retired-vocabulary-reader'),
      /local EventStore boot failed: unknown durable event type during replay: IncumbencyRetired/,
    )
    assert.equal(
      readFileSync(writer, 'utf8'),
      knownLine + retiredLine,
      'a refused reload must not rewrite or drop the retained line',
    )

    // Control: with the retired line removed from the same directory, the same
    // reload path loads the remaining legitimate history.
    writeFileSync(writer, knownLine)
    const handle = eventStore.create(root, 'retired-vocabulary-reader')
    try {
      assert.deepEqual(eventStore.streams(handle), ['durable-events-027/known'])
      const loaded = eventStore.read(handle, known.id)
      assert.equal(loaded.id, known.id)
      assert.equal(loaded.type, 'JobRequested')
    } finally {
      eventStore.dispose(handle)
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
