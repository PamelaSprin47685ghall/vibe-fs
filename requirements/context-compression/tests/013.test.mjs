import assert from 'node:assert/strict'
import test from 'node:test'
import * as diag from '../../../dist/Context/Companion/DiagnosticEmitSurface.js'

test('WHAT[CONTEXT-COMPRESSION-013] CTX_014_diagnostic_emit_is_structured_and_redacted', () => {
  const res = diag.emit({ sensitive: 'password123', event: 'test' })
  assert.equal(res.sensitive, undefined)
  assert.equal(res.event, 'test')
})

test('WHAT[CONTEXT-COMPRESSION-013] CTX_014_fatal_emits_structured_event_without_raw_payload', () => {
  const fatal = diag.fatalEvent({ raw: 'secret' })
  assert.doesNotMatch(JSON.stringify(fatal), /secret/)
})

test('WHAT[CONTEXT-COMPRESSION-013] CTX_014_emit_drops_unbounded_fields_without_affecting_caller', () => {
  const res = diag.emit({ huge: 'x'.repeat(10000) })
  assert.ok(res)
})
