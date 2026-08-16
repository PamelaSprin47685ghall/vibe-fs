// tests/unit/process/pty-api.test.mjs — Pty byte/id helpers and the
// cross-runtime parent-abort registry.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  bytes,
  newId,
  registerParentAbort,
  unregisterParentAbort,
  abortParent,
} = await import('../../../dist/Process/PtyApi.js')

const { PtyId__get_Value } = await import('../../../dist/Process/PtyTypes.js')

// ── bytes / newId ────────────────────────────────────────────────────────────

test('WHAT[PROC-001] PTY_API_bytes_encodes_utf8', () => {
  assert.deepEqual([...bytes('abc')], [97, 98, 99])
  // Multi-byte UTF-8: 'é' is two bytes, '雪' is three.
  assert.deepEqual([...bytes('é')], [0xc3, 0xa9])
  assert.deepEqual([...bytes('雪')], [0xe9, 0x9b, 0xaa])
  assert.deepEqual([...bytes('')], [])
})

test('WHAT[PROC-001] PTY_API_new_id_has_pty_prefix_and_eight_hex_chars', () => {
  const id = PtyId__get_Value(newId())
  assert.match(id, /^pty-[0-9a-f]{8}$/)
})

test('WHAT[PROC-001] PTY_API_new_id_is_unique_per_call', () => {
  const seen = new Set(Array.from({ length: 64 }, () => PtyId__get_Value(newId())))
  assert.equal(seen.size, 64)
})

// ── parent abort registry ────────────────────────────────────────────────────

test('WHAT[PROC-006] PTY_API_abort_parent_invokes_every_registered_callback', () => {
  const parent = 'parent-all'
  const calls = []
  registerParentAbort(parent, () => calls.push('a'))
  registerParentAbort(parent, () => calls.push('b'))

  abortParent(parent)
  assert.deepEqual(calls.sort(), ['a', 'b'])

  // Aborting does NOT clear the registry: a second abort fires them again.
  abortParent(parent)
  assert.equal(calls.length, 4)
})

test('WHAT[PROC-006] PTY_API_abort_parent_with_unknown_id_is_a_noop', () => {
  abortParent('parent-never-registered')
})

test('WHAT[PROC-006] PTY_API_unregister_removes_only_the_matching_token', () => {
  const parent = 'parent-partial'
  const calls = []
  const tokenA = registerParentAbort(parent, () => calls.push('a'))
  registerParentAbort(parent, () => calls.push('b'))

  unregisterParentAbort(parent, tokenA)
  abortParent(parent)
  assert.deepEqual(calls, ['b'])
})

test('WHAT[PROC-006] PTY_API_unregister_last_callback_drops_the_parent_entry', () => {
  const parent = 'parent-dropped'
  const calls = []
  const token = registerParentAbort(parent, () => calls.push('a'))

  unregisterParentAbort(parent, token)
  abortParent(parent)
  assert.deepEqual(calls, [])
})

test('WHAT[PROC-006] PTY_API_unregister_with_unknown_parent_or_token_is_a_noop', () => {
  unregisterParentAbort('parent-nope', 1)
  const parent = 'parent-mismatch'
  registerParentAbort(parent, () => {})
  unregisterParentAbort(parent, 99999)
})

test('WHAT[PROC-006] PTY_API_tokens_are_monotonic_across_parents', () => {
  const t1 = registerParentAbort('parent-tok-1', () => {})
  const t2 = registerParentAbort('parent-tok-2', () => {})
  assert.ok(t2 > t1, `${t2} > ${t1}`)
})

test('WHAT[PROC-006] PTY_API_throwing_abort_callback_does_not_block_the_rest', () => {
  const parent = 'parent-throw'
  const calls = []
  registerParentAbort(parent, () => {
    throw new Error('abort exploded')
  })
  registerParentAbort(parent, () => calls.push('after'))

  abortParent(parent)
  assert.deepEqual(calls, ['after'])
})
