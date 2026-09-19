import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync, existsSync, statSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
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

test('WHAT[repository-programming-020] FILEMUT_specs_carry_names_descriptions_and_arguments', () => {
  const mv = createMv(toolModule())
  const rm = createRm(toolModule())
  assert.equal(name(mv), 'mv')
  assert.equal(name(rm), 'rm')
  assert.ok(description(mv).length > 0)
  assert.ok(description(rm).length > 0)
  assert.deepEqual(argumentNames(mv), ['source', 'destination'])
  assert.deepEqual(argumentNames(rm), ['path'])
})

test('WHAT[repository-programming-020] FILEMUT_mv_moves_a_file', async () => {
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

test('WHAT[repository-programming-020] FILEMUT_mv_renames_a_directory_with_contents', async () => {
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

test('WHAT[repository-programming-020] FILEMUT_mv_missing_source_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  const result = await execute(
    createMv(toolModule()),
    { source: join(dir, 'nope.txt'), destination: join(dir, 'x.txt') },
    context('ses-mv-missing'),
  )
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /No such file or directory|没有那个文件或目录/)
  cleanup()
})

test('WHAT[repository-programming-020] FILEMUT_mv_requires_source_and_destination', async () => {
  const { dir, cleanup } = sandbox()
  const mv = createMv(toolModule())
  const missingBoth = await execute(mv, {}, context('ses-mv-req'))
  assert.match(missingBoth, /source and destination are required|必须提供 source 与 destination/)
  const missingDestination = await execute(mv, { source: join(dir, 'a.txt') }, context('ses-mv-req2'))
  assert.match(missingDestination, /source and destination are required|必须提供 source 与 destination/)
  cleanup()
})

test('WHAT[repository-programming-020] FILEMUT_rm_removes_a_file', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'trash.txt')
  writeFileSync(path, 'payload')
  const result = parseTomlFields(await execute(createRm(toolModule()), { path }, context('ses-rm')))
  assert.equal(result.removed, path)
  assert.equal(existsSync(path), false)
  cleanup()
})

test('WHAT[repository-programming-020] FILEMUT_rm_removes_an_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'empty-dir')
  mkdirSync(path)
  const result = parseTomlFields(await execute(createRm(toolModule()), { path }, context('ses-rm-empty')))
  assert.equal(result.removed, path)
  assert.equal(existsSync(path), false)
  cleanup()
})

test('WHAT[repository-programming-020] FILEMUT_rm_refuses_a_non_empty_directory', async () => {
  const { dir, cleanup } = sandbox()
  const path = join(dir, 'non-empty-dir')
  mkdirSync(path)
  writeFileSync(join(path, 'inner.txt'), 'payload')
  const result = await execute(createRm(toolModule()), { path }, context('ses-rm-nonempty'))
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /directory not empty|目录非空/)
  assert.equal(isDirectory(path), true)
  assert.equal(existsSync(join(path, 'inner.txt')), true)
  cleanup()
})

test('WHAT[repository-programming-020] FILEMUT_rm_missing_path_returns_error', async () => {
  const { dir, cleanup } = sandbox()
  const result = await execute(createRm(toolModule()), { path: join(dir, 'nope.txt') }, context('ses-rm-missing'))
  assert.match(result, /No such file or directory|没有那个文件或目录/)
  cleanup()
})

test('WHAT[repository-programming-020] FILEMUT_rm_requires_a_path', async () => {
  const { dir, cleanup } = sandbox()
  const result = await execute(createRm(toolModule()), {}, context('ses-rm-req'))
  assert.match(result, /path is required|必须提供 path/)
  cleanup()
})

