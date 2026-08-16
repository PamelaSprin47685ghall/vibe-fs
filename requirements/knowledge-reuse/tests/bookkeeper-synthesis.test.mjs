// FROZEN — 2026-08-14. Synthesis uses canonical Casebook Current; no history scan.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive, CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { refreshStale } from '../../../dist/Repository/Knowledge/Casebook/Bookkeeper.js'
import { collector, cleanupInspector, noteAnswer, notePrompt, setEnabled, tryFinalizeInspector } from '../../../dist/Repository/Knowledge/Casebook/Lifecycle.js'
import { ObservationCollector__Collect_Z15AE2BE0 as collect } from '../../../dist/Enforcer/ObservationCollector.js'
import { contentHash as hash } from '../../../dist/Repository/Knowledge/Casebook/Capture.js'
import { Observation } from '../../../dist/Repository/Knowledge/Casebook/Model.js'
import { acquire } from '../../../dist/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Persistence/Journal/RuntimePath.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { listItems, resultOf, sessionId, toList } from '../../verification-system/tests/support/domain.mjs'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'
import { BookkeeperRuntime_setSessionPort as setSessionPort, BookkeeperRuntime_resetSessionPort as resetSessionPort } from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRuntime.js'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])
const FAIL_A = 'A-must-fail-synthesis'
const failingPort = () => {
  let seq = 0
  return { port: {
    CreateChildSession: async () => ({ tag: 0, fields: [sessionId(`bk-fail-${++seq}`)] }),
    AbortSession: async () => ({ tag: 0, fields: [] }),
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    SendPrompt: async () => ({ tag: 4, fields: ['injected synth failure'] }),
  } }
}
const record = (session, q, a, obs) => ({ SessionId: session, Q: q, A: a, Observations: toList(obs), LastAccessOrder: 0 })

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_injected_synthesizer_error_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-err-'))
  const local = createLocalEventStore()
  const { port } = failingPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal(resultOf(await archive(local.store, record('s-err-1', 'Q keep', FAIL_A, [fileRead('a.txt', hash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    setSessionPort(port)
    const refreshed = resultOf(await refreshStale(local.store, dir, 's-err-1'))
    assert.equal(refreshed.ok, false)
    assert.match(String(refreshed.error), /injected synth failure/)
    const fetched = resultOf(await fetchCase(local.store, 10, 's-err-1'))
    assert.equal(fetched.value.A, FAIL_A)
    assert.equal(fetched.value.Q, 'Q keep')
    assert.equal(listItems(fetched.value.Observations)[0].fields[1], hash('hello'))
  } finally {
    resetSessionPort()
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_synthesizer_runs_once_per_stale_refresh', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-once-'))
  const local = createLocalEventStore()
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal(resultOf(await archive(local.store, record('s-once', 'Q-count-synth-once', 'A once', [fileRead('a.txt', hash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    setSessionPort(port)
    const refreshed = resultOf(await refreshStale(local.store, dir, 's-once'))
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
  } finally {
    resetSessionPort()
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_uses_synthesizer_not_raw_noteAnswer', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-fin-'))
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)
    setSessionPort(port)
    const key = 'insp-synth-fin'
    const rawA = 'PromptAuthority is owned by the Host.'
    notePrompt(key, 'What owns PromptAuthority?')
    collect(collector, key, 'read', { path: 'a.txt' }, 'hello')
    noteAnswer(key, rawA)
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const store = acquire(gitCommonDir(dir))
    const fetched = resultOf(await fetchCase(store, 10, key))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.A, rawA)
    assert.equal(fetched.value.A, CANONICAL_A)
    const publishedA = fetched.value.A
    notePrompt(key, 'Q2')
    noteAnswer(key, 'A2')
    const second = resultOf(await tryFinalizeInspector(dir, key))
    assert.equal(second.ok, false)
    assert.match(String(second.error), /already finalized/)
    assert.equal(resultOf(await fetchCase(store, 10, key)).value.A, publishedA)
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_cleanup_never_synthesizes', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-cleanup-'))
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)
    setSessionPort(port)
    const key = 'insp-cleanup-synth'
    notePrompt(key, 'Q-cleanup-never-synth')
    collect(collector, key, 'read', { path: 'b.txt' }, 'body')
    noteAnswer(key, 'A cleanup')
    cleanupInspector(key)
    assert.equal(createCalls.length, 0)
    assert.equal(programCalls.length, 0)
    const store = acquire(gitCommonDir(dir))
    const fetched = resultOf(await fetchCase(store, 10, key))
    assert.equal(fetched.value == null, true)
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
