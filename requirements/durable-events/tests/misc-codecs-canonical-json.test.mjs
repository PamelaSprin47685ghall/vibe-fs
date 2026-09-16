// Split from tests/unit/codec/misc-codecs.test.mjs (cutover Wave 2a); owner: durable-events.
// Canonical JSON codec cluster: key-sorting canonicalization, order-insensitive
// canonical.equality, named-field stripping — the identity protocol of DURABLE-EVENTS-003.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as canonical from '../../../dist/OpenCode/Codec/CanonicalJsonSurface.js'

// ── CanonicalJson ────────────────────────────────────────────────────────────

test('WHAT[DURABLE-EVENTS-003] MISC_canonical_json_sorts_keys_recursively', () => {
  assert.equal(canonical.canonicalJson({ b: 1, a: { d: 4, c: 3 } }), '{"a":{"c":3,"d":4},"b":1}')
  assert.equal(canonical.canonicalJson({ a: 1, b: 2 }), canonical.canonicalJson({ b: 2, a: 1 }))
  assert.equal(canonical.canonicalJson([3, { x: 1, y: 2 }]), '[3,{"x":1,"y":2}]')
  assert.equal(canonical.canonicalJson('s'), '"s"')
  assert.equal(canonical.canonicalJson(null), 'null')
})

test('WHAT[DURABLE-EVENTS-003] canonical JSON orders numeric-looking and non-BMP keys by Unicode code point', () => {
  assert.equal(canonical.canonicalJson({ 2: 'two', 10: 'ten' }), '{"10":"ten","2":"two"}')
  assert.equal(
    canonical.canonicalJson({ '\u{10000}': 'supplementary', '\uE000': 'bmp' }),
    '{"":"bmp","𐀀":"supplementary"}',
  )
})

test('WHAT[DURABLE-EVENTS-003] canonical JSON preserves JSON sparse-array null semantics', () => {
  assert.equal(canonical.canonicalJson(new Array(2)), '[null,null]')
})

test('WHAT[DURABLE-EVENTS-003] MISC_canonical_json_equal_ignores_key_order', () => {
  assert.equal(canonical.equal({ a: 1, b: 2 }, { b: 2, a: 1 }), true)
  assert.equal(canonical.equal({ a: 1 }, { a: 2 }), false)
  assert.equal(canonical.equal({ a: 1 }, { a: 1, b: 2 }), false)
  assert.equal(canonical.equal(null, undefined), false)
})

test('WHAT[DURABLE-EVENTS-003] MISC_without_keys_drops_named_fields_only', () => {
  assert.deepEqual(canonical.withoutKeys(['id', 'secret'], { id: 'x', secret: 'y', keep: 1 }), { keep: 1 })
  assert.equal(canonical.withoutKeys(['a'], 'plain'), 'plain')
  assert.equal(canonical.withoutKeys(['a'], null), null)
  assert.deepEqual(canonical.withoutKeys(['a'], [1, 2]), [1, 2], 'arrays pass through untouched')
})
