import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData, assertOpaque, isJsData } from '../../verification-system/tests/support/js-contract.mjs'

test('WHAT[JS-SEMANTIC-SURFACE-005] JS_SURFACE_005_js_native_representation_rules', () => {
  assert.equal(isJsData(null), true)
  assert.equal(isJsData('s'), true)
  assert.equal(isJsData(42), true)
  assert.equal(isJsData(true), true)
  assert.equal(isJsData(10n), true)
  assert.equal(isJsData([]), true)
  assert.equal(isJsData({ a: [1, { b: 'c' }] }), true)

  assert.equal(isJsData({ tag: 0, fields: ['x'] }), false)
  assert.equal(isJsData({ cases: () => ['A', 'B'] }), false)
  assert.equal(isJsData({ head: 1, tail: null }), false)
  assert.equal(isJsData({ $reflection: {}, value: 1 }), false)
  assert.equal(isJsData(new Date()), false)

  const fsharpMapLike = new (class {
    constructor() {
      this.size = 0
    }
    entries() {
      return []
    }
  })()
  assert.equal(isJsData(fsharpMapLike), false)
  assert.throws(() => assertJsData({ tag: 1, fields: [] }), /JS-native/)
  assert.equal(assertJsData({ ok: true, value: [1, 2] }).ok, true)

  assert.equal(assertOpaque({}, 'h') !== undefined, true)
  assert.equal(assertOpaque(() => {}, 'f') !== undefined, true)
  assert.throws(() => assertOpaque('s'), /opaque/)
  assert.throws(() => assertOpaque(1), /opaque/)
})
