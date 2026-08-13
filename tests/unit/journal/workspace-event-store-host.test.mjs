// tests/unit/journal/workspace-event-store-host.test.mjs
// W1-host: PluginHost/SharedAgentJournal path boots from EventStore and leaves planted NDJSON unread.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentFact,
  caseOf,
  fact,
  fold,
  idValue,
  payloadOf,
  sessionId,
  stream,
} from '../support/domain.mjs'

const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const RuntimePath = await import('../../../dist/Journal/RuntimePath.js')
const Shared = await import('../../../dist/Journal/SharedAgentJournal.js')
const Workspace = await import('../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js')
const AgentJournalMod = await import('../../../dist/Journal/AgentJournal.js')
const EsWriter = await import('../../../dist/Journal/EventStoreJournalWriter.js')

const POISON = 'LEAVE_UNREAD_POISON_SENTINEL_NEVER_PARSE\n{not-a-journal-envelope\n'
const STALE_RUNTIME_ID = 'abandoned-runtime-host'
const SESSION = sessionId('ses_host_es')
const CLOSED_AGENT = agentFact('CompanionBloggerClosed', { SessionId: SESSION })

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}: ${payloadOf(result)}`)
  return payloadOf(result)
}

const fingerprint = (path) => {
  const st = statSync(path)
  return {
    size: st.size,
    mtimeMs: st.mtimeMs,
    ino: st.ino,
    sha256: createHash('sha256').update(readFileSync(path)).digest('hex'),
  }
}

const plantStaleNdjson = (runtimeDir) => {
  mkdirSync(runtimeDir, { recursive: true, mode: 0o700 })
  const ndjsonPath = join(runtimeDir, `${STALE_RUNTIME_ID}.ndjson`)
  writeFileSync(ndjsonPath, POISON, { mode: 0o600 })
  assert.equal(existsSync(ndjsonPath), true)
  return { ndjsonPath, before: fingerprint(ndjsonPath) }
}

const assertUntouched = (path, before) => {
  const after = fingerprint(path)
  assert.deepEqual(after, before, 'planted NDJSON must stay byte+inode/mtime identical (leave-unread)')
  assert.equal(readFileSync(path, 'utf8'), POISON, 'planted NDJSON content must remain unparsed poison')
}

const openJournalFactory = (commonDir) => {
  const port = Workspace.bootPort(commonDir)
  return async (runtimeId, processId, startedAt) => {
    const resumed = await port.ResumeOrCreate(runtimeId, processId, startedAt)
    if (caseOf(resumed) !== 'Ok') return resumed
    const triple = payloadOf(resumed)
    return AgentJournalMod.AgentJournalModule_createFromProjection(triple[0], triple[2])
  }
}

test('host_SharedAgentJournal_boots_EventStore_and_leaves_planted_ndjson_unread', async () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-host-es-'))
  try {
    execFileSync('git', ['init', '--quiet', workspace])
    const commonDir = RuntimePath.gitCommonDir(workspace)
    const runtimeDir = RuntimePath.forWorkspace(workspace)
    const planted = plantStaleNdjson(runtimeDir)

    const journal = mustOk(
      await Shared.acquire(runtimeDir, process.pid, new Date(), openJournalFactory(commonDir)),
      'SharedAgentJournal.acquire',
    )

    assertUntouched(planted.ndjsonPath, planted.before)
    assert.equal(existsSync(join(runtimeDir, 'blobs')), false)

    const writer = AgentJournalMod.AgentJournal__get_Writer(journal)
    assert.equal(EsWriter.EventStoreJournalWriter__get_FilePath(writer), '')
    assert.equal(Number(EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq(writer)), 1)

    const closed = mustOk(
      await AgentJournalMod.AgentJournalModule_appendAgent(stream.session(SESSION), undefined, CLOSED_AGENT, journal),
      'appendAgent',
    )
    assert.ok(fold.session(closed, 'ses_host_es'), 'EventStore-backed journal must accept append')

    assertUntouched(planted.ndjsonPath, planted.before)
    assert.equal(Number(EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq(writer)), 2)

    // Canonical store tip must exist under the git common-dir after boot+append.
    const rawPair = Workspace.acquire(commonDir)
    const store = rawPair[1]
    const tip = await store.OpenSnapshot()
    assert.equal(typeof Persist.GitObjectIdModule_value(Persist.RootOidModule_value(tip.RootOid)), 'string')

    Shared.release(journal)
    Workspace.release(commonDir)
    Workspace.release(commonDir)
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})

test('host_SharedAgentJournal_cache_hit_does_not_reread_ndjson', async () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-host-es-cache-'))
  try {
    execFileSync('git', ['init', '--quiet', workspace])
    const commonDir = RuntimePath.gitCommonDir(workspace)
    const runtimeDir = RuntimePath.forWorkspace(workspace)
    const planted = plantStaleNdjson(runtimeDir)
    const openJournal = openJournalFactory(commonDir)

    const first = mustOk(await Shared.acquire(runtimeDir, process.pid, new Date(), openJournal), 'acquire first')
    const second = mustOk(await Shared.acquire(runtimeDir, process.pid, new Date(), openJournal), 'acquire second')
    assert.equal(first, second, 'cache hit must return the same AgentJournal instance')

    assertUntouched(planted.ndjsonPath, planted.before)

    Shared.release(first)
    Shared.release(second)
    Workspace.release(commonDir)
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})
