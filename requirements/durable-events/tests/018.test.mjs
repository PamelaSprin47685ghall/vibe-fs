import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const Hook = await import('../../../dist/Git/Hook/Surface.js')

const MARKER = 'wanxiang-hook-dispatcher'

const read = (relative) => readFileSync(new URL(`../../../${relative}`, import.meta.url), 'utf8')

const sandboxHooks = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hooks-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[durable-events-018] HOOK_activation_ensure_installs_both_hooks_and_remote_fetch_refspec_without_running_sync', async () => {
  const { execFileSync } = await import('node:child_process')
  const repo = mkdtempSync(join(tmpdir(), 'wxs-hook-ensure-'))
  try {
    execFileSync('git', ['init', '--quiet', repo])
    execFileSync('git', ['-C', repo, 'remote', 'add', 'origin', 'https://example.com/repo.git'])

    const ok = Hook.ensure(repo)
    assert.equal(ok, true, 'Hook.ensure must succeed for an initialized repository')

    const gitDir = join(repo, '.git')
    const prePushPath = join(gitDir, 'hooks', 'pre-push')
    const refTxPath = join(gitDir, 'hooks', 'reference-transaction')
    assert.equal(readFileSync(prePushPath, 'utf8').includes(MARKER), true, 'pre-push hook must be installed and owned')
    assert.equal(readFileSync(refTxPath, 'utf8').includes(MARKER), true, 'reference-transaction hook must be installed and owned')

    const fetchSpecs = execFileSync('git', ['-C', repo, 'config', '--get-all', 'remote.origin.fetch'], { encoding: 'utf8' })
    assert.match(fetchSpecs, /\+refs\/wanxiang\/store:refs\/wanxiang\/remotes\/origin\/store/,
      'ensure must configure the remote tracking fetch refspec')
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('WHAT[durable-events-018] HOOK_shim_resolves_node_from_environment_not_installer_host_execPath', async () => {
  const { execFileSync } = await import('node:child_process')
  const repo = mkdtempSync(join(tmpdir(), 'wxs-hook-shim-'))
  try {
    execFileSync('git', ['init', '--quiet', repo])
    assert.equal(Hook.ensure(repo), true)

    const prePush = readFileSync(join(repo, '.git', 'hooks', 'pre-push'), 'utf8')
    const refTx = readFileSync(join(repo, '.git', 'hooks', 'reference-transaction'), 'utf8')

    for (const shim of [prePush, refTx]) {
      assert.match(shim, /^#!\/bin\/sh/)
      assert.match(shim, /exec \/usr\/bin\/env node/)
      assert.doesNotMatch(shim, new RegExp(process.execPath.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')),
        'shim must resolve Node from environment, not hardcode the installer host process.execPath')
    }
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('WHAT[durable-events-018] HOOK_reference_transaction_and_pre_push_launch_the_same_independent_full_converge_runtime', async () => {
  const { spawnSync } = await import('node:child_process')
  const runner = join(process.cwd(), 'resources/git/wanxiang-hook.mjs')

  // 1. Re-entrance guard: WANXIANG_GIT_SYNC_ACTIVE=1 exits cleanly immediately
  const reentrant = spawnSync(process.execPath, [runner, 'pre-push', 'origin'], {
    env: { ...process.env, WANXIANG_GIT_SYNC_ACTIVE: '1' },
  })
  assert.equal(reentrant.status, 0, 'active sync must short-circuit without error')

  // 2. reference-transaction with non-committed state exits cleanly
  const nonCommitted = spawnSync(process.execPath, [runner, 'reference-transaction', 'prepared'], {
    input: '',
  })
  assert.equal(nonCommitted.status, 0, 'non-committed reference-transaction state must exit 0')

  // 3. reference-transaction with non-store ref input exits cleanly
  const nonStoreRef = spawnSync(process.execPath, [runner, 'reference-transaction', 'committed'], {
    input: '0000000000000000000000000000000000000000 1111111111111111111111111111111111111111 refs/heads/main\n',
  })
  assert.equal(nonStoreRef.status, 0, 'unrelated branch reference transaction must exit 0')

  // 4. Unknown hook kind exits with error status 1
  const unknown = spawnSync(process.execPath, [runner, 'unknown-hook-kind'], {
    encoding: 'utf8',
  })
  assert.equal(unknown.status, 1)
  assert.match(unknown.stderr, /unknown hook kind: unknown-hook-kind/i)

  // 5. Pre-push requires non-empty remote name
  const emptyRemote = spawnSync(process.execPath, [runner, 'pre-push', ''], {
    encoding: 'utf8',
  })
  assert.equal(emptyRemote.status, 1)
  assert.match(emptyRemote.stderr, /requires the Git remote name/i)

})

test('WHAT[durable-events-018] HOOK_classification_preserves_foreign_hooks', () => {
  assert.equal(Hook.classifyExistingHook(null), 'Installed')
  assert.equal(Hook.classifyExistingHook(`# ${MARKER}\n`), 'AlreadyOwned')
  assert.equal(Hook.classifyExistingHook('#!/bin/sh\necho foreign\n'), 'ForeignHook')
})

test('WHAT[durable-events-018] HOOK_install_refreshes_owned_hook_but_never_overwrites_foreign_hook', () => {
  const { dir, cleanup } = sandboxHooks()
  try {
    const owned = `#!/bin/sh\n# ${MARKER}\nexit 0\n`
    const installed = Hook.installOrDiagnose(dir, 'PrePush', owned)
    assert.equal(installed, 'Installed')
    assert.equal(readFileSync(join(dir, 'pre-push'), 'utf8'), owned)

    const refreshed = `${owned}# refreshed\n`
    const refreshVerdict = Hook.installOrDiagnose(dir, 'PrePush', refreshed)
    assert.equal(refreshVerdict, 'AlreadyOwned')
    assert.equal(readFileSync(join(dir, 'pre-push'), 'utf8'), refreshed)

    const foreignDir = mkdtempSync(join(tmpdir(), 'wxs-hooks-foreign-'))
    try {
      const foreignPath = join(foreignDir, 'pre-push')
      const foreign = '#!/bin/sh\necho foreign\n'
      writeFileSync(foreignPath, foreign)
      const verdict = Hook.installOrDiagnose(foreignDir, 'PrePush', owned)
      assert.equal(verdict, 'ForeignHook')
      assert.equal(readFileSync(foreignPath, 'utf8'), foreign)
    } finally {
      rmSync(foreignDir, { recursive: true, force: true })
    }
  } finally {
    cleanup()
  }
})
