import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const event = ({
  id = '1111111111111111111111111111111111111111',
  payload = { state: 'open' },
} = {}) => ({
  id,
  stream: 'proof/canonical-codec-slice',
  type: 'JobRequested',
  parents: [],
  payload,
  payloadRefs: [],
})

test('WHAT[DURABLE-EVENTS-023] canonical codec surface keeps encode decode UTF-8 identity and merge in one fail-closed protocol', () => {
  const left = event()
  const same = event()
  const collision = event({ payload: { state: 'closed' } })
  const distinct = event({ id: '2222222222222222222222222222222222222222' })
  const canonical = eventCodec.encode(left)

  assert.deepEqual(eventCodec.decode(canonical), { ok: true, event: left })
  assert.deepEqual(eventCodec.decodeUtf8Text(Buffer.from(canonical)), { ok: true, text: canonical })
  assert.deepEqual(eventCodec.decodeUtf8(Buffer.from(canonical)), { ok: true, event: left })

  assert.deepEqual(eventCodec.checkIdentity(left, same), { ok: true })
  assert.deepEqual(eventCodec.checkIdentity(left, collision), {
    ok: false,
    error: { code: 'IdentityCollision', eventId: left.id },
  })
  assert.deepEqual(eventCodec.checkIdentity(left, distinct), { ok: true })

  assert.deepEqual(eventCodec.mergeByIdentity([left, same, distinct]), {
    ok: true,
    events: [left, distinct],
  })
  assert.deepEqual(eventCodec.mergeByIdentity([left, collision, distinct]), {
    ok: false,
    error: { code: 'IdentityCollision', eventId: left.id },
  })

  const nonCanonical = canonical.replace('"event_id"', '"stream_id"').replace(/,"stream_id":"[^"]+"/, `,"event_id":"${left.id}"`)
  assert.deepEqual(eventCodec.decode(nonCanonical), {
    ok: false,
    error: { code: 'NonCanonical', reason: 'event bytes are not §5.0 canonical' },
  })
  const invalidUtf8 = Buffer.from([0xc3, 0x20])
  const invalidUtf8Error = {
    ok: false,
    error: { code: 'NonCanonical', reason: 'event bytes are not valid UTF-8' },
  }
  assert.deepEqual(eventCodec.decodeUtf8Text(invalidUtf8), invalidUtf8Error)
  assert.deepEqual(eventCodec.decodeUtf8(invalidUtf8), invalidUtf8Error)
})

test('WHAT[DURABLE-EVENTS-024] semantic cut fatal requires settlement and one injected physical fuse', () => {
  assertFatalBoundary('durable-events')
})
