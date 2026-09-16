import assert from 'node:assert/strict'
import test from 'node:test'
import * as canonicalJson from '../../../dist/Persistence/EventStore/CanonicalJsonSurface.js'
import * as utf8Codec from '../../../dist/Persistence/EventStore/Utf8CodecSurface.js'
import * as codecSurface from '../../../dist/Persistence/EventStore/CodecSurface.js'

test('WHAT[DURABLE-EVENTS-003] canonical_bytes_are_utf8_json_plus_single_LF_with_sorted_keys', () => {
  const json = canonicalJson.stringifyCanonical({ b: 2, a: 1 })
  assert.equal(json, '{"a":1,"b":2}\n')
})

test('WHAT[DURABLE-EVENTS-003] event payload keys follow Unicode code-point order without integer-key reordering', () => {
  const json = canonicalJson.stringifyCanonical({ '10': 'ten', '2': 'two', 'a': 'alpha' })
  assert.equal(json, '{"10":"ten","2":"two","a":"alpha"}\n')
})

test('WHAT[DURABLE-EVENTS-003] canonical JSON orders numeric-looking and non-BMP keys by Unicode code point', () => {
  const json = canonicalJson.stringifyCanonical({ '🍕': 'pizza', '100': 'num', 'apple': 'fruit' })
  const keys = Object.keys(JSON.parse(json))
  assert.deepEqual(keys, ['100', 'apple', '🍕'])
})

test('WHAT[DURABLE-EVENTS-003] canonical JSON preserves JSON sparse-array null semantics', () => {
  const arr = []
  arr[2] = 'x'
  const json = canonicalJson.stringifyCanonical({ list: arr })
  assert.equal(json, '{"list":[null,null,"x"]}\n')
})

test('WHAT[DURABLE-EVENTS-003] invalid UTF-8 bytes fail closed before canonical JSON decoding', () => {
  assert.throws(() => utf8Codec.decodeCanonicalUtf8(Buffer.from([0xff, 0xfe])), /invalid UTF-8/)
})

test('WHAT[DURABLE-EVENTS-003] UTF-8 BOM bytes are rejected rather than stripped by the decoder', () => {
  assert.throws(() => utf8Codec.decodeCanonicalUtf8(Buffer.from([0xef, 0xbb, 0xbf, 0x7b, 0x7d])), /UTF-8 BOM rejected/)
})
