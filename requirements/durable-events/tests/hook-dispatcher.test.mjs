// FROZEN — 2026-08-14. Rewritten for the shock-cut hook architecture.
// Intentionally NOT executed before implementation.
// DURABLE-EVENTS-018 / DURABLE-CONVERGENCE-008.

import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { caseOf } from '../../verification-system/tests/support/domain.mjs'

const Hook = await import('../../../dist/Infrastructure/Git/HookDispatcher.js')
const MARKER = 'wanxiang-hook-dispatcher'
const read = (relative) => readFileSync(new URL(`../../../${relative}`, import.meta.url), 'utf8')

const sandboxHooks = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hooks-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('HOOK_startup_ensure_installs_both_hooks_and_remote_fetch_refspec_without_running_sync', () => {
  const source = read('src/Wanxiangshu/Infrastructure/Git/HookDispatcher.fs')
  assert.match(source, /HookKind\.ReferenceTransaction/)
  assert.match(source, /HookKind\.PrePush/)
  assert.match(source, /remote\.%s\.fetch/)
  assert.match(source, /StoreRef\.remoteTracking/)
  assert.match(source, /let ensure .*Result<unit, string>/s)
  assert.doesNotMatch(source, /WriterStreamSync|GitGateway\.converge|member _\.(Fetch|Pull|Push)/,
    'startup ensure must install the membrane, not perform synchronization')
})

test('HOOK_reference_transaction_and_pre_push_launch_the_same_independent_full_converge_runtime', () => {
  const dispatcher = read('src/Wanxiangshu/Infrastructure/Git/HookDispatcher.fs')
  const sync = read('src/Wanxiangshu/Infrastructure/Git/HookSync.fs')
  const runner = read('resources/git/wanxiang-hook.mjs')

  assert.match(dispatcher, /resources\/git/)
  assert.match(dispatcher, /WANXIANG_GIT_SYNC_ACTIVE/)
  assert.match(sync, /runPrePush/)
  assert.match(sync, /runReferenceTransaction/)
  assert.match(sync, /converge remote None/)
  assert.match(sync, /converge remote observed/)
  const syncCode = sync.split('\n').filter((line) => !line.trimStart().startsWith('///')).join('\n')
  assert.doesNotMatch(syncCode, /WorkspaceEventStore|CanonicalIntegrator|PluginHost|IEventStore/)
  assert.match(runner, /reference-transaction/)
  assert.match(runner, /pre-push/)
  assert.doesNotMatch(runner, /WorkspaceEventStore|CanonicalIntegrator|PluginHost/)
})

test('HOOK_classification_preserves_foreign_hooks', () => {
  assert.equal(caseOf(Hook.classifyExistingHook(undefined)), 'Installed')
  assert.equal(caseOf(Hook.classifyExistingHook(`# ${MARKER}\n`)), 'AlreadyOwned')
  assert.equal(caseOf(Hook.classifyExistingHook('#!/bin/sh\necho foreign\n')), 'ForeignHook')
})

test('HOOK_install_refreshes_owned_hook_but_never_overwrites_foreign_hook', () => {
  const { dir, cleanup } = sandboxHooks()
  try {
    const owned = `#!/bin/sh\n# ${MARKER}\nexit 0\n`
    const installed = Hook.installOrDiagnose(dir, Hook.HookKind.PrePush, owned)
    assert.equal(caseOf(installed), 'Installed')
    assert.equal(readFileSync(join(dir, 'pre-push'), 'utf8'), owned)

    const refreshed = `${owned}# refreshed\n`
    const refreshVerdict = Hook.installOrDiagnose(dir, Hook.HookKind.PrePush, refreshed)
    assert.equal(caseOf(refreshVerdict), 'AlreadyOwned')
    assert.equal(readFileSync(join(dir, 'pre-push'), 'utf8'), refreshed)

    const foreignDir = mkdtempSync(join(tmpdir(), 'wxs-hooks-foreign-'))
    try {
      const foreignPath = join(foreignDir, 'pre-push')
      const foreign = '#!/bin/sh\necho foreign\n'
      writeFileSync(foreignPath, foreign)
      const verdict = Hook.installOrDiagnose(foreignDir, Hook.HookKind.PrePush, owned)
      assert.equal(caseOf(verdict), 'ForeignHook')
      assert.equal(readFileSync(foreignPath, 'utf8'), foreign)
    } finally {
      rmSync(foreignDir, { recursive: true, force: true })
    }
  } finally {
    cleanup()
  }
})
