import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'
import { run, caseName, failureCode, rewritten, created, removed, moved, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import { pending, failureCatalog } from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rmmv-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const localStore = () => {
  const owned = mkdtempSync(join(tmpdir(), 'wxs-rmmv-events-'))
  const handle = createEventStore(owned, randomUUID().replaceAll('-', ''))
  return { handle, close: () => { disposeEventStore(handle); rmSync(owned, { recursive: true, force: true }) } }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

// Engineer carries Move/Remove (office-capability); Coder falls back to
// Engineer permissions in the generator, so either works. Engineer is explicit.
const execute = (dir, body, store = null) =>
  run(dir, 'Engineer', 'en', program(body), 2000, Date.now() + 60_000, 1 << 20, store)

test('WHAT[repository-programming-040] rm deletes a file as-is and reports removed', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'scratch.tmp'), 'data', 'utf8')
    const outcome = await execute(dir, `    this.rm('scratch.tmp');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(existsSync(join(dir, 'scratch.tmp')), false)
    assert.deepEqual(removed(outcome), ['scratch.tmp'])
    assert.deepEqual(rewritten(outcome), [])
    assert.deepEqual(created(outcome), [])
    assert.deepEqual(moved(outcome), [])
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] rm refuses a non-empty directory', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'keep'))
    writeFileSync(join(dir, 'keep', 'a.txt'), 'a', 'utf8')
    const outcome = await execute(dir, `    this.rm('keep');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'DIRECTORY_NOT_EMPTY')
    assert.equal(existsSync(join(dir, 'keep', 'a.txt')), true, 'non-empty directory must survive')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] rm removes an empty directory', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'empty'))
    const outcome = await execute(dir, `    this.rm('empty');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(existsSync(join(dir, 'empty')), false)
    assert.deepEqual(removed(outcome), ['empty'])
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] rm reports FILE_NOT_FOUND for a missing target', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await execute(dir, `    this.rm('missing.tmp');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_NOT_FOUND')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] mv moves a file and reports moved/destination', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'old.js'), 'export const x = 1;', 'utf8')
    const outcome = await execute(dir, `    this.mv('old.js', 'new.js');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(existsSync(join(dir, 'old.js')), false)
    assert.equal(readFileSync(join(dir, 'new.js'), 'utf8'), 'export const x = 1;')
    assert.deepEqual(moved(outcome), ['old.js -> new.js'])
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] mv overwrites an existing destination (POSIX mv)', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'src.txt'), 'source', 'utf8')
    writeFileSync(join(dir, 'dst.txt'), 'old-destination', 'utf8')
    const outcome = await execute(dir, `    this.mv('src.txt', 'dst.txt');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(existsSync(join(dir, 'src.txt')), false)
    assert.equal(readFileSync(join(dir, 'dst.txt'), 'utf8'), 'source')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] mv reports FILE_NOT_FOUND for a missing source', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await execute(dir, `    this.mv('missing.js', 'new.js');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_NOT_FOUND')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] mv moves a directory', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src-dir'))
    writeFileSync(join(dir, 'src-dir', 'a.txt'), 'a', 'utf8')
    const outcome = await execute(dir, `    this.mv('src-dir', 'dst-dir');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(existsSync(join(dir, 'src-dir')), false)
    assert.equal(readFileSync(join(dir, 'dst-dir', 'a.txt'), 'utf8'), 'a')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] rm and mv reject paths outside the capability boundary', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const rmOutcome = await execute(dir, `    this.rm('../escape.tmp');`)
    assert.equal(caseName(rmOutcome), 'Failed')
    assert.equal(failureCode(rmOutcome), 'PATH_DENIED')

    writeFileSync(join(dir, 'a.txt'), 'a', 'utf8')
    const mvOutcome = await execute(dir, `    this.mv('a.txt', '../escape.txt');`)
    assert.equal(caseName(mvOutcome), 'Failed')
    assert.equal(failureCode(mvOutcome), 'PATH_DENIED')
    assert.equal(existsSync(join(dir, 'a.txt')), true, 'source must survive a refused move')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] mv rejects a destination outside the boundary even when the source is valid', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'a', 'utf8')
    const outcome = await execute(dir, `    this.mv('a.txt', 'sub/../../escape.txt');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'PATH_DENIED')
    assert.equal(existsSync(join(dir, 'a.txt')), true, 'source must survive a refused move')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-040] rm and mv record a single durable entry each', async () => {
  const { dir, cleanup } = sandbox()
  const store = localStore()
  try {
    writeFileSync(join(dir, 'gone.txt'), 'data', 'utf8')
    writeFileSync(join(dir, 'from.txt'), 'from', 'utf8')

    const rmOutcome = await execute(dir, `    this.rm('gone.txt');`, store.handle)
    assert.equal(caseName(rmOutcome), 'Succeeded')
    const mvOutcome = await execute(dir, `    this.mv('from.txt', 'to.txt');`, store.handle)
    assert.equal(caseName(mvOutcome), 'Succeeded')

    // Each invocation is its own transaction; the durable projection holds the
    // Prepared facts until their matching Committed arrives (the workflow
    // appends both, so the pending set drains to empty).
    const pendingAfter = pending(store.handle)
    assert.equal(pendingAfter.length, 0, 'Prepared/Committed pair drains the pending projection')

    // The rendered result names the effects.
    assert.match(render(rmOutcome), /removed = \["gone\.txt"\]/)
    assert.match(render(mvOutcome), /moved = \["from\.txt -> to\.txt"\]/)
  } finally {
    store.close()
    cleanup()
  }
})

test('WHAT[repository-programming-040] rm and mv stable failure codes are catalogued', () => {
  // A real EXDEV requires a second mount point, which CI cannot guarantee. The
  // stable code is part of the frozen failure catalog; the renameSync atomicity
  // that makes EXDEV leave neither side behind is the host tool's proven POSIX
  // baseline (repository-programming-020).
  const codes = failureCatalog().map((entry) => entry.code)
  for (const code of ['DIRECTORY_NOT_EMPTY', 'CROSS_DEVICE_MOVE', 'INVALID_ARGUMENT']) {
    assert.equal(codes.includes(code), true, `missing stable code ${code}`)
  }
})

test('WHAT[repository-programming-040] rm accepts a target that was read via file() with a fresh snapshot', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'guarded.txt'), 'snapshot', 'utf8')
    const body = `    const seen = await this.cat('guarded.txt');
    this.rm('guarded.txt');
    return { seen };`
    const outcome = await execute(dir, body)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(existsSync(join(dir, 'guarded.txt')), false, 'rm lands as-is when the snapshot is fresh')
    assert.deepEqual(removed(outcome), ['guarded.txt'])
  } finally {
    cleanup()
  }
})
