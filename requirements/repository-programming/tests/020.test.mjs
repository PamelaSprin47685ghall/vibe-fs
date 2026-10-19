import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync, existsSync, statSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { generateSurfaceForRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { run, caseName, failureCode, failureReason, removed, moved } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

// Superseded note: the standalone plugin `mv`/`rm` ToolSpecs (`FileMutationSurface`) are retired;
// the js programming surface's `mv(from, to)` and `rm(path)` members (projected from Move/Remove
// permissions) are the POSIX file-mutation contract now.

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-filemut-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const isDirectory = (path) => existsSync(path) && statSync(path).isDirectory()

const runProgram = (dir, body, role = 'Engineer') =>
  run(
    dir,
    role,
    'en',
    `class Js extends JsProgram {\n  async run() {\n${body}\n    return { ok: true };\n  }\n}`,
    2000,
    Date.now() + 60_000,
    1 << 20,
    null,
  )

test('WHAT[repository-programming-020] FILEMUT_specs_carry_names_descriptions_and_arguments', () => {
  const engineer = generateSurfaceForRole('Engineer', ['Read', 'Write', 'Move', 'Remove'])
  const mv = engineer.members.find((m) => m.memberName === 'mv')
  const rm = engineer.members.find((m) => m.memberName === 'rm')
  assert.ok(mv, 'mv member projected for Move permission')
  assert.ok(rm, 'rm member projected for Remove permission')
  assert.equal(mv.runtimeBindingKey, 'js.mv')
  assert.equal(rm.runtimeBindingKey, 'js.rm')
  assert.ok(mv.description.length > 0)
  assert.ok(rm.description.length > 0)
  assert.match(engineer.baseClassSource, /mv\(from,\s*to\)/)
  assert.match(engineer.baseClassSource, /rm\(path\)/)

  const manager = generateSurfaceForRole('Manager', ['Read', 'Glob'])
  assert.equal(manager.members.some((m) => m.memberName === 'mv' || m.memberName === 'rm'), false)
})

test('WHAT[repository-programming-020] FILEMUT_mv_moves_a_file', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'alpha.txt'), 'payload')
    const outcome = await runProgram(dir, `    this.mv('alpha.txt', 'beta.txt');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(moved(outcome), ['alpha.txt -> beta.txt'])
    assert.equal(existsSync(join(dir, 'alpha.txt')), false)
    assert.equal(readFileSync(join(dir, 'beta.txt'), 'utf8'), 'payload')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-020] FILEMUT_mv_renames_a_directory_with_contents', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const source = join(dir, 'old-dir')
    const destination = join(dir, 'new-dir')
    mkdirSync(source)
    writeFileSync(join(source, 'inner.txt'), 'payload')
    const outcome = await runProgram(dir, `    this.mv('old-dir', 'new-dir');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(moved(outcome), ['old-dir -> new-dir'])
    assert.equal(existsSync(source), false)
    assert.equal(isDirectory(destination), true)
    assert.equal(existsSync(join(destination, 'inner.txt')), true)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-020] FILEMUT_mv_missing_source_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    this.mv('nope.txt', 'x.txt');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_NOT_FOUND')
    assert.match(failureReason(outcome), /nope\.txt/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-020] FILEMUT_rm_removes_a_file', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const path = join(dir, 'trash.txt')
    writeFileSync(path, 'payload')
    const outcome = await runProgram(dir, `    this.rm('trash.txt');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(removed(outcome), ['trash.txt'])
    assert.equal(existsSync(path), false)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-020] FILEMUT_rm_removes_an_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const path = join(dir, 'empty-dir')
    mkdirSync(path)
    const outcome = await runProgram(dir, `    this.rm('empty-dir');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(removed(outcome), ['empty-dir'])
    assert.equal(existsSync(path), false)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-020] FILEMUT_rm_refuses_a_non_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const path = join(dir, 'non-empty-dir')
    mkdirSync(path)
    writeFileSync(join(path, 'inner.txt'), 'payload')
    const outcome = await runProgram(dir, `    this.rm('non-empty-dir');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'DIRECTORY_NOT_EMPTY')
    assert.equal(isDirectory(path), true)
    assert.equal(existsSync(join(path, 'inner.txt')), true)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-020] FILEMUT_rm_missing_path_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    this.rm('nope.txt');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_NOT_FOUND')
    assert.match(failureReason(outcome), /nope\.txt/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-020] FILEMUT_mv_rename_failure_surfaces_os_message', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const source = join(dir, 'a.txt')
    writeFileSync(source, 'payload')
    const blockedDir = join(dir, 'blocked')
    mkdirSync(blockedDir)
    writeFileSync(join(blockedDir, 'inner.txt'), 'payload')
    const outcome = await runProgram(dir, `    this.mv('a.txt', 'blocked');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_READ_FAILED')
    assert.equal(existsSync(source), true)
  } finally {
    cleanup()
  }
})
