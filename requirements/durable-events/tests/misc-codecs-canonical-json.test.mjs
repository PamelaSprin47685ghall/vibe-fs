// Split from tests/unit/codec/misc-codecs.test.mjs (cutover Wave 2a); owner: durable-events.
// Canonical JSON codec cluster: key-sorting canonicalization, order-insensitive
// equality, named-field stripping — the identity protocol of DURABLE-EVENTS-003.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  canonicalJson,
  equal,
  withoutKeys,
} = await import('../../../dist/OpenCode/Codec/CanonicalJson.js')

// ── CanonicalJson ────────────────────────────────────────────────────────────

test('MISC_canonical_json_sorts_keys_recursively', () => {
  assert.equal(canonicalJson({ b: 1, a: { d: 4, c: 3 } }), '{"a":{"c":3,"d":4},"b":1}')
  assert.equal(canonicalJson({ a: 1, b: 2 }), canonicalJson({ b: 2, a: 1 }))
  assert.equal(canonicalJson([3, { x: 1, y: 2 }]), '[3,{"x":1,"y":2}]')
  assert.equal(canonicalJson('s'), '"s"')
  assert.equal(canonicalJson(null), 'null')
})

test('MISC_canonical_json_equal_ignores_key_order', () => {
  assert.equal(equal({ a: 1, b: 2 }, { b: 2, a: 1 }), true)
  assert.equal(equal({ a: 1 }, { a: 2 }), false)
  assert.equal(equal({ a: 1 }, { a: 1, b: 2 }), false)
  assert.equal(equal(null, undefined), false)
})

test('MISC_without_keys_drops_named_fields_only', () => {
  assert.deepEqual(withoutKeys(['id', 'secret'], { id: 'x', secret: 'y', keep: 1 }), { keep: 1 })
  assert.equal(withoutKeys(['a'], 'plain'), 'plain')
  assert.equal(withoutKeys(['a'], null), null)
  assert.deepEqual(withoutKeys(['a'], [1, 2]), [1, 2], 'arrays pass through untouched')
})
