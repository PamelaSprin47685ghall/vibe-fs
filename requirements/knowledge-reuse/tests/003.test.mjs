import assert from 'node:assert/strict'
import test from 'node:test'
import * as capture from '../../../dist/Knowledge/Casebook/CaptureSurface.js'
import * as domain from '../../../dist/Knowledge/Casebook/DomainSurface.js'

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_read_capture_is_typed_and_hashed', () => {
  const obs = capture.captureRead('src/index.js', 'console.log("hello")')
  assert.equal(obs.kind, 'Read')
  assert.equal(obs.path, 'src/index.js')
  assert.ok(obs.fingerprint.length > 0)
})

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_glob_capture_parses_rendered_paths', () => {
  const obs = capture.captureGlob('src/**/*.js', ['src/a.js', 'src/b.js'])
  assert.equal(obs.kind, 'Glob')
  assert.deepEqual(obs.matches, ['src/a.js', 'src/b.js'])
})

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_grep_capture_keeps_match_lines', () => {
  const obs = capture.captureGrep('pattern', 'src/**/*.js', [{ path: 'src/a.js', line: 10 }])
  assert.equal(obs.kind, 'Grep')
  assert.equal(obs.matches.length, 1)
})

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_unknown_tool_yields_nothing', () => {
  const obs = capture.captureToolResult('unknown_tool', {})
  assert.equal(obs, null)
})

test('WHAT[KNOWLEDGE-REUSE-003] S63_executor_reading_positives', () => {
  const r = capture.captureRead('test.txt', 'contents')
  assert.equal(r.path, 'test.txt')
})

test('WHAT[KNOWLEDGE-REUSE-003] S63_executor_reading_negatives_skip_safely', () => {
  assert.equal(capture.captureToolResult('run', { command: 'ls' }), null)
})

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_normalize_dedupes_and_orders_observations', () => {
  const o1 = domain.readObservation('b.txt', 'h1')
  const o2 = domain.readObservation('a.txt', 'h2')
  const o3 = domain.readObservation('b.txt', 'h1')
  const normalized = domain.normalizeObservations([o1, o2, o3])
  assert.equal(normalized.length, 2)
  assert.equal(normalized[0].path, 'a.txt')
  assert.equal(normalized[1].path, 'b.txt')
})
