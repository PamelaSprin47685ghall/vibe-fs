// tests/unit/tools/file-tools.test.mjs — VERIFY-009 coverage: the static read/write/edit tools.
//
// Pure Node fs against a per-test temp directory; the only cross-boundary value is the
// Fable CancellationToken, created the same way as in large-gate.test.mjs.

import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { readdirSync } from 'node:fs'
import test from 'node:test'

const { ToolContext } = await import('../../../dist/Tools/ToolContext.js')
const { fileReadTool, fileWriteTool, fileEditTool } = await import('../../../dist/Tools/FileTools.js')

const fableLibraryDir = join(
  process.cwd(),
  'dist',
  'fable_modules',
  readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),
)
const { createCancellationToken } = await import(join(fableLibraryDir, 'Async.js'))

const context = (workspace) =>
  new ToolContext(
    { fields: ['ses_file_tools'], cases: () => ['SessionId'], tag: 0 },
    workspace,
    createCancellationToken(false),
  )

const input = (payload) => ({ Payload: payload })

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-filetools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('FILETOOLS_read_returns_content_for_existing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'note.txt')
  writeFileSync(path, 'hello world')

  const tool = fileReadTool()
  const output = await tool.Execute(context(dir), input(JSON.stringify({ filePath: path })))

  assert.equal(output.Result, 'hello world')
  assert.equal(output.Truncated, false)
  assert.equal(tool.Name, 'read')
  cleanup()
})

test('FILETOOLS_read_reports_missing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'nope.txt')

  const tool = fileReadTool()
  const output = await tool.Execute(context(dir), input(JSON.stringify({ filePath: path })))

  assert.equal(output.Result, `File not found: ${path}`)
  cleanup()
})

test('FILETOOLS_read_accepts_a_bare_string_payload', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'bare.txt')
  writeFileSync(path, 'bare payload')

  const tool = fileReadTool()
  const output = await tool.Execute(context(dir), input(JSON.stringify(path)))

  assert.equal(output.Result, 'bare payload')
  cleanup()
})

test('FILETOOLS_read_falls_back_to_raw_payload_when_not_json', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'raw.txt')
  writeFileSync(path, 'raw content')

  const tool = fileReadTool()
  const output = await tool.Execute(context(dir), input(path))

  assert.equal(output.Result, 'raw content')
  cleanup()
})

test('FILETOOLS_write_creates_file_and_reports_size', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'out.txt')

  const tool = fileWriteTool()
  const output = await tool.Execute(
    context(dir),
    input(JSON.stringify({ filePath: path, content: 'written by test' })),
  )

  assert.equal(readFileSync(path, 'utf8'), 'written by test')
  assert.match(output.Result, /^Wrote .+ \(\d+ bytes\)$/)
  assert.equal(output.Truncated, false)
  cleanup()
})

test('FILETOOLS_write_refuses_unparseable_payload', async () => {
  const { dir, cleanup } = sandbox()
  const tool = fileWriteTool()
  const output = await tool.Execute(context(dir), input('not json at all'))

  assert.match(output.Result, /^Failed to parse JSON payload for write tool: /)
  cleanup()
})

test('FILETOOLS_edit_replaces_exact_match', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'edit.txt')
  writeFileSync(path, 'alpha beta gamma')

  const tool = fileEditTool()
  const output = await tool.Execute(
    context(dir),
    input(JSON.stringify({ filePath: path, oldString: 'beta', newString: 'BETA' })),
  )

  assert.equal(output.Result, `Edited ${path}`)
  assert.equal(readFileSync(path, 'utf8'), 'alpha BETA gamma')
  cleanup()
})

test('FILETOOLS_edit_reports_missing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'missing.txt')

  const tool = fileEditTool()
  const output = await tool.Execute(
    context(dir),
    input(JSON.stringify({ filePath: path, oldString: 'x', newString: 'y' })),
  )

  assert.equal(output.Result, `File not found: ${path}`)
  cleanup()
})

test('FILETOOLS_edit_reports_absent_old_string', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'no-match.txt')
  writeFileSync(path, 'nothing to replace')

  const tool = fileEditTool()
  const output = await tool.Execute(
    context(dir),
    input(JSON.stringify({ filePath: path, oldString: 'zzz', newString: 'y' })),
  )

  assert.equal(output.Result, `oldString not found in file ${path}`)
  assert.equal(readFileSync(path, 'utf8'), 'nothing to replace')
  cleanup()
})

test('FILETOOLS_edit_refuses_unparseable_payload', async () => {
  const { dir, cleanup } = sandbox()
  const tool = fileEditTool()
  const output = await tool.Execute(context(dir), input('{broken'))

  assert.match(output.Result, /^Invalid edit payload: /)
  cleanup()
})
