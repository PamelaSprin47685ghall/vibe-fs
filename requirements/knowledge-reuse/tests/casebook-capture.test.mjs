// tests/unit/casebook/casebook-capture.test.mjs — G6-B: typed observation
// capture (CASE-003) + executor reading tolerance (§63).
//
// Capture comes from tool-execution args + rendered output, never transcript
// text. Unparseable executions yield None — one fewer change-detection
// opportunity, never a failed Inspector call.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_read_capture_is_typed_and_hashed', () => {
  const obs = casebook.capture('read', { path: 'src/a.fs' }, 'module A')
  assert.notEqual(obs, null)
  assert.equal(obs.kind, 'file-read')
  assert.equal(obs.path, 'src/a.fs')
  assert.equal(obs.contentHash, casebook.contentHash('module A'))
  assert.equal(obs.contentHash.length, 64, 'sha256 hex')
  // empty output → no observation
  assert.equal(casebook.capture('read', { path: 'src/a.fs' }, ''), null)
  // missing path → no observation
  assert.equal(casebook.capture('read', {}, 'text'), null)
})

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_glob_capture_parses_rendered_paths', () => {
  const obs = casebook.capture('glob', { pattern: 'src/**/*.fs' }, 'src/a.fs\nsrc/b.fs\n')
  assert.equal(obs.kind, 'glob-result')
  assert.equal(obs.pattern, 'src/**/*.fs')
  assert.deepEqual(obs.paths, ['src/a.fs', 'src/b.fs'])
})

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_grep_capture_keeps_match_lines', () => {
  const obs = casebook.capture('grep', { pattern: 'TODO' }, 'src/a.fs:3:TODO fix\n')
  assert.equal(obs.kind, 'grep-result')
  assert.equal(obs.pattern, 'TODO')
  assert.equal(obs.matches.length, 1)
})

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_unknown_tool_yields_nothing', () => {
  assert.equal(casebook.capture('executor', { command: 'ls' }, 'x'), null)
  assert.equal(casebook.capture('write', { path: 'a' }, 'x'), null)
})

test('WHAT[KNOWLEDGE-REUSE-003] S63_executor_reading_positives', () => {
  const fileOf = (cmd) => {
    const obs = casebook.ofExecCommand(cmd)
    assert.notEqual(obs, null, `${cmd} must be recognized`)
    return obs.path
  }
  assert.equal(fileOf('cat src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('cat -n src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('head src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('head -n 30 src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('tail -100 src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('tail -f src/a.fs'), 'src/a.fs')
  assert.equal(fileOf("sed -n '20,80p' src/a.fs"), 'src/a.fs')
  assert.equal(fileOf('cat src/a.fs | grep bar'), 'src/a.fs')
})

test('WHAT[KNOWLEDGE-REUSE-003] S63_executor_reading_negatives_skip_safely', () => {
  for (const cmd of ['cat "$(echo x)"', 'sh -c "cat a"', 'bash -c "cat a"', 'grep -r x .', 'ls -la']) {
    assert.equal(casebook.ofExecCommand(cmd), null, `${cmd} must be skipped`)
  }
})
