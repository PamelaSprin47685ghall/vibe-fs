import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

const event = {
  id: '757466382d62797465732d6172652d6964656e7469',
  stream: 'proof/utf8',
  type: 'JobRequested',
  parents: [],
  payload: { text: 'é' },
  payloadRefs: [],
}

const invalidUtf8Event = () => {
  const bytes = Buffer.from(eventCodec.encode(event))
  const continuation = bytes.indexOf(0xa9)
  assert.notEqual(continuation, -1)
  bytes[continuation] = 0x20
  return bytes
}

test('WHAT[DURABLE-EVENTS-003] invalid UTF-8 bytes fail closed before canonical JSON decoding', () => {
  const decoded = eventCodec.decodeUtf8(invalidUtf8Event())

  assert.equal(decoded.ok, false)
  assert.equal(decoded.error.code, 'NonCanonical')
  assert.match(decoded.error.reason, /not valid UTF-8/)
})

test('WHAT[DURABLE-EVENTS-003] UTF-8 BOM bytes are rejected rather than stripped by the decoder', () => {
  const decoded = eventCodec.decodeUtf8(Buffer.concat([
    Buffer.from([0xef, 0xbb, 0xbf]),
    Buffer.from(eventCodec.encode(event)),
  ]))

  assert.equal(decoded.ok, false)
  assert.equal(decoded.error.code, 'NonCanonical')
  assert.match(decoded.error.reason, /must not contain a BOM/)
})

test('WHAT[DURABLE-EVENTS-007] local writer boot rejects invalid UTF-8 without replacement decoding', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-invalid-utf8-'))
  const commonDir = join(root, '.git')
  const eventsDir = join(commonDir, 'wanxiang', 'events')
  mkdirSync(eventsDir, { recursive: true })
  writeFileSync(join(eventsDir, 'broken.ndjson'), invalidUtf8Event())

  try {
    assert.throws(() => eventStore.create(commonDir, 'new-writer'), /not valid UTF-8/)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
