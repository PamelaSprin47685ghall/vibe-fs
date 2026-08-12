import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { refreshStale } from '../../../dist/Infrastructure/CasebookBookkeeper.js'
import {
  collector,
  cleanupInspector,
  noteAnswer,
  notePrompt,
  setEnabled,
  tryFinalizeInspector,
} from '../../../dist/Infrastructure/CasebookLifecycle.js'
import {
  ObservationCollector__Collect_Z15AE2BE0 as collect,
} from '../../../dist/Infrastructure/ObservationCollector.js'
import { contentHash as hash } from '../../../dist/Infrastructure/CasebookCapture.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { loadEvents } from '../../../dist/Infrastructure/CasebookStore.js'
import { acquire } from '../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Journal/RuntimePath.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { caseOf, listItems, resultOf, sessionId, toList } from '../support/domain.mjs'
import {
  CANONICAL_A,
  CANONICAL_Q,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'
const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])

const FAIL_A = 'A-must-fail-synthesis'
const CLEANUP_Q = 'Q-cleanup-never-synth'

const failingPort = () => {
  const createCalls = []
  let seq = 0
  return {
    createCalls,
    port: {
      CreateChildSession: async (parentId) => {
        seq += 1
        const child = sessionId(`bk-fail-${seq}`)
        createCalls.push(parentId)
        return { tag: 0, fields: [child] }
      },
      AbortSession: async () => ({ tag: 0, fields: [] }),
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => ({ tag: 4, fields: ['injected synth failure'] }),
    },
  }
}

test('CASE006_injected_synthesizer_error_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-err-'))
  const { port } = failingPort()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = {
      SessionId: 's-err-1',
      Q: 'Q keep',
      A: FAIL_A,
      Observations: toList([fileRead('a.txt', hash('hello'))]),
      LastAccessOrder: 0,
    }
    assert.equal(resultOf(archive(store, raw, caseRec)).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')

    setSessionPort(port)
    const refreshed = resultOf(await refreshStale(store, raw, dir, 's-err-1'))
    assert.equal(refreshed.ok, false, 'transaction Error must not publish')
    assert.equal(String(refreshed.error).includes('injected synth failure'), true)

    const fetched = resultOf(fetchCase(store, raw, 10, 's-err-1'))
    assert.equal(fetched.value.A, FAIL_A, 'old Case remains')
    assert.equal(fetched.value.Q, 'Q keep')
    const obs = listItems(fetched.value.Observations)
    assert.equal(obs[0].fields[1], hash('hello'), 'observations not advanced')

    const kinds = listItems(resultOf(loadEvents(raw, store.OpenSnapshot())).value).map((e) => caseOf(e))
    assert.equal(kinds.includes('CaseCaptured'), true)
    assert.equal(kinds.includes('CaseRefreshed'), false)
  } finally {
    resetSessionPort()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE006_synthesizer_runs_once_per_stale_refresh', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-once-'))
  const { port, createCalls, editQaCalls } = scriptedBookkeeperPort()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = {
      SessionId: 's-once',
      Q: 'Q-count-synth-once',
      A: 'A once',
      Observations: toList([fileRead('a.txt', hash('hello'))]),
      LastAccessOrder: 0,
    }
    assert.equal(resultOf(archive(store, raw, caseRec)).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')

    setSessionPort(port)
    const refreshed = resultOf(await refreshStale(store, raw, dir, 's-once'))
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1, 'exactly one child session per refresh')
    assert.equal(editQaCalls.length >= 2, true, 'js-bookkeeper invoked')
  } finally {
    resetSessionPort()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE010_finalize_uses_synthesizer_not_raw_noteAnswer', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-fin-'))
  const { port, createCalls, editQaCalls } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)
    setSessionPort(port)

    const sessionIdKey = 'insp-synth-fin'
    const rawA = 'PromptAuthority is owned by the Host.'
    notePrompt(sessionIdKey, 'What owns PromptAuthority?')
    collect(collector, sessionIdKey, 'read', { path: 'a.txt' }, 'hello')
    noteAnswer(sessionIdKey, rawA)

    const first = resultOf(await tryFinalizeInspector(dir, sessionIdKey))
    assert.equal(first.ok, true, `first finalize ok, got ${JSON.stringify(first.error)}`)
    assert.equal(createCalls.length, 1)
    assert.equal(editQaCalls.length >= 2, true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(fetchCase(store, raw, 10, sessionIdKey))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.Q, 'What owns PromptAuthority?')
    assert.notEqual(fetched.value.A, rawA, 'fetched A is synthesized, not raw noteAnswer')
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(fetched.value.A.includes('evidence:'), false)
    assert.equal(listItems(fetched.value.Observations).length, 1)

    const publishedA = fetched.value.A
    notePrompt(sessionIdKey, 'Q2')
    noteAnswer(sessionIdKey, 'A2')
    const second = resultOf(await tryFinalizeInspector(dir, sessionIdKey))
    assert.equal(second.ok, false, 'second finalize must be refused')
    assert.equal(String(second.error).includes('already finalized'), true)

    const still = resultOf(fetchCase(store, raw, 10, sessionIdKey))
    assert.equal(still.value.A, publishedA, 'original synthesized case retained')
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE010_cleanup_never_synthesizes', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-cleanup-'))
  const { port, createCalls, editQaCalls } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)
    setSessionPort(port)

    const sessionIdKey = 'insp-cleanup-synth'
    notePrompt(sessionIdKey, CLEANUP_Q)
    collect(collector, sessionIdKey, 'read', { path: 'b.txt' }, 'body')
    noteAnswer(sessionIdKey, 'A cleanup')
    cleanupInspector(sessionIdKey)

    assert.equal(createCalls.length, 0, 'unexpected cleanup must not CreateChildSession')
    assert.equal(editQaCalls.length, 0, 'unexpected cleanup must not run js-bookkeeper')

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(fetchCase(store, raw, 10, sessionIdKey))
    assert.equal(fetched.value === undefined || fetched.value === null, true)
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
