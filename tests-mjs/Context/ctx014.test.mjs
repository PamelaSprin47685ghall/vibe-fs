// tests-mjs/Context/ctx014.test.mjs — CTX-014 diagnostic observability boundary.
//
// Two claims, both fail closed:
//
//   the forbidden field names never appear in production source — a new
//   `context_ratio`-style field would trip this before it can reach a log line;
//   the layer that would make it a decision (the estimator) was deleted in X9,
//   and this test is the tombstone check
//
//   `Diagnostic.emit` refuses any field outside the whitelist — the schema is
//   structural, not a documentation convention

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { diagnostic as diag } from '../domain.mjs'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')
const NEXT_DIR = path.join(ROOT, 'src', 'Wanxiangshu.Next')

function sourceFiles(dir) {
  const out = []
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) out.push(...sourceFiles(full))
    else if (entry.name.endsWith('.fs')) out.push(full)
  }
  return out
}

const FORBIDDEN = ['context_ratio', 'estimated_tokens_remaining', 'compression_needed']

test('CTX_014_forbidden_field_names_never_appear_in_production_source', () => {
  for (const file of sourceFiles(NEXT_DIR)) {
    // `Diagnostic.fs` declares the forbidden names themselves (the tombstone
    // list); the claim is that no OTHER source uses them.
    if (path.basename(file) === 'Diagnostic.fs') continue
    const body = fs.readFileSync(file, 'utf8')
    for (const name of FORBIDDEN) {
      assert.equal(
        body.includes(name),
        false,
        `${path.relative(ROOT, file)} must not contain '${name}' (CTX-014 forbidden field)`,
      )
    }
  }
})

test('CTX_014_diagnostic_emit_accepts_only_whitelisted_fields', () => {
  // Expected path: whitelist ok, no console side effect required.
  assert.doesNotThrow(() => diag.emit('reanchor_failed', [['session_id', 'ses_x']]))

  // A field outside the whitelist is refused, not silently dropped.
  assert.throws(
    () => diag.emit('operation', [['context_ratio', '0.9']]),
    /CTX-014/,
    'forbidden field must be refused',
  )
  assert.throws(
    () => diag.emit('operation', [['estimated_tokens_remaining', '100']]),
    /CTX-014/,
    'forbidden field must be refused',
  )
  assert.throws(
    () => diag.emit('operation', [['compression_needed', 'true']]),
    /CTX-014/,
    'forbidden field must be refused',
  )
  assert.throws(
    () => diag.emit('operation', [['not_a_real_field', 'x']]),
    /CTX-014/,
    'any unknown field must be refused',
  )
})

test('CTX_014_diagnostic_emit_is_silent', () => {
  const lines = []
  const w = console.warn
  const e = console.error
  console.warn = (...a) => lines.push(['warn', ...a])
  console.error = (...a) => lines.push(['error', ...a])
  try {
    diag.emit('reanchor_failed', [['session_id', 'ses_x']])
    assert.equal(lines.length, 0, 'expected diagnostics must not print')
  } finally {
    console.warn = w
    console.error = e
  }
})

test('CTX_014_diagnostic_fatal_prints_and_refuses_unknown_fields', () => {
  const lines = []
  const e = console.error
  console.error = (line) => lines.push(String(line))
  try {
    diag.fatal('enforcer-cycle-failed', [
      ['session_id', 'ses_x'],
      ['result', 'missing CurrentRequest'],
    ])
    assert.equal(lines.length, 1)
    const payload = JSON.parse(lines[0])
    assert.equal(payload.operation, 'enforcer-cycle-failed')
    assert.equal(payload.result, 'missing CurrentRequest')

    assert.throws(() => diag.fatal('operation', [['not_a_real_field', 'x']]), /CTX-014/)
  } finally {
    console.error = e
  }
})
