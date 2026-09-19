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

test('WHAT[repository-programming-007] FILETOOLS_read_returns_content_for_existing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'note.txt')
  writeFileSync(path, 'hello world')

  const output = await read(dir, JSON.stringify({ filePath: path }))
  assert.equal(output.result, 'hello world')
  assert.equal(output.truncated, false)
  assert.equal(toolName('read'), 'read')
  cleanup()
})
test('WHAT[repository-programming-007] FILETOOLS_read_reports_missing_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'nope.txt')
  const output = await read(dir, JSON.stringify({ filePath: path }))
  assert.equal(output.result, `File not found: ${path}`)
  cleanup()
})
test('WHAT[repository-programming-007] FILETOOLS_read_accepts_a_bare_string_payload', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'bare.txt')
  writeFileSync(path, 'bare payload')
  const output = await read(dir, JSON.stringify(path))
  assert.equal(output.result, 'bare payload')
  cleanup()
})
test('WHAT[repository-programming-007] FILETOOLS_read_falls_back_to_raw_payload_when_not_json', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'raw.txt')
  writeFileSync(path, 'raw content')
  const output = await read(dir, path)
  assert.equal(output.result, 'raw content')
  cleanup()
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { failureCatalog, validateAnchorDeclaration: validateDeclaration, validateAnchorOccurrence: validateOccurrence } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const declaration = (spec, occurrence) => ({ ...spec, occurrence })
const ok = (result) => result.ok
const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })

test('WHAT[repository-programming-007] JS006_empty_anchor_declaration_is_refused', () => {
  assert.equal(ok(validateDeclaration(declaration(exact(''), undefined))), false)
  assert.equal(ok(validateDeclaration(declaration(regex(''), undefined))), false)
  assert.equal(ok(validateDeclaration(declaration(exact('hello'), undefined))), true)
  assert.equal(ok(validateDeclaration(declaration(regex('^\\s*$'), undefined))), true)
})
test('WHAT[repository-programming-007] JS006_non_positive_occurrence_is_refused', () => {
  assert.equal(ok(validateOccurrence(declaration(exact('x'), 0))), false)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), -1))), false)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), 1))), true)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), undefined))), true)
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

