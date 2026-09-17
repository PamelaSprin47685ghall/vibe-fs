import assert from 'node:assert/strict'
import { existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const isDirectory = (path) => existsSync(path) && statSync(path).isDirectory()

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_017_mv_moves_a_file', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_017_mv_renames_a_directory', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_017_mv_missing_source_returns_error', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_018_rm_removes_a_file', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_018_rm_removes_an_empty_directory', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_018_rm_refuses_a_non_empty_directory', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_018_rm_missing_path_returns_error', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_016_mv_and_rm_are_denied_for_non_coder_roles', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    // Manager holds neither Move nor Remove (AGENT-006/016).
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

test('WHAT[REPOSITORY-PROGRAMMING-020] AGENT_016_mv_and_rm_are_denied_when_the_role_is_unresolved', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    // No Authority Root: AGENT-007 layer two fail-closed — the tool must not run.
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
