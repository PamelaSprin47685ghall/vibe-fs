// tests/integration/plugin/file-mutation-tools.test.mjs — AGENT-016/017/018.
//
// Layer 3 (executable): the mv / rm tools registered by the plugin, driven through
// the real `hooks.tool.*.execute` path with a durable Authority Root naming the
// calling session's role (AGENT-007 layer two). The git-initialised workspace the
// fixture provides is the sandbox: absolute paths only, so the tools' behaviour is
// independent of the runner's cwd.
//
// Contract under test (docs/what/agent.md):
//   AGENT-016  mv / rm are Coder-only; every other role is denied at the gate.
//   AGENT-017  mv = POSIX mv: move/rename files and directories.
//   AGENT-018  rm = POSIX rm minus recursion: files and EMPTY directories are
//              removed; a non-empty directory is refused with an error.

import assert from 'node:assert/strict'
import { existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../unit/plugin/plugin-fixture.mjs'

const isDirectory = (path) => existsSync(path) && statSync(path).isDirectory()

test('AGENT_017_mv_moves_a_file', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-mv-file', 'fast-coder')
    const source = join(directory, 'alpha.txt')
    const destination = join(directory, 'beta.txt')
    writeFileSync(source, 'payload')

    const result = parseToml(
      await hooks.tool.mv.execute({ source, destination }, { sessionID: 'coder-mv-file', agent: 'fast-coder' }),
    )

    assert.equal(result.moved, source)
    assert.equal(result.destination, destination)
    assert.equal(existsSync(source), false, 'source must be gone after mv')
    assert.equal(existsSync(destination), true, 'destination must exist after mv')
  })
})

test('AGENT_017_mv_renames_a_directory', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-mv-dir', 'fast-coder')
    const source = join(directory, 'old-dir')
    const destination = join(directory, 'new-dir')
    mkdirSync(source)
    writeFileSync(join(source, 'inner.txt'), 'payload')

    const result = parseToml(
      await hooks.tool.mv.execute({ source, destination }, { sessionID: 'coder-mv-dir', agent: 'fast-coder' }),
    )

    assert.equal(result.moved, source)
    assert.equal(existsSync(source), false, 'source directory must be gone after mv')
    assert.equal(isDirectory(destination), true, 'destination must be a directory after mv')
    assert.equal(existsSync(join(destination, 'inner.txt')), true, 'directory contents must move with it')
  })
})

test('AGENT_017_mv_missing_source_returns_error', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-mv-missing', 'fast-coder')
    const result = parseToml(
      await hooks.tool.mv.execute(
        { source: join(directory, 'nope.txt'), destination: join(directory, 'x.txt') },
        { sessionID: 'coder-mv-missing', agent: 'fast-coder' },
      ),
    )
    assert.match(result.error, /No such file or directory/)
  })
})

test('AGENT_018_rm_removes_a_file', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-rm-file', 'fast-coder')
    const path = join(directory, 'trash.txt')
    writeFileSync(path, 'payload')

    const result = parseToml(
      await hooks.tool.rm.execute({ path }, { sessionID: 'coder-rm-file', agent: 'fast-coder' }),
    )

    assert.equal(result.removed, path)
    assert.equal(existsSync(path), false, 'file must be gone after rm')
  })
})

test('AGENT_018_rm_removes_an_empty_directory', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-rm-empty-dir', 'fast-coder')
    const path = join(directory, 'empty-dir')
    mkdirSync(path)

    const result = parseToml(
      await hooks.tool.rm.execute({ path }, { sessionID: 'coder-rm-empty-dir', agent: 'fast-coder' }),
    )

    assert.equal(result.removed, path)
    assert.equal(existsSync(path), false, 'empty directory must be gone after rm')
  })
})

test('AGENT_018_rm_refuses_a_non_empty_directory', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-rm-nonempty', 'fast-coder')
    const path = join(directory, 'non-empty-dir')
    mkdirSync(path)
    writeFileSync(join(path, 'inner.txt'), 'payload')

    const result = parseToml(
      await hooks.tool.rm.execute({ path }, { sessionID: 'coder-rm-nonempty', agent: 'fast-coder' }),
    )

    assert.match(result.error, /directory not empty/)
    assert.equal(isDirectory(path), true, 'non-empty directory must survive rm')
    assert.equal(existsSync(join(path, 'inner.txt')), true, 'contents must survive rm')
  })
})

test('AGENT_018_rm_missing_path_returns_error', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-rm-missing', 'fast-coder')
    const result = parseToml(
      await hooks.tool.rm.execute({ path: join(directory, 'nope.txt') }, { sessionID: 'coder-rm-missing', agent: 'fast-coder' }),
    )
    assert.match(result.error, /No such file or directory/)
  })
})

test('AGENT_016_mv_and_rm_are_denied_for_non_coder_roles', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    // Manager holds neither Move nor Remove (AGENT-006/016).
    acceptAuthorityRoot(runtime, 'manager-mv-rm', 'fast-manager')
    const context = { sessionID: 'manager-mv-rm', agent: 'fast-manager' }

    const mvResult = parseToml(
      await hooks.tool.mv.execute(
        { source: join(directory, 'a.txt'), destination: join(directory, 'b.txt') },
        context,
      ),
    )
    assert.match(mvResult.error, /not permitted for role/)

    const rmResult = parseToml(
      await hooks.tool.rm.execute({ path: join(directory, 'a.txt') }, context),
    )
    assert.match(rmResult.error, /not permitted for role/)
  })
})

test('AGENT_016_mv_and_rm_are_denied_when_the_role_is_unresolved', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    // No Authority Root: AGENT-007 layer two fail-closed — the tool must not run.
    const context = { sessionID: 'unresolved-mv-rm', agent: 'fast-manager' }

    const mvResult = parseToml(
      await hooks.tool.mv.execute(
        { source: join(directory, 'a.txt'), destination: join(directory, 'b.txt') },
        context,
      ),
    )
    assert.match(mvResult.error, /no Authority Root fixes this session's role/)

    const rmResult = parseToml(
      await hooks.tool.rm.execute({ path: join(directory, 'a.txt') }, context),
    )
    assert.match(rmResult.error, /no Authority Root fixes this session's role/)
  })
})
