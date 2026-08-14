// tests/unit/Context/ctx014.test.mjs — CTX-014 diagnostic observability boundary.
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
import { diagnostic as diag, toList } from '../../../tests/unit/support/domain.mjs'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const NEXT_DIR = path.join(ROOT, 'src', 'Wanxiangshu')

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

test('CTX_014_enforcer_protocol_violation_fields_are_whitelisted', () => {
  // ENFORCER-042: the multi-call protocol-violation emit carries `result` and
  // `call_count`. Both must be in AllowedFields so the emit never throws and the
  // multi-call cycle can still proceed to commit (diagnostic is silent, HOST-007).
  assert.doesNotThrow(() =>
    diag.emit('enforcer-protocol-violation', [
      ['result', 'multiple blog calls in one provider step; tip = first by PartOrdinal (ENFORCER-025)'],
      ['call_count', '2'],
    ]),
  )
  // call_count is numeric-only payload; the whitelist is name-based, so any value
  // with that NAME is accepted (value shape is not the schema's concern).
  assert.doesNotThrow(() => diag.emit('enforcer-protocol-violation', [['call_count', '7']]))
})

test('CTX_014_diagnostic_emit_is_silent_by_default_and_observable_on_demand', () => {
  const lines = []
  const w = console.warn
  const e = console.error
  console.warn = (...a) => lines.push(['warn', ...a])
  console.error = (...a) => lines.push(['error', ...a])
  const previous = process.env.WANXIANGSHU_DIAG
  try {
    delete process.env.WANXIANGSHU_DIAG
    diag.emit('reanchor_failed', [['session_id', 'ses_x']])
    assert.equal(lines.length, 0, 'expected diagnostics must not print by default')

    // HOST-007 is about a log line never becoming a recovery protocol, not about being
    // undebuggable: an explicit env flag surfaces the same records and changes no decision.
    process.env.WANXIANGSHU_DIAG = '1'
    diag.emit('reanchor_failed', [['session_id', 'ses_x']])
    assert.equal(lines.length, 1, 'WANXIANGSHU_DIAG=1 must surface the record')
    assert.match(String(lines[0][1]), /reanchor_failed/)
  } finally {
    console.warn = w
    console.error = e
    if (previous === undefined) delete process.env.WANXIANGSHU_DIAG
    else process.env.WANXIANGSHU_DIAG = previous
  }
})

