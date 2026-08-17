// FROZEN — 2026-08-14. Dumb bare Git remote + independent hook-process convergence.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { join } from 'node:path'
import test from 'node:test'

import { createBareWorkspace, readRemoteStoreOid, remoteHasObject } from '../../../../verification-system/tests/support/dumb-remote.mjs'
import * as eventStore from '../../../../../dist/Persistence/EventStore/Surface.js'

const runner = fileURLToPath(new URL('../../../../../resources/git/wanxiang-hook.mjs', import.meta.url))
const event = (id, writer) => ({
  id,
  stream: 'dumb/remote',
  type: 'JobRequested',
  parents: [],
  payload: { writer },
  payloadRefs: [],
})
const open = (repo, writerId) => eventStore.create(join(repo, '.git'), writerId)
const append = async (handle, value) => {
  const result = await eventStore.append(handle, [value])
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
}
const hook = (repo, kind = 'pre-push', arg = 'origin', input = '') => spawnSync(
  process.execPath,
  [runner, kind, arg],
  { cwd: repo, input, encoding: 'utf8', env: { ...process.env, WANXIANG_GIT_SYNC_ACTIVE: '' } },
)
const assertHookOk = (result) => {
  assert.equal(result.status, 0, `hook failed: ${result.stderr || result.stdout}`)
}

test('WHAT[DURABLE-CONVERGENCE-009] dumb_remote_helper_has_no_Wanxiang_domain_or_projection_logic', () => {
  const source = readFileSync(new URL('../../../../verification-system/tests/support/dumb-remote.mjs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /dist\/Domain|CanonicalIntegrator|Projection|WriterStreamSync|HookSync/)
  assert.match(source, /git/)
})

test('WHAT[DURABLE-CONVERGENCE-009] pre_push_hook_process_uploads_one_local_writer_file_to_bare_remote_store_ref', async () => {
  const ws = createBareWorkspace(['a'])
  try {
    const repo = ws.client('a')
    const local = open(repo, 'writer-a')
    try {
      await append(local, event('a'.repeat(40), 'a'))
    } finally {
      eventStore.dispose(local)
    }
    assertHookOk(hook(repo))
    const oid = readRemoteStoreOid(ws.bare)
    assert.match(oid, /^[0-9a-f]{40}$/)
    assert.equal(remoteHasObject(ws.bare, oid), true)
  } finally {
    ws.cleanup()
  }
})

test('WHAT[DURABLE-CONVERGENCE-009] second_machine_hook_imports_remote_writer_truth_without_any_running_Wanxiang_process', async () => {
  const ws = createBareWorkspace(['a', 'b'])
  try {
    const a = ws.client('a')
    const b = ws.client('b')
    const localA = open(a, 'writer-a')
    try {
      await append(localA, event('a'.repeat(40), 'a'))
    } finally {
      eventStore.dispose(localA)
    }
    assertHookOk(hook(a))

    // No WorkspaceEventStore/PluginHost instance is created for B. The standalone
    // hook runner itself must fetch, k-way validate, materialize local truth and publish.
    assertHookOk(hook(b))
    assert.equal(existsSync(join(b, '.git', 'wanxiang', 'events', 'writer-a.ndjson')), true)
    const reopenedB = open(b, 'writer-b-after-sync')
    try {
      assert.deepEqual(eventStore.read(reopenedB, 'a'.repeat(40)), event('a'.repeat(40), 'a'))
    } finally {
      eventStore.dispose(reopenedB)
    }
  } finally {
    ws.cleanup()
  }
})

test('WHAT[DURABLE-CONVERGENCE-009] two_offline_clients_converge_by_whole_writer_files_and_repeat_is_idempotent', async () => {
  const ws = createBareWorkspace(['a', 'b'])
  try {
    const a = ws.client('a')
    const b = ws.client('b')
    const localA = open(a, 'writer-a')
    const localB = open(b, 'writer-b')
    try {
      await append(localA, event('a'.repeat(40), 'a'))
      await append(localB, event('b'.repeat(40), 'b'))
    } finally {
      eventStore.dispose(localA)
      eventStore.dispose(localB)
    }

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

test('WHAT[DURABLE-CONVERGENCE-008] reference_transaction_is_also_full_bidirectional_convergence', async () => {
  const source = readFileSync(new URL('../../../../../src/Wanxiangshu/Git/Hook/Sync.fs', import.meta.url), 'utf8')
  assert.match(source, /runReferenceTransaction/)
  assert.match(source, /converge remote observed/)
  assert.doesNotMatch(source, /downloadOnly|importOnly|ConvergeObserved/)
})