test('WHAT[repository-programming-007] JS005_bindings_file_reads_utf8', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const binding = createApi(dir)
    const result = apiOf(binding).js.read('a.txt')
    assert.equal(result.ok, true)
    assert.equal(result.text, 'hello')
    assert.equal(result.byteCount, 5)
    const missing = apiOf(binding).js.read('nope.txt')
    assert.equal(missing.ok, false)
    assert.equal(missing.code, 'FILE_NOT_FOUND')
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { readUtf8, glob, findAnchor, requireUnique, grep, commitPlan, rollbackPlan } = await import("../../../dist/Repository/Programming/Js/FilesystemSurface.js");

const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jstools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const ok = (result) => result.ok
const codeOf = (result) => result.code
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[repository-programming-007] JS005_readUtf8_reads_and_classifies', () => {
  const { dir, cleanup } = sandbox()
  try {
    const file = join(dir, 'a.txt')
    writeFileSync(file, 'hello', 'utf8')
    assert.equal(unwrap(readUtf8(file)), 'hello')
    assert.equal(codeOf(readUtf8(join(dir, 'missing.txt'))), 'FILE_NOT_FOUND')
    // invalid UTF-8 bytes → INVALID_UTF8, never silent replacement
    writeFileSync(join(dir, 'bad.bin'), Buffer.from([0xff, 0xfe, 0xfd]))
    assert.equal(codeOf(readUtf8(join(dir, 'bad.bin'))), 'INVALID_UTF8')
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-007] JS006_findAnchor_ordered_string_and_regex', () => {
  const text = 'a b a b a'
  // exact, occurrence 1/2/3
  assert.deepEqual(unwrap(findAnchor(text, exact('a'), 1)), [0, 1])
  assert.deepEqual(unwrap(findAnchor(text, exact('a'), 2)), [4, 5])
  assert.deepEqual(unwrap(findAnchor(text, exact('a'), 3)), [8, 9])
  assert.equal(codeOf(findAnchor(text, exact('a'), 4)), 'ANCHOR_NOT_FOUND')
  // regex
  assert.deepEqual(unwrap(findAnchor(text, regex('b a'), 1)), [2, 5])
  assert.deepEqual(unwrap(findAnchor(text, regex('b a'), 2)), [6, 9])
  // zero-width: ^ anchors at absolute file start
  assert.deepEqual(unwrap(findAnchor(text, regex('^'), 1)), [0, 0])
  assert.equal(codeOf(findAnchor(text, regex('('), 1)), 'INVALID_ANCHOR_PATTERN')
})
test('WHAT[repository-programming-007] JS006_requireUnique_refuses_ambiguous_anchors', () => {
  const text = 'x y x'
  assert.deepEqual(unwrap(requireUnique(text, exact('y'))), [2, 3])
  assert.equal(codeOf(requireUnique(text, exact('x'))), 'ANCHOR_NOT_UNIQUE')
  assert.equal(codeOf(requireUnique(text, exact('z'))), 'ANCHOR_NOT_FOUND')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { parse: parseToml } = await import("smol-toml");
const { generate } = await import("../../../dist/Repository/Programming/Js/GeneratorSurface.js");
const { run, runObserved, caseName, rewritten, created, failureCode, render } = await import("../../../dist/Repository/Programming/Js/WorkflowSurface.js");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const { pending } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-workflow-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const localStore = () => {
  const owned = mkdtempSync(join(tmpdir(), 'wxs-workflow-events-'))
  const commonDir = join(owned, '.git')
  mkdirSync(commonDir, { recursive: true })
  const handle = createEventStore(commonDir, randomUUID().replaceAll('-', ''))
  return { handle, close: () => { disposeEventStore(handle); rmSync(owned, { recursive: true, force: true }) } }
}
const coderSurface = () => generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')
const runWorkflow = async (dir, program, { deadlineMs = 2000, store = null } = {}) => ({
  outcome: await run(dir, 'Coder', 'en', program, deadlineMs, Date.now() + 60_000, 1 << 20, store),
  surface: coderSurface(),
})

test('WHAT[repository-programming-007] JS006_missing_anchor_reason_names_declaration_path_and_pattern', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    await this.file('a.txt', [['begin', 'end', '## JS-007 FileView.text()']]);
    return { ok: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    const failed = parseToml(render(outcome))
    assert.equal(failed.reason.includes('anchor 1'), true)
    assert.equal(failed.reason.includes('a.txt'), true)
    assert.equal(failed.reason.includes('## JS-007 FileView.text()'), true)
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-007] JS005_offset_anchor_clips_to_closed_file_range', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const file = await this.file('a.txt', [['h', 'hend', 'hello']]);
    return {
      window: file.text('h', 'h+6'),
      before: file.text('hend-5', 'hend'),
      clippedEnd: file.text('h', 'h+1000'),
      clippedStart: file.text('^-1000', 'h'),
      eof: file.text('$+100', '$+200').length,
    };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    const doc = parseToml(render(outcome))
    assert.equal(doc.data.window, 'hello ')
    assert.equal(doc.data.before, 'hello')
    assert.equal(doc.data.clippedEnd, 'hello world')
    assert.equal(doc.data.clippedStart, '')
    assert.equal(doc.data.eof, 0)
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-007] JS005_offset_N_is_string_index_not_line_number', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'ab\ncd\nef', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const file = await this.file('a.txt');
    return {
      twoUnits: file.text('^', '^+2'),
      threeLen: file.text('^', '^+3').length,
    };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    const doc = parseToml(render(outcome))
    assert.equal(doc.data.twoUnits, 'ab')
    assert.equal(doc.data.threeLen, 3)
  } finally {
    cleanup()
  }
})
}