test('CTX_014_diagnostic_records_carry_their_fields', () => {
  const lines = []
  const e = console.error
  const previous = process.env.WANXIANGSHU_DIAG
  try {
    console.error = (...a) => lines.push(a.join(' '))
    process.env.WANXIANGSHU_DIAG = '1'
    diag.emit('enforcer-cycle-repair', toList([['session_id', 'ses_x'], ['result', 'why it happened']]))
    // A record that names its operation and drops its fields explains nothing, which is how a
    // stalled session ends up with no diagnosis anywhere. The payload shape is pinned here.
    const record = JSON.parse(lines[0])
    assert.deepEqual(record, {
      operation: 'enforcer-cycle-repair',
      session_id: 'ses_x',
      result: 'why it happened',
    })
  } finally {
    console.error = e
    if (previous === undefined) delete process.env.WANXIANGSHU_DIAG
    else process.env.WANXIANGSHU_DIAG = previous
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

test('LOOP_010_loop_kill_diagnostic_fields_are_whitelisted', () => {
  // LOOP-010 allowlist: session_id / operation / effective_character_count /
  // detector_step / result (+ duration, provider_error). Full loop body is forbidden.
  const allowed = [
    ['session_id', 'ses_loop'],
    ['result', 'armed'],
    ['detector_step', '120'],
    ['effective_character_count', '12.5000'],
    ['duration', '3'],
    ['provider_error', 'abort refused'],
  ]
  assert.doesNotThrow(() => diag.emit('loop-kill', allowed))

  for (const result of [
    'armed',
    'aborted',
    'ignored-duplicate',
    'continue-sent',
    'budget-exhausted',
    'abort-failed',
  ]) {
    assert.doesNotThrow(() =>
      diag.emit('loop-kill', [
        ['session_id', 'ses_loop'],
        ['result', result],
      ]),
    )
  }

  // Body of the loop must never be a diagnostic field.
  for (const forbidden of ['loop_body', 'delta', 'text', 'stream_body', 'n_gram_text']) {
    assert.throws(
      () => diag.emit('loop-kill', [['session_id', 'ses_loop'], [forbidden, 'xxxx'.repeat(100)]]),
      /CTX-014/,
      `field '${forbidden}' must be refused (LOOP-010 no loop body)`,
    )
  }

  // Production sites: only Diagnostic.emit "loop-kill" field keys are in scope.
  // Whole-file scans false-positive on Host delta bindings (e.g. delta.Delta).
  const LOOP_010_KEYS = new Set([
    'session_id',
    'result',
    'detector_step',
    'effective_character_count',
    'duration',
    'provider_error',
  ])
  const FORBIDDEN_BODY_KEYS = ['loop_body', 'delta', 'text', 'stream_body', 'n_gram_text']

  function loopKillFieldKeys(source) {
    const keys = []
    const emitRe = /Diagnostic\.emit\s+"loop-kill"\s+(?:fields|\[)/g
    let m
    while ((m = emitRe.exec(source)) !== null) {
      const from = m.index + m[0].length - 1
      if (m[0].endsWith('fields')) {
        // Named binding path: keys live in the preceding `fields = [ ... ]` block.
        const blockStart = source.lastIndexOf('let fields', m.index)
        const block = blockStart >= 0 ? source.slice(blockStart, m.index) : ''
        for (const k of block.matchAll(/"([a-z_]+)"\s*,/g)) keys.push(k[1])
        continue
      }
      // Inline list: Diagnostic.emit "loop-kill" [ "k", v; ... ]
      let depth = 0
      let end = from
      for (; end < source.length; end++) {
        const ch = source[end]
        if (ch === '[') depth++
        else if (ch === ']') {
          depth--
          if (depth === 0) {
            end++
            break
          }
        }
      }
      const block = source.slice(from, end)
      for (const k of block.matchAll(/"([a-z_]+)"\s*,/g)) keys.push(k[1])
    }
    return keys
  }

  for (const rel of [
    ['Infrastructure', 'OpenCode', 'Host', 'LoopSensor.fs'],
    ['Application', 'Recovery', 'ProviderRecoveryWorkflow.fs'],
  ]) {
    const sitePath = path.join(NEXT_DIR, ...rel)
    const siteBody = fs.readFileSync(sitePath, 'utf8')
    assert.match(siteBody, /Diagnostic\.emit\s+"loop-kill"/, `${rel.join('/')} must emit loop-kill`)
    const keys = loopKillFieldKeys(siteBody)
    assert.ok(keys.length > 0, `${rel.join('/')} must expose at least one loop-kill field key`)
    for (const key of keys) {
      assert.ok(
        LOOP_010_KEYS.has(key),
        `${rel.join('/')}: field '${key}' is outside LOOP-010 allowlist`,
      )
      assert.ok(
        !FORBIDDEN_BODY_KEYS.includes(key),
        `${rel.join('/')}: body field '${key}' must not be a diagnostic key`,
      )
    }
  }

  const sensorPath = path.join(NEXT_DIR, 'Infrastructure', 'OpenCode', 'Host', 'LoopSensor.fs')
  const sensorBody = fs.readFileSync(sensorPath, 'utf8')
  for (const key of ['session_id', 'result', 'detector_step', 'effective_character_count', 'provider_error']) {
    assert.ok(
      loopKillFieldKeys(sensorBody).includes(key),
      `LoopSensor loop-kill emit must include field ${key}`,
    )
  }

  const diagnosticPath = path.join(NEXT_DIR, 'Infrastructure', 'OpenCode', 'Host', 'Diagnostic.fs')
  const diagnosticBody = fs.readFileSync(diagnosticPath, 'utf8')
  assert.match(diagnosticBody, /"effective_character_count"/)
  assert.match(diagnosticBody, /"detector_step"/)
  assert.match(diagnosticBody, /\/\/ LOOP-010/)
})
