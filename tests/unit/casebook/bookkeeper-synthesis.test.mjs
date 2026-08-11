// tests/unit/casebook/bookkeeper-synthesis.test.mjs — G6-E/F:
// Bookkeeper QaSynthesize transaction (once per refresh/finalize; Error keeps old Case).

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import {
  defaultSynthesize,
  refreshStale,
  resetSynthesizer,
  setSynthesizer,
} from '../../../dist/Infrastructure/CasebookBookkeeper.js'
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
import { caseOf, errorResult, listItems, resultOf, toList } from '../support/domain.mjs'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])

const FAIL_A = 'A-must-fail-synthesis'
const COUNT_Q = 'Q-count-synth-once'
const CLEANUP_Q = 'Q-cleanup-never-synth'

test('CASE006_injected_synthesizer_error_keeps_old_case', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-err-'))
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

    // Sentinel A so a parallel file's default path is not poisoned.
    setSynthesizer((q, a, obs) => {
      if (a === FAIL_A) return errorResult('injected synth failure')
      return defaultSynthesize(q, a, obs)
    })

    const refreshed = resultOf(refreshStale(store, raw, dir, 's-err-1'))
    assert.equal(refreshed.ok, false, 'synthesizer Error must not publish')
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
    resetSynthesizer()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE006_synthesizer_runs_once_per_stale_refresh', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-once-'))
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = {
      SessionId: 's-once',
      Q: COUNT_Q,
      A: 'A once',
      Observations: toList([fileRead('a.txt', hash('hello'))]),
      LastAccessOrder: 0,
    }
    assert.equal(resultOf(archive(store, raw, caseRec)).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')

    let calls = 0
    setSynthesizer((q, a, obs) => {
      if (q === COUNT_Q) calls += 1
      return defaultSynthesize(q, a, obs)
    })

    const refreshed = resultOf(refreshStale(store, raw, dir, 's-once'))
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(calls, 1, 'exactly one provider transaction')
  } finally {
    resetSynthesizer()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE010_finalize_uses_synthesizer_not_raw_noteAnswer', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-fin-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)

    const sessionId = 'insp-synth-fin'
    const rawA = 'PromptAuthority is owned by the Host.'
    notePrompt(sessionId, 'What owns PromptAuthority?')
    collect(collector, sessionId, 'read', { path: 'a.txt' }, 'hello')
    noteAnswer(sessionId, rawA)

    const first = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(first.ok, true, `first finalize ok, got ${JSON.stringify(first.error)}`)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(fetchCase(store, raw, 10, sessionId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value.Q, 'What owns PromptAuthority?')
    assert.notEqual(fetched.value.A, rawA, 'fetched A is synthesized, not raw noteAnswer')
    assert.equal(fetched.value.A.startsWith(rawA), true)
    assert.equal(listItems(fetched.value.Observations).length, 1)

    const publishedA = fetched.value.A
    notePrompt(sessionId, 'Q2')
    noteAnswer(sessionId, 'A2')
    const second = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(second.ok, false, 'second finalize must be refused')
    assert.equal(String(second.error).includes('already finalized'), true)

    const still = resultOf(fetchCase(store, raw, 10, sessionId))
    assert.equal(still.value.A, publishedA, 'original synthesized case retained')
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE010_cleanup_never_synthesizes', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-cleanup-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)

    let calls = 0
    setSynthesizer((q, a, obs) => {
      if (q === CLEANUP_Q) calls += 1
      return defaultSynthesize(q, a, obs)
    })

    const sessionId = 'insp-cleanup-synth'
    notePrompt(sessionId, CLEANUP_Q)
    collect(collector, sessionId, 'read', { path: 'b.txt' }, 'body')
    noteAnswer(sessionId, 'A cleanup')
    cleanupInspector(sessionId)

    assert.equal(calls, 0, 'unexpected cleanup must not run QaSynthesize')

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(fetchCase(store, raw, 10, sessionId))
    assert.equal(fetched.value === undefined || fetched.value === null, true)
  } finally {
    resetSynthesizer()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
