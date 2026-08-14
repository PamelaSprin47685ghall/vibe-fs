// tests/unit/casebook/casebook-capture.test.mjs — G6-B: typed observation
// capture (CASE-003) + executor reading tolerance (§63).
//
// Capture comes from tool-execution args + rendered output, never transcript
// text. Unparseable executions yield None — one fewer change-detection
// opportunity, never a failed Inspector call.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  capture,
  ofExecCommand,
  contentHash,
} from '../../../dist/Repository/Knowledge/Casebook/Capture.js'
import { caseOf, listItems } from '../../verification-system/tests/support/domain.mjs'

test('CASE003_read_capture_is_typed_and_hashed', () => {
  const obs = capture('read', { path: 'src/a.fs' }, 'module A')
  assert.equal(obs !== undefined, true)
  assert.equal(caseOf(obs), 'FileRead')
  assert.equal(obs.fields[0], 'src/a.fs')
  assert.equal(obs.fields[1], contentHash('module A'))
  assert.equal(obs.fields[1].length, 64, 'sha256 hex')
  // empty output → no observation
  assert.equal(capture('read', { path: 'src/a.fs' }, ''), undefined)
  // missing path → no observation
  assert.equal(capture('read', {}, 'text'), undefined)
})

test('CASE003_glob_capture_parses_rendered_paths', () => {
  const obs = capture('glob', { pattern: 'src/**/*.fs' }, 'src/a.fs\nsrc/b.fs\n')
  assert.equal(caseOf(obs), 'GlobResult')
  assert.equal(obs.fields[0], 'src/**/*.fs')
  assert.deepEqual(listItems(obs.fields[1]), ['src/a.fs', 'src/b.fs'])
})

test('CASE003_grep_capture_keeps_match_lines', () => {
  const obs = capture('grep', { pattern: 'TODO' }, 'src/a.fs:3:TODO fix\n')
  assert.equal(caseOf(obs), 'GrepResult')
  assert.equal(obs.fields[0], 'TODO')
  assert.equal(listItems(obs.fields[1]).length, 1)
})

test('CASE003_unknown_tool_yields_nothing', () => {
  assert.equal(capture('executor', { command: 'ls' }, 'x'), undefined)
  assert.equal(capture('write', { path: 'a' }, 'x'), undefined)
})

test('S63_executor_reading_positives', () => {
  const fileOf = (cmd) => {
    const obs = ofExecCommand(cmd)
    assert.equal(obs !== undefined, true, `${cmd} must be recognized`)
    return obs.fields[0]
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

test('S63_executor_reading_negatives_skip_safely', () => {
  for (const cmd of ['cat "$(echo x)"', 'sh -c "cat a"', 'bash -c "cat a"', 'grep -r x .', 'ls -la']) {
    assert.equal(ofExecCommand(cmd), undefined, `${cmd} must be skipped`)
  }
})
