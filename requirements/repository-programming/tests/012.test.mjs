import test from 'node:test'

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

test('WHAT[REPOSITORY-PROGRAMMING-012] JS008_012_bindings_rewrite_stages_without_touching_disk', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old text', 'utf8')
    const binding = createApi(dir)
    const result = apiOf(binding).js.edit('a.txt', 'new text')
    assert.equal(result.ok, true)
    assert.equal(stagedCount(binding), 1)
    const disk = apiOf(binding).js.read('a.txt')
    assert.equal(disk.text, 'old text')
  } finally {
    cleanup()
  }
})
test('WHAT[REPOSITORY-PROGRAMMING-012] JS009_012_bindings_write_leaves_disk_untouched', () => {
  const { dir, cleanup } = sandbox()
  try {
    const binding = createApi(dir)
    const result = apiOf(binding).js.write('new.txt', 'fresh')
    assert.equal(result.ok, true)
    const disk = apiOf(binding).js.read('new.txt')
    assert.equal(disk.ok, false)
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const { appendPrepared, appendCommitted, pending } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-txstore-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const localStore = (commonDir) => {
  const owned = commonDir ?? mkdtempSync(join(tmpdir(), 'wxs-txstore-events-'))
  const gitCommonDir = join(owned, '.git')
  mkdirSync(gitCommonDir, { recursive: true })
  const handle = createEventStore(gitCommonDir, randomUUID().replaceAll('-', ''))
  return { handle, owned, close: () => { disposeEventStore(handle); if (!commonDir) rmSync(owned, { recursive: true, force: true }) } }
}
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result
}
const mutation = (path, originalText, newText) => ({
  path,
  originalText,
  newText,
})
const prepared = (id, root, mutations) => ({
  transactionId: id,
  workspaceRoot: root,
  mutations,
})

test('WHAT[REPOSITORY-PROGRAMMING-012] JS012_prepare_then_commit_updates_only_integrator_Current', async () => {
  const local = localStore()
  try {
    const p = prepared('tx-1', '/ws', [mutation('a.txt', 'old', 'new')])
    unwrap(await appendPrepared(local.handle, p))
    assert.equal(pending(local.handle).length, 1)

    unwrap(await appendCommitted(local.handle, 'tx-1'))
    assert.deepEqual(pending(local.handle), [])
  } finally {
    local.close()
  }
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

test('WHAT[REPOSITORY-PROGRAMMING-012] JS012_workflow_with_store_persists_prepare_and_commit', async () => {
  const { dir, cleanup } = sandbox()
  const local = localStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const file = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', file.text('^', 'begin') + 'goodbye' + file.text('end', '$'));
    return { done: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program, { store: local.handle })
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed to disk')
    assert.deepEqual(pending(local.handle), [], 'no uncommitted transaction remains in Integrator Current')
  } finally {
    local.close()
    cleanup()
  }
})
}
