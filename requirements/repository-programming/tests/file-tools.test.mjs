// VERIFY-009 coverage: static read/write/edit tools through their registered
// owner surface. Native Node fs is used only for fixture setup/observations.

import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { read, write, edit, toolName } from '../../../dist/OpenCode/Tools/FileToolsSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-filetools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[REPOSITORY-PROGRAMMING-007] FILETOOLS_read_returns_content_for_existing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'note.txt')
  writeFileSync(path, 'hello world')

  const output = await read(dir, JSON.stringify({ filePath: path }))
  assert.equal(output.result, 'hello world')
  assert.equal(output.truncated, false)
  assert.equal(toolName('read'), 'read')
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-007] FILETOOLS_read_reports_missing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'nope.txt')
  const output = await read(dir, JSON.stringify({ filePath: path }))
  assert.equal(output.result, `File not found: ${path}`)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-007] FILETOOLS_read_accepts_a_bare_string_payload', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'bare.txt')
  writeFileSync(path, 'bare payload')
  const output = await read(dir, JSON.stringify(path))
  assert.equal(output.result, 'bare payload')
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-007] FILETOOLS_read_falls_back_to_raw_payload_when_not_json', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'raw.txt')
  writeFileSync(path, 'raw content')
  const output = await read(dir, path)
  assert.equal(output.result, 'raw content')
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-010] FILETOOLS_write_creates_file_and_reports_size', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'out.txt')
  const output = await write(dir, JSON.stringify({ filePath: path, content: 'written by test' }))
  assert.equal(readFileSync(path, 'utf8'), 'written by test')
  assert.match(output.result, /^Wrote .+ \(\d+ bytes\)$/)
  assert.equal(output.truncated, false)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-010] FILETOOLS_write_refuses_unparseable_payload', async () => {
  const { dir, cleanup } = sandbox()
  const output = await write(dir, 'not json at all')
  assert.match(output.result, /^Failed to parse JSON payload for write tool: /)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-010] FILETOOLS_edit_replaces_exact_match', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'edit.txt')
  writeFileSync(path, 'alpha beta gamma')
  const output = await edit(dir, JSON.stringify({ filePath: path, oldString: 'beta', newString: 'BETA' }))
  assert.equal(output.result, `Edited ${path}`)
  assert.equal(readFileSync(path, 'utf8'), 'alpha BETA gamma')
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-010] FILETOOLS_edit_reports_missing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'missing.txt')
  const output = await edit(dir, JSON.stringify({ filePath: path, oldString: 'x', newString: 'y' }))
  assert.equal(output.result, `File not found: ${path}`)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-010] FILETOOLS_edit_reports_absent_old_string', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'no-match.txt')
  writeFileSync(path, 'nothing to replace')
  const output = await edit(dir, JSON.stringify({ filePath: path, oldString: 'zzz', newString: 'y' }))
  assert.equal(output.result, `oldString not found in file ${path}`)
  assert.equal(readFileSync(path, 'utf8'), 'nothing to replace')
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-010] FILETOOLS_edit_refuses_unparseable_payload', async () => {
  const { dir, cleanup } = sandbox()
  const output = await edit(dir, '{broken')
  assert.match(output.result, /^Invalid edit payload: /)
  cleanup()
})
