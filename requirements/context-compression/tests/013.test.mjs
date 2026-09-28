import assert from 'node:assert/strict'
import test from 'node:test'
import * as owner from '../../../dist/Context/Companion/CompressionSurface.js'

test('WHAT[context-compression-013] diagnostic emission is non-fatal and does not write raw console payloads', () => {
  const lines = []
  const original = console.error
  console.error = (line) => lines.push(String(line))
  try {
    assert.doesNotThrow(() => owner.diagnosticEmit('context_compression_test', [
      ['session_id', 'ses_x'], ['provider_error', 'E_TEST'], ['raw', 'private task'],
    ]))
    assert.deepEqual(lines, [])
  } finally {
    console.error = original
  }
})

test.todo('WHAT[context-compression-013] actual retry and commit decisions are unchanged when diagnostic delivery fails or its text changes; GAP-104')
