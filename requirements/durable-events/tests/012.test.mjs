import assert from 'node:assert/strict'
import test from 'node:test'
import * as closure from '../../../dist/Persistence/JournalPayloadClosureSurface.js'

test('WHAT[DURABLE-EVENTS-012] closure_lifts_a_content_addressed_digest_into_payload_refs', () => {
  const refs = closure.extractPayloadRefs({ digest: 'sha256-blob-1', text: 'large-data' })
  assert.deepEqual(refs, ['sha256-blob-1'])
})

test('WHAT[DURABLE-EVENTS-012] closure_dedupes_a_matching_blob_ref_and_digest_pair', () => {
  const refs = closure.extractPayloadRefs({ ref1: 'sha256-abc', ref2: 'sha256-abc' })
  assert.deepEqual(refs, ['sha256-abc'])
})

test('WHAT[DURABLE-EVENTS-012] closure_ignores_non_content_addressed_placeholder_handles', () => {
  const refs = closure.extractPayloadRefs({ handle: 'placeholder-temp' })
  assert.deepEqual(refs, [])
})

test('WHAT[DURABLE-EVENTS-012] closure_is_empty_for_a_fact_without_blob_fields', () => {
  const refs = closure.extractPayloadRefs({ count: 42 })
  assert.deepEqual(refs, [])
})
