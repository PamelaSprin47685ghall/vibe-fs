// VERIFY-009 coverage: mv / rm tool specs through the registered owner
// boundary. Node fs is used only for per-test fixture setup/observation.

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync, existsSync, statSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  createMv,
  createRm,
  name,
  description,
  argumentNames,
  execute,
} from '../../../dist/OpenCode/Tools/FileMutationSurface.js'

const toolModule = () => {
  const tool = (definition) => definition
  tool.schema = { string: () => ({ kind: 'string-schema' }) }
  return { tool }
}
const context = (sessionID) => ({ sessionID, agent: 'coder' })

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-filemut-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const parseTomlFields = (text) =>
  Object.fromEntries(
    text
      .split('\n')
      .filter((line) => line.includes(' = '))
      .map((line) => {
        const [field, ...rest] = line.split(' = ')
        const raw = rest.join(' = ')
        return [field, raw.startsWith('"') ? JSON.parse(raw) : raw]
      }),
  )

const isDirectory = (path) => existsSync(path) && statSync(path).isDirectory()

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_specs_carry_names_descriptions_and_arguments', () => {
  const mv = createMv(toolModule())
  const rm = createRm(toolModule())
  assert.equal(name(mv), 'mv')
  assert.equal(name(rm), 'rm')
  assert.ok(description(mv).length > 0)
  assert.ok(description(rm).length > 0)
  assert.deepEqual(argumentNames(mv), ['source', 'destination'])
  assert.deepEqual(argumentNames(rm), ['path'])
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_moves_a_file', async () => {
  const { dir, cleanup } = sandbox()
  const source = join(dir, 'alpha.txt')
  const destination = join(dir, 'beta.txt')
  writeFileSync(source, 'payload')
  const result = parseTomlFields(await execute(createMv(toolModule()), { source, destination }, context('ses-mv')))
  assert.equal(result.moved, source)
  assert.equal(result.destination, destination)
  assert.equal(existsSync(source), false)
  assert.equal(existsSync(destination), true)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_renames_a_directory_with_contents', async () => {
  const { dir, cleanup } = sandbox()
  const source = join(dir, 'old-dir')
  const destination = join(dir, 'new-dir')
  mkdirSync(source)
  writeFileSync(join(source, 'inner.txt'), 'payload')
  const result = parseTomlFields(await execute(createMv(toolModule()), { source, destination }, context('ses-mv-dir')))
  assert.equal(result.moved, source)
  assert.equal(existsSync(source), false)
  assert.equal(isDirectory(destination), true)
  assert.equal(existsSync(join(destination, 'inner.txt')), true)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_missing_source_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  const result = await execute(
    createMv(toolModule()),
    { source: join(dir, 'nope.txt'), destination: join(dir, 'x.txt') },
    context('ses-mv-missing'),
  )
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /No such file or directory/)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_requires_source_and_destination', async () => {
  const { dir, cleanup } = sandbox()
  const mv = createMv(toolModule())
  const missingBoth = await execute(mv, {}, context('ses-mv-req'))
  assert.match(missingBoth, /source and destination are required/)
  const missingDestination = await execute(mv, { source: join(dir, 'a.txt') }, context('ses-mv-req2'))
  assert.match(missingDestination, /source and destination are required/)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_removes_a_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'trash.txt')
  writeFileSync(path, 'payload')
  const result = parseTomlFields(await execute(createRm(toolModule()), { path }, context('ses-rm')))
  assert.equal(result.removed, path)
  assert.equal(existsSync(path), false)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_removes_an_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'empty-dir')
  mkdirSync(path)
  const result = parseTomlFields(await execute(createRm(toolModule()), { path }, context('ses-rm-empty')))
  assert.equal(result.removed, path)
  assert.equal(existsSync(path), false)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_refuses_a_non_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'non-empty-dir')
  mkdirSync(path)
  writeFileSync(join(path, 'inner.txt'), 'payload')
  const result = await execute(createRm(toolModule()), { path }, context('ses-rm-nonempty'))
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /directory not empty/)
  assert.equal(isDirectory(path), true)
  assert.equal(existsSync(join(path, 'inner.txt')), true)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_missing_path_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  const result = await execute(createRm(toolModule()), { path: join(dir, 'nope.txt') }, context('ses-rm-missing'))
  assert.match(result, /No such file or directory/)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_requires_a_path', async () => {
  const { dir, cleanup } = sandbox()
  const result = await execute(createRm(toolModule()), {}, context('ses-rm-req'))
  assert.match(result, /path is required/)
  cleanup()
})

test('WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_rename_failure_surfaces_os_message', async () => {
  const { dir, cleanup } = sandbox()
  const source = join(dir, 'a.txt')
  writeFileSync(source, 'payload')
  const blockedDir = join(dir, 'blocked')
  mkdirSync(blockedDir)
  writeFileSync(join(blockedDir, 'inner.txt'), 'payload')
  const result = await execute(createMv(toolModule()), { source, destination: blockedDir }, context('ses-mv-fail'))
  assert.match(result, /mv: .+ -> .+: /)
  cleanup()
})
