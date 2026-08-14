// FROZEN — 2026-08-14. Dumb bare Git remote + independent hook-process convergence.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { join } from 'node:path'
import test from 'node:test'

import { createBareWorkspace, readRemoteStoreOid, remoteHasObject } from '../../../../verification-system/tests/support/dumb-remote.mjs'
import { createLocalEventStore } from '../../../../verification-system/tests/support/local-event-store.mjs'
import { eventId, resultOf, toList } from '../../../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../../../dist/Persistence/EventStore/Model.js')
const runner = fileURLToPath(new URL('../../../../../resources/git/wanxiang-hook.mjs', import.meta.url))
const streamId = (v) => Domain.EventStreamIdModule_create(v)
const envelope = (id, writer) => new Domain.EventEnvelope(
  eventId(id), streamId('dumb/remote'), 'JobRequested', toList([]), { writer }, toList([]),
)
const hook = (repo, kind = 'pre-push', arg = 'origin', input = '') => spawnSync(
  process.execPath,
  [runner, kind, arg],
  { cwd: repo, input, encoding: 'utf8', env: { ...process.env, WANXIANG_GIT_SYNC_ACTIVE: '' } },
)
const assertHookOk = (result) => {
  assert.equal(result.status, 0, `hook failed: ${result.stderr || result.stdout}`)
}

test('dumb_remote_helper_has_no_Wanxiang_domain_or_projection_logic', () => {
  const source = readFileSync(new URL('../../../../verification-system/tests/support/dumb-remote.mjs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /dist\/Domain|CanonicalIntegrator|Projection|WriterStreamSync|HookSync/)
  assert.match(source, /git/)
})

test('pre_push_hook_process_uploads_one_local_writer_file_to_bare_remote_store_ref', async () => {
  const ws = createBareWorkspace(['a'])
  try {
    const repo = ws.client('a')
    const local = createLocalEventStore({ commonDir: join(repo, '.git'), writerId: 'writer-a' })
    assert.equal(resultOf(await local.store.Append(toList([envelope('a'.repeat(40), 'a')]))).ok, true)
    assertHookOk(hook(repo))
    const oid = readRemoteStoreOid(ws.bare)
    assert.match(oid, /^[0-9a-f]{40}$/)
    assert.equal(remoteHasObject(ws.bare, oid), true)
  } finally {
    ws.cleanup()
  }
})

test('second_machine_hook_imports_remote_writer_truth_without_any_running_Wanxiang_process', async () => {
  const ws = createBareWorkspace(['a', 'b'])
  try {
    const a = ws.client('a')
    const b = ws.client('b')
    const localA = createLocalEventStore({ commonDir: join(a, '.git'), writerId: 'writer-a' })
    assert.equal(resultOf(await localA.store.Append(toList([envelope('a'.repeat(40), 'a')]))).ok, true)
    assertHookOk(hook(a))

    // No WorkspaceEventStore/PluginHost instance is created for B. The standalone
    // hook runner itself must fetch, k-way validate, materialize local truth and publish.
    assertHookOk(hook(b))
    assert.equal(existsSync(join(b, '.git', 'wanxiang', 'events', 'writer-a.ndjson')), true)
    const reopenedB = createLocalEventStore({ commonDir: join(b, '.git'), writerId: 'writer-b-after-sync' })
    assert.ok(reopenedB.store.TryEvent(eventId('a'.repeat(40))))
  } finally {
    ws.cleanup()
  }
})

test('two_offline_clients_converge_by_whole_writer_files_and_repeat_is_idempotent', async () => {
  const ws = createBareWorkspace(['a', 'b'])
  try {
    const a = ws.client('a')
    const b = ws.client('b')
    const localA = createLocalEventStore({ commonDir: join(a, '.git'), writerId: 'writer-a' })
    const localB = createLocalEventStore({ commonDir: join(b, '.git'), writerId: 'writer-b' })
    assert.equal(resultOf(await localA.store.Append(toList([envelope('a'.repeat(40), 'a')]))).ok, true)
    assert.equal(resultOf(await localB.store.Append(toList([envelope('b'.repeat(40), 'b')]))).ok, true)

    assertHookOk(hook(a))
    assertHookOk(hook(b))
    assertHookOk(hook(a))
    const firstTip = readRemoteStoreOid(ws.bare)
    assertHookOk(hook(a))
    assert.equal(readRemoteStoreOid(ws.bare), firstTip, 'same complete writer bytes materialize the same Git root')

    for (const repo of [a, b]) {
      assert.equal(existsSync(join(repo, '.git', 'wanxiang', 'events', 'writer-a.ndjson')), true)
      assert.equal(existsSync(join(repo, '.git', 'wanxiang', 'events', 'writer-b.ndjson')), true)
    }
  } finally {
    ws.cleanup()
  }
})

test('reference_transaction_is_also_full_bidirectional_convergence', async () => {
  const source = readFileSync(new URL('../../../../../src/Wanxiangshu/Git/Hook/Sync.fs', import.meta.url), 'utf8')
  assert.match(source, /runReferenceTransaction/)
  assert.match(source, /converge remote observed/)
  assert.doesNotMatch(source, /downloadOnly|importOnly|ConvergeObserved/)
})
