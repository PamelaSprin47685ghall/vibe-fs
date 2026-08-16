// Blogger materialization and receipt identity through the cycle owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as cycle from '../../../dist/Context/Companion/Blogger/Runtime/CycleSurface.js'

const materialize = (over = {}) => ({ kind: 'materialize', requestId: 'req-1', blogger: 'ses-blogger', digest: 'ctx-1', ...over })
const entry = (over = {}) => ({ kind: 'entry', requestId: 'req-1', run: 'msg-e1', ...over })
const squash = (over = {}) => ({ kind: 'squash', requestId: 'req-s1', run: 'msg-s1', ...over })
const state = (...actions) => cycle.scenario(actions)
const ok = (...actions) => {
  const result = state(...actions)
  assert.equal(result.ok, true, result.error ?? '')
  return result.state
}

test('WHAT[EFFECT-ACCOUNTING-008] C5_materialize_opens_request_queryable_by_blogger', () => {
  assert.deepEqual(ok(materialize({ requestId: 'req-open' }),), {
    openRequests: 1,
    openBloggers: 1,
    receipts: 0,
    requestBindings: 0,
  })
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_entry_commit_records_receipt_and_clears_open_request', () => {
  assert.deepEqual(ok(materialize(), entry()), {
    openRequests: 0,
    openBloggers: 0,
    receipts: 1,
    requestBindings: 1,
  })
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_same_provider_run_cannot_be_both_entry_and_squash', () => {
  const result = state(entry({ run: 'msg-same' }), squash({ run: 'msg-same' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /already has/i)
})

test('WHAT[EFFECT-ACCOUNTING-004] C5_same_request_materialize_is_idempotent', () => {
  assert.deepEqual(ok(materialize({ requestId: 'req-idem' }), materialize({ requestId: 'req-idem' })), {
    openRequests: 1,
    openBloggers: 1,
    receipts: 0,
    requestBindings: 0,
  })
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_materialize_prompt_key_fill_in_after_send', () => {
  assert.deepEqual(ok(materialize({ requestId: 'req-key' }), materialize({ requestId: 'req-key', promptKey: 'pk-blog-1' })), {
    openRequests: 1,
    openBloggers: 1,
    receipts: 0,
    requestBindings: 0,
  })
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_materialize_prompt_key_cannot_rebind', () => {
  const result = state(
    materialize({ requestId: 'req-rebind', promptKey: 'pk-a' }),
    materialize({ requestId: 'req-rebind', promptKey: 'pk-b' }),
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /different PromptKey/i)
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_duplicate_request_materialize_different_context_rejected', () => {
  const result = state(materialize({ requestId: 'req-dup', digest: 'ctx-1' }), materialize({ requestId: 'req-dup', digest: 'ctx-2' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /different context/i)
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_abandon_clears_open_request', () => {
  assert.deepEqual(ok(materialize({ requestId: 'req-ab' }), { kind: 'abandon', requestId: 'req-ab', blogger: 'ses-blogger' }), {
    openRequests: 0,
    openBloggers: 0,
    receipts: 0,
    requestBindings: 0,
  })
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_request_id_cannot_rebind_to_different_provider_run', () => {
  const result = state(entry({ requestId: 'req-bind', run: 'msg-a' }), entry({ requestId: 'req-bind', run: 'msg-b' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /RequestId.*rebind/i)
})
