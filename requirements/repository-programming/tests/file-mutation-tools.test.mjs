// tests/unit/tools/file-mutation-tools.test.mjs — VERIFY-009 coverage: mv / rm tool specs.
//
// The tools execute against the real filesystem in a per-test temp directory; only the
// HostToolFactory (schema DSL) and HostToolContext are faked, because the mv/rm bodies
// never touch either beyond argument decoding.

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync, existsSync, statSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { listItems } from '../../verification-system/tests/support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { mvSpec, rmSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/FileMutationTools.js')

// The spec builder only calls schema.string(); the real Host owns the rest of the DSL.
const fakeSchema = { string: () => ({ kind: 'string-schema' }) }
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (sessionId) => new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

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
        const [name, ...rest] = line.split(' = ')
        const raw = rest.join(' = ')
        return [name, raw.startsWith('"') ? JSON.parse(raw) : raw]
      }),
  )

const isDirectory = (path) => existsSync(path) && statSync(path).isDirectory()

test('FILEMUT_specs_carry_names_descriptions_and_arguments', () => {
  const mv = mvSpec(factory)
  const rm = rmSpec(factory)
  assert.equal(mv.Name, 'mv')
  assert.equal(rm.Name, 'rm')
  assert.ok(mv.Description.length > 0)
  assert.ok(rm.Description.length > 0)
  assert.deepEqual(
    listItems(mv.Arguments).map(([name]) => name),
    ['source', 'destination'],
  )
  assert.deepEqual(
    listItems(rm.Arguments).map(([name]) => name),
    ['path'],
  )
})

test('FILEMUT_mv_moves_a_file', async () => {
  const { dir, cleanup } = sandbox()
  const source = join(dir, 'alpha.txt')
  const destination = join(dir, 'beta.txt')
  writeFileSync(source, 'payload')

  const result = parseTomlFields(
    await mvSpec(factory).Execute(makeArgs({ source, destination }), context('ses-mv')),
  )

  assert.equal(result.moved, source)
  assert.equal(result.destination, destination)
  assert.equal(existsSync(source), false)
  assert.equal(existsSync(destination), true)
  cleanup()
})

test('FILEMUT_mv_renames_a_directory_with_contents', async () => {
  const { dir, cleanup } = sandbox()
  const source = join(dir, 'old-dir')
  const destination = join(dir, 'new-dir')
  mkdirSync(source)
  writeFileSync(join(source, 'inner.txt'), 'payload')

  const result = parseTomlFields(
    await mvSpec(factory).Execute(makeArgs({ source, destination }), context('ses-mv-dir')),
  )

  assert.equal(result.moved, source)
  assert.equal(existsSync(source), false)
  assert.equal(isDirectory(destination), true)
  assert.equal(existsSync(join(destination, 'inner.txt')), true)
  cleanup()
})

test('FILEMUT_mv_missing_source_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  const result = await mvSpec(factory).Execute(
    makeArgs({ source: join(dir, 'nope.txt'), destination: join(dir, 'x.txt') }),
    context('ses-mv-missing'),
  )
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /No such file or directory/)
  cleanup()
})

test('FILEMUT_mv_requires_source_and_destination', async () => {
  const { dir, cleanup } = sandbox()
  const spec = mvSpec(factory)

  const missingBoth = await spec.Execute(makeArgs({}), context('ses-mv-req'))
  assert.match(missingBoth, /source and destination are required/)

  const missingDestination = await spec.Execute(
    makeArgs({ source: join(dir, 'a.txt') }),
    context('ses-mv-req2'),
  )
  assert.match(missingDestination, /source and destination are required/)
  cleanup()
})

test('FILEMUT_rm_removes_a_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'trash.txt')
  writeFileSync(path, 'payload')

  const result = parseTomlFields(await rmSpec(factory).Execute(makeArgs({ path }), context('ses-rm')))

  assert.equal(result.removed, path)
  assert.equal(existsSync(path), false)
  cleanup()
})

test('FILEMUT_rm_removes_an_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'empty-dir')
  mkdirSync(path)

  const result = parseTomlFields(await rmSpec(factory).Execute(makeArgs({ path }), context('ses-rm-empty')))

  assert.equal(result.removed, path)
  assert.equal(existsSync(path), false)
  cleanup()
})

test('FILEMUT_rm_refuses_a_non_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'non-empty-dir')
  mkdirSync(path)
  writeFileSync(join(path, 'inner.txt'), 'payload')

  const result = await rmSpec(factory).Execute(makeArgs({ path }), context('ses-rm-nonempty'))

  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /directory not empty/)
  assert.equal(isDirectory(path), true)
  assert.equal(existsSync(join(path, 'inner.txt')), true)
  cleanup()
})

test('FILEMUT_rm_missing_path_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  const result = await rmSpec(factory).Execute(
    makeArgs({ path: join(dir, 'nope.txt') }),
    context('ses-rm-missing'),
  )
  assert.match(result, /No such file or directory/)
  cleanup()
})

test('FILEMUT_rm_requires_a_path', async () => {
  const { dir, cleanup } = sandbox()
  const result = await rmSpec(factory).Execute(makeArgs({}), context('ses-rm-req'))
  assert.match(result, /path is required/)
  cleanup()
})

test('FILEMUT_mv_rename_failure_surfaces_os_message', async () => {
  const { dir, cleanup } = sandbox()
  const source = join(dir, 'a.txt')
  writeFileSync(source, 'payload')
  // Rename a file onto an existing NON-EMPTY directory: POSIX rename fails.
  const blockedDir = join(dir, 'blocked')
  mkdirSync(blockedDir)
  writeFileSync(join(blockedDir, 'inner.txt'), 'payload')

  const result = await mvSpec(factory).Execute(
    makeArgs({ source, destination: blockedDir }),
    context('ses-mv-fail'),
  )

  assert.match(result, /mv: .+ -> .+: /)
  cleanup()
})