test('WHAT[repository-programming-020] FILEMUT_mv_rename_failure_surfaces_os_message', async () => {
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

{
const { parse: parseToml } = await import('smol-toml')
const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')

integrationTest('WHAT[repository-programming-020] AGENT_017_mv_moves_a_file', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-mv-file', 'engineer')
    const source = join(directory, 'alpha.txt')
    const destination = join(directory, 'beta.txt')
    writeFileSync(source, 'payload')

    const result = parseToml(
      await hooks.tool.mv.execute({ source, destination }, { sessionID: 'engineer-mv-file', agent: 'engineer' }),
    )

    assert.equal(result.moved, source)
    assert.equal(result.destination, destination)
    assert.equal(existsSync(source), false, 'source must be gone after mv')
    assert.equal(existsSync(destination), true, 'destination must exist after mv')
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_017_mv_renames_a_directory', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-mv-dir', 'engineer')
    const source = join(directory, 'old-dir')
    const destination = join(directory, 'new-dir')
    mkdirSync(source)
    writeFileSync(join(source, 'inner.txt'), 'payload')

    const result = parseToml(
      await hooks.tool.mv.execute({ source, destination }, { sessionID: 'engineer-mv-dir', agent: 'engineer' }),
    )

    assert.equal(result.moved, source)
    assert.equal(existsSync(source), false, 'source directory must be gone after mv')
    assert.equal(isDirectory(destination), true, 'destination must be a directory after mv')
    assert.equal(existsSync(join(destination, 'inner.txt')), true, 'directory contents must move with it')
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_017_mv_missing_source_returns_error', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-mv-missing', 'engineer')
    const text = await hooks.tool.mv.execute(
      { source: join(directory, 'nope.txt'), destination: join(directory, 'x.txt') },
      { sessionID: 'engineer-mv-missing', agent: 'engineer' },
    )
    assert.match(text, /No such file or directory/)
    assert.equal(parseToml(text).error, undefined)
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_018_rm_removes_a_file', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-rm-file', 'engineer')
    const path = join(directory, 'trash.txt')
    writeFileSync(path, 'payload')

    const result = parseToml(
      await hooks.tool.rm.execute({ path }, { sessionID: 'engineer-rm-file', agent: 'engineer' }),
    )

    assert.equal(result.removed, path)
    assert.equal(existsSync(path), false, 'file must be gone after rm')
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_018_rm_removes_an_empty_directory', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-rm-empty-dir', 'engineer')
    const path = join(directory, 'empty-dir')
    mkdirSync(path)

    const result = parseToml(
      await hooks.tool.rm.execute({ path }, { sessionID: 'engineer-rm-empty-dir', agent: 'engineer' }),
    )

    assert.equal(result.removed, path)
    assert.equal(existsSync(path), false, 'empty directory must be gone after rm')
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_018_rm_refuses_a_non_empty_directory', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-rm-nonempty', 'engineer')
    const path = join(directory, 'non-empty-dir')
    mkdirSync(path)
    writeFileSync(join(path, 'inner.txt'), 'payload')

    const text = await hooks.tool.rm.execute({ path }, { sessionID: 'engineer-rm-nonempty', agent: 'engineer' })

    assert.match(text, /directory not empty/)
    assert.equal(parseToml(text).error, undefined)
    assert.equal(isDirectory(path), true, 'non-empty directory must survive rm')
    assert.equal(existsSync(join(path, 'inner.txt')), true, 'contents must survive rm')
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_018_rm_missing_path_returns_error', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-rm-missing', 'engineer')
    const text = await hooks.tool.rm.execute(
      { path: join(directory, 'nope.txt') },
      { sessionID: 'engineer-rm-missing', agent: 'engineer' },
    )
    assert.match(text, /No such file or directory/)
    assert.equal(parseToml(text).error, undefined)
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_016_mv_and_rm_are_denied_for_non_coder_roles', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'manager-mv-rm', 'manager')
    const context = { sessionID: 'manager-mv-rm', agent: 'manager' }

    const mvResult = await hooks.tool.mv.execute(
      { source: join(directory, 'a.txt'), destination: join(directory, 'b.txt') },
      context,
    )
    assert.match(mvResult, /mv is not available to Manager\./)
    assert.equal(parseToml(mvResult).error, undefined)

    const rmResult = await hooks.tool.rm.execute({ path: join(directory, 'a.txt') }, context)
    assert.match(rmResult, /rm is not available to Manager\./)
    assert.equal(parseToml(rmResult).error, undefined)
  })
})

integrationTest('WHAT[repository-programming-020] AGENT_016_mv_and_rm_are_denied_when_the_role_is_unresolved', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const context = { sessionID: 'unresolved-mv-rm', agent: 'manager' }

    const mvResult = await hooks.tool.mv.execute(
      { source: join(directory, 'a.txt'), destination: join(directory, 'b.txt') },
      context,
    )
    assert.match(mvResult, /This tool is unavailable until the caller's authority is established\./)
    assert.equal(parseToml(mvResult).error, undefined)

    const rmResult = await hooks.tool.rm.execute({ path: join(directory, 'a.txt') }, context)
    assert.match(rmResult, /This tool is unavailable until the caller's authority is established\./)
    assert.equal(parseToml(rmResult).error, undefined)
  })
})
}
