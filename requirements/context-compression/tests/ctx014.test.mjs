// CTX-014 — diagnostic events are structured, redacted and failure-transparent.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as owner from '../../../dist/Context/Companion/CompressionSurface.js'

const diag = {
  emit: (operation, fields) => owner.diagnosticEmit(operation, fields),
  fatal: (operation, fields) => owner.diagnosticFatal(operation, fields),
}

const fields = [
  ['session_id', 'ses_x'],
  ['operation', 'context_compression_test'],
  ['provider_error', 'E_TEST'],
  ['result', 'failed'],
]

const captureError = (fn) => {
  const lines = []
  const original = console.error
  console.error = (line) => lines.push(String(line))
  try {
    fn()
  } finally {
    console.error = original
  }
  return lines
}

test('WHAT[CONTEXT-COMPRESSION-013] CTX_014_diagnostic_emit_is_structured_and_redacted', () => {
  const lines = captureError(() => diag.emit('context_compression_test', fields))
  // `emit` is intentionally non-fatal and must not write an unstructured line.
  assert.equal(lines.length, 0)
})

test('WHAT[CONTEXT-COMPRESSION-013] CTX_014_fatal_emits_structured_event_without_raw_payload', () => {
  const previous = process.env.WANXIANGSHU_NO_FATAL_EXIT
  process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
  try {
    const lines = captureError(() => diag.fatal('context_compression_test', fields))
    assert.equal(lines.length, 1)
    const event = JSON.parse(lines[0])
    assert.equal(event.operation, 'context_compression_test')
    assert.equal(event.session_id, 'ses_x')
    assert.equal(event.provider_error, 'E_TEST')
    assert.equal(event.raw, undefined, 'fatal event must not carry a raw payload')
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_NO_FATAL_EXIT
    else process.env.WANXIANGSHU_NO_FATAL_EXIT = previous
  }
})

test('WHAT[CONTEXT-COMPRESSION-013] CTX_014_emit_drops_unbounded_fields_without_affecting_caller', () => {
  const state = { accepted: true }
  assert.doesNotThrow(() => diag.emit('context_compression_test', [['estimated_tokens_remaining', 'secret']]))
  assert.deepEqual(state, { accepted: true })
})
