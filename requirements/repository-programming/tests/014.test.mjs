import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { caseName, failureCode, failureReason, rewritten, run, runObserved } = await import("../../../dist/Repository/Programming/Js/WorkflowSurface.js");
const { generate } = await import("../../../dist/Repository/Programming/Js/GeneratorSurface.js");
const { api: runtimeApi, createApi: createRuntimeApi, readPaths: runtimeReadPaths, run: runRuntime, stagedCount, stagedKinds } = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-edit-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`
const execute = (dir, body, language = 'en') =>
  run(dir, 'Coder', language, program(body), 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-014] JS_EDIT_target_read_is_observed_and_external_change_wins', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    const outcome = await runObserved(
      dir,
      'Coder',
      'en',
      program(`    this.edit('a.txt', { find: 'alpha', put: 'beta' });`),
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      async (readPaths, effectPaths) => {
        assert.deepEqual(readPaths, ['a.txt'])
        assert.deepEqual(effectPaths, ['a.txt'])
        writeFileSync(join(dir, 'a.txt'), 'external\n', 'utf8')
      },
    )

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'external\n')
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

test('WHAT[repository-programming-014] JS014_commitPlan_rejects_a_create_race_without_overwriting', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'new.txt'), 'external', 'utf8')
    const result = commitPlan(dir, [{ kind: 'create', path: 'new.txt', newText: 'tool' }])
    assert.equal(codeOf(result), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'new.txt'), 'utf8'), 'external')
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-014] JS014_commitPlan_rejects_a_stale_rewrite_without_overwriting', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'external', 'utf8')
    const result = commitPlan(dir, [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'snapshot', newText: 'tool' },
    ])
    assert.equal(codeOf(result), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'external')
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

test('WHAT[repository-programming-014] JS014_stale_rewrite_is_a_conflict_with_no_retry', () => {
  const fresh = [rewrite('a.txt', 'current', 'new')]
  const stale = [rewrite('a.txt', 'old', 'new')]
  assert.equal(ok(validateFreshness(current, fresh)), true)
  assert.equal(codeOf(validateFreshness(current, stale)), 'FILE_CHANGED')
  // create targets are not freshness-checked
  assert.equal(ok(validateFreshness(current, [create('b.txt', 'n')])), true)
})
test('WHAT[repository-programming-014] JS014_preflight_covers_read_only_snapshots_and_create_absence', () => {
  const changedRead = preflight(
    ['dependency.txt'],
    { 'dependency.txt': 'external' },
    [{ path: 'dependency.txt', text: 'snapshot' }],
    [],
  )
  assert.equal(codeOf(changedRead), 'FILE_CHANGED')
  assert.equal(codeOf(preflight(['new.txt'], { 'new.txt': 'external' }, [], [create('new.txt', 'tool')])), 'FILE_CHANGED')
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

test('WHAT[repository-programming-014] JS014_workflow_rejects_a_changed_read_only_dependency', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'dependency.txt'), 'snapshot', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const dependency = await this.file('dependency.txt');
    this.write('output.txt', dependency.text());
    return { ok: true };
  }
}`
    let observations = 0
    const outcome = await runObserved(
      dir,
      'Coder',
      'en',
      program,
      2000,
      4_102_444_800_000,
      1 << 20,
      null,
      async (readPaths, effectPaths) => {
        observations += 1
        assert.deepEqual(readPaths, ['dependency.txt'])
        assert.deepEqual(effectPaths, ['output.txt'])
        writeFileSync(join(dir, 'dependency.txt'), 'external', 'utf8')
      },
    )

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'dependency.txt'), 'utf8'), 'external')
    assert.equal(existsSync(join(dir, 'output.txt')), false)
    assert.equal(observations, 1)
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-014] JS014_workflow_rejects_a_create_target_added_after_staging', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const program = `class Js extends JsProgram {
  async run() {
    this.write('new.txt', 'tool');
    return { ok: true };
  }
}`
    const outcome = await runObserved(
      dir,
      'Coder',
      'en',
      program,
      2000,
      4_102_444_800_000,
      1 << 20,
      null,
      async () => writeFileSync(join(dir, 'new.txt'), 'external', 'utf8'),
    )

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'new.txt'), 'utf8'), 'external')
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-014] JS014_workflow_tracks_every_file_scanned_by_grep', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'source.txt'), 'needle', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const matches = await this.grep('needle', '*.txt');
    this.write('output.txt', String(matches.length));
    return { count: matches.length };
  }
}`
    const outcome = await runObserved(
      dir,
      'Coder',
      'en',
      program,
      2000,
      4_102_444_800_000,
      1 << 20,
      null,
      async (readPaths) => {
        assert.deepEqual(readPaths, ['source.txt'])
        writeFileSync(join(dir, 'source.txt'), 'external', 'utf8')
      },
    )

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_CHANGED')
    assert.equal(existsSync(join(dir, 'output.txt')), false)
  } finally {
    cleanup()
  }
})
}
