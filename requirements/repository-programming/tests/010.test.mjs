import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { read, write, edit, toolName } = await import("../../../dist/OpenCode/Tools/FileToolsSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-filetools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[repository-programming-010] FILETOOLS_write_creates_file_and_reports_size', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'out.txt')
  const output = await write(dir, JSON.stringify({ filePath: path, content: 'written by test' }))
  assert.equal(readFileSync(path, 'utf8'), 'written by test')
  assert.match(output.result, /^Wrote .+ \(\d+ bytes\)$/)
  assert.equal(output.truncated, false)
  cleanup()
})
test('WHAT[repository-programming-010] FILETOOLS_write_refuses_unparseable_payload', async () => {
  const { dir, cleanup } = sandbox()
  const output = await write(dir, 'not json at all')
  assert.match(output.result, /^Failed to parse JSON payload for write tool: /)
  cleanup()
})
test('WHAT[repository-programming-010] FILETOOLS_edit_replaces_exact_match', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'edit.txt')
  writeFileSync(path, 'alpha beta gamma')
  const output = await edit(dir, JSON.stringify({ filePath: path, oldString: 'beta', newString: 'BETA' }))
  assert.equal(output.result, `Edited ${path}`)
  assert.equal(readFileSync(path, 'utf8'), 'alpha BETA gamma')
  cleanup()
})
test('WHAT[repository-programming-010] FILETOOLS_edit_reports_missing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'missing.txt')
  const output = await edit(dir, JSON.stringify({ filePath: path, oldString: 'x', newString: 'y' }))
  assert.equal(output.result, `File not found: ${path}`)
  cleanup()
})
test('WHAT[repository-programming-010] FILETOOLS_edit_reports_absent_old_string', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'no-match.txt')
  writeFileSync(path, 'nothing to replace')
  const output = await edit(dir, JSON.stringify({ filePath: path, oldString: 'zzz', newString: 'y' }))
  assert.equal(output.result, `oldString not found in file ${path}`)
  assert.equal(readFileSync(path, 'utf8'), 'nothing to replace')
  cleanup()
})
test('WHAT[repository-programming-010] FILETOOLS_edit_refuses_unparseable_payload', async () => {
  const { dir, cleanup } = sandbox()
  const output = await edit(dir, '{broken')
  assert.match(output.result, /^Invalid edit payload: /)
  cleanup()
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, writeFileSync, rmSync, mkdirSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { createApi, api: apiOf, stagedCount, stagedKinds, run } = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");
const { generate } = await import("../../../dist/Repository/Programming/Js/GeneratorSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bindings-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const coderSurface = () => generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')

test('WHAT[repository-programming-010] JS008_012_bindings_rewrite_requires_existing_target', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old text', 'utf8')
    const binding = createApi(dir)
    const result = apiOf(binding).js.edit('a.txt', 'new text')
    assert.equal(result.ok, true)
    const missing = apiOf(binding).js.edit('nope.txt', 'x')
    assert.equal(missing.ok, false)
    assert.equal(missing.code, 'FILE_NOT_FOUND')
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-010] JS009_012_bindings_write_stages_create', () => {
  const { dir, cleanup } = sandbox()
  try {
    const binding = createApi(dir)
    const result = apiOf(binding).js.write('new.txt', 'fresh')
    assert.equal(result.ok, true)
    assert.equal(stagedCount(binding), 1)
    assert.deepEqual(stagedKinds(binding), ['Create'])
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-010] JS009_bindings_write_rejects_an_existing_target_before_staging', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'existing.txt'), 'external', 'utf8')
    const binding = createApi(dir)
    const result = apiOf(binding).js.write('existing.txt', 'tool')
    assert.equal(result.ok, false)
    assert.equal(result.code, 'FILE_ALREADY_EXISTS')
    assert.equal(stagedCount(binding), 0)
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { validateSingleIntent, validateTargets, validateFreshness, preflight, commitPlan, rollbackPlan } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const ok = (result) => result.ok
const codeOf = (result) => result.code
const rewrite = (path, originalText, newText) => ({ kind: 'rewrite', path, originalText, newText })
const create = (path, text) => ({ kind: 'create', path, text })
const current = { 'a.txt': 'current' }

test('WHAT[repository-programming-010] JS026_same_path_once_rejects_duplicate_mutation_targets', () => {
  const dup = [rewrite('a.txt', 'x', 'y'), create('a.txt', 'z')]
  const result = validateSingleIntent(dup)
  assert.equal(ok(result), false)
  assert.equal(codeOf(result), 'DUPLICATE_MUTATION_TARGET')

  const distinct = [rewrite('a.txt', 'x', 'y'), create('b.txt', 'z')]
  assert.equal(ok(validateSingleIntent(distinct)), true)
})
test('WHAT[repository-programming-010] JS008_009_rewrite_requires_existing_target_create_requires_missing', () => {
  const existing = ['a.txt']
  assert.equal(ok(validateTargets(existing, [rewrite('a.txt', 'x', 'y')])), true)
  assert.equal(codeOf(validateTargets(existing, [rewrite('missing.txt', 'x', 'y')])), 'FILE_NOT_FOUND')
  assert.equal(ok(validateTargets(existing, [create('new.txt', 'n')])), true)
  assert.equal(codeOf(validateTargets(existing, [create('a.txt', 'n')])), 'FILE_ALREADY_EXISTS')
})
}
