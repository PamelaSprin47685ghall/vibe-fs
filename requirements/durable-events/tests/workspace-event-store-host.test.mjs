import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as workspaceHost from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const POISON = 'LEAVE_UNREAD_POISON_SENTINEL_NEVER_PARSE\n{not-a-journal-envelope\n'
const fingerprint = (path) => {
  const st = statSync(path)
  return { size: st.size, mtimeMs: st.mtimeMs, ino: st.ino, sha256: createHash('sha256').update(readFileSync(path)).digest('hex') }
}
const mustOk = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result
}

const withRepo = async (name, fn) => {
  const workspace = mkdtempSync(join(tmpdir(), `wxs-host-es-${name}-`))
  try {
    execFileSync('git', ['init', '--quiet', workspace])
    await fn(workspace, join(workspace, '.git'))
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
}

test('WHAT[DURABLE-EVENTS-010] SharedAgentJournal_boots_local_EventStore_and_leaves_retired_RuntimePath_ndjson_unread', async () => {
  await withRepo('boot', async (workspace, commonDir) => {
    const retiredDir = join(commonDir, 'wanxiangshu-next', 'runtimes')
    mkdirSync(retiredDir, { recursive: true })
    const stale = join(retiredDir, 'abandoned-runtime-host.ndjson')
    writeFileSync(stale, POISON)
    const before = fingerprint(stale)

    const acquired = mustOk(await workspaceHost.WorkspaceEventStoreSurface_acquire(retiredDir, commonDir, process.pid, '2026-04-01T00:00:00Z'))
    assert.deepEqual(fingerprint(stale), before)
    assert.equal(readFileSync(stale, 'utf8'), POISON)

    const closed = mustOk(await workspaceHost.WorkspaceEventStoreSurface_appendClosed(acquired.journal, 'ses_host_es'))
    assert.equal(closed.session, true)
    assert.deepEqual(fingerprint(stale), before)

    const files = readdirSync(join(commonDir, 'wanxiang', 'events'))
    assert.equal(files.length >= 1, true)
    assert.equal(files.every((name) => name.endsWith('.ndjson')), true)
    assert.equal(workspaceHost.WorkspaceEventStoreSurface_hasCurrent(commonDir), true)

    workspaceHost.WorkspaceEventStoreSurface_release(acquired.journal)
  })
})

test('WHAT[DURABLE-EVENTS-009] SharedAgentJournal_cache_hit_returns_same_instance_without_rereading_retired_path', async () => {
  await withRepo('cache', async (workspace, commonDir) => {
    const retiredDir = join(commonDir, 'wanxiangshu-next', 'runtimes')
    mkdirSync(retiredDir, { recursive: true })
    const stale = join(retiredDir, 'old.ndjson')
    writeFileSync(stale, POISON)
    const before = fingerprint(stale)

    const first = mustOk(await workspaceHost.WorkspaceEventStoreSurface_acquire(retiredDir, commonDir, process.pid, '2026-04-01T00:00:00Z'))
    const second = mustOk(await workspaceHost.WorkspaceEventStoreSurface_acquire(retiredDir, commonDir, process.pid, '2026-04-01T00:00:01Z'))
    assert.equal(workspaceHost.WorkspaceEventStoreSurface_same(first.journal, second.journal), true)
    assert.deepEqual(fingerprint(stale), before)

    workspaceHost.WorkspaceEventStoreSurface_release(first.journal)
    workspaceHost.WorkspaceEventStoreSurface_release(second.journal)
  })
})
