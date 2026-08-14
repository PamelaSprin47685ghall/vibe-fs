// FROZEN — 2026-08-14. WorkspaceEventStore owns one process writer + one Integrator per git common-dir.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync, readdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { agentFact, caseOf, fold, payloadOf, sessionId, stream } from '../../verification-system/tests/support/domain.mjs'

const RuntimePath = await import('../../../dist/Persistence/Journal/RuntimePath.js')
const Shared = await import('../../../dist/Persistence/Journal/SharedAgentJournal.js')
const Workspace = await import('../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js')
const AgentJournal = await import('../../../dist/Persistence/Journal/AgentJournal.js')

const POISON = 'LEAVE_UNREAD_POISON_SENTINEL_NEVER_PARSE\n{not-a-journal-envelope\n'
const SESSION = sessionId('ses_host_es')
const CLOSED_AGENT = agentFact('CompanionBloggerClosed', { SessionId: SESSION })
const mustOk = (r) => { assert.equal(caseOf(r), 'Ok'); return payloadOf(r) }
const fingerprint = (path) => {
  const st = statSync(path)
  return { size: st.size, mtimeMs: st.mtimeMs, ino: st.ino, sha256: createHash('sha256').update(readFileSync(path)).digest('hex') }
}
const openJournalFactory = (commonDir) => {
  const port = Workspace.bootPort(commonDir)
  return async (runtimeId, processId, startedAt) => {
    const resumed = await port.ResumeOrCreate(runtimeId, processId, startedAt)
    if (caseOf(resumed) !== 'Ok') return resumed
    const triple = payloadOf(resumed)
    return AgentJournal.AgentJournalModule_createFromProjection(triple[0], triple[2])
  }
}

test('SharedAgentJournal_boots_local_EventStore_and_leaves_retired_RuntimePath_ndjson_unread', async () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-host-es-'))
  try {
    execFileSync('git', ['init', '--quiet', workspace])
    const commonDir = RuntimePath.gitCommonDir(workspace)
    const retiredDir = RuntimePath.forWorkspace(workspace)
    mkdirSync(retiredDir, { recursive: true })
    const stale = join(retiredDir, 'abandoned-runtime-host.ndjson')
    writeFileSync(stale, POISON)
    const before = fingerprint(stale)

    const journal = mustOk(await Shared.acquire(retiredDir, process.pid, new Date(), openJournalFactory(commonDir)))
    assert.deepEqual(fingerprint(stale), before)
    assert.equal(readFileSync(stale, 'utf8'), POISON)

    const closed = mustOk(await AgentJournal.AgentJournalModule_appendAgent(stream.session(SESSION), undefined, CLOSED_AGENT, journal))
    assert.ok(fold.session(closed, 'ses_host_es'))
    assert.deepEqual(fingerprint(stale), before)

    const files = readdirSync(join(commonDir, 'wanxiang', 'events'))
    assert.equal(files.length >= 1, true)
    assert.equal(files.every((name) => name.endsWith('.ndjson')), true)
    const store = Workspace.acquire(commonDir)
    assert.ok(store.TryCurrent('Journal'))

    Shared.release(journal)
    Workspace.release(commonDir)
    Workspace.release(commonDir)
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})

test('SharedAgentJournal_cache_hit_returns_same_instance_without_rereading_retired_path', async () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-host-es-cache-'))
  try {
    execFileSync('git', ['init', '--quiet', workspace])
    const commonDir = RuntimePath.gitCommonDir(workspace)
    const retiredDir = RuntimePath.forWorkspace(workspace)
    mkdirSync(retiredDir, { recursive: true })
    const stale = join(retiredDir, 'old.ndjson')
    writeFileSync(stale, POISON)
    const before = fingerprint(stale)
    const open = openJournalFactory(commonDir)
    const first = mustOk(await Shared.acquire(retiredDir, process.pid, new Date(), open))
    const second = mustOk(await Shared.acquire(retiredDir, process.pid, new Date(), open))
    assert.equal(first, second)
    assert.deepEqual(fingerprint(stale), before)
    Shared.release(first)
    Shared.release(second)
    Workspace.release(commonDir)
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})
