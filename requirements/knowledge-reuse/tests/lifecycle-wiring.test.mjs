// FROZEN — 2026-08-14. Lifecycle wiring observes canonical Casebook Current only.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { collector, setEnabled, notePrompt, noteAnswer, tryFinalizeInspector, cleanupInspector, touchAccess } from '../../../dist/Infrastructure/CasebookLifecycle.js'
import { CasebookWorkflow_fetchCase as fetchCase, CasebookWorkflow_touchCaseAccess as touchCaseAccess } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { ObservationCollector__Collect_Z15AE2BE0 as collect, ObservationCollector__Count_Z721C83C5 as count } from '../../../dist/Infrastructure/ObservationCollector.js'
import { acquire } from '../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Journal/RuntimePath.js'
import { listItems, resultOf } from '../../verification-system/tests/support/domain.mjs'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'
import { BookkeeperRuntime_setSessionPort as setSessionPort, BookkeeperRuntime_resetSessionPort as resetSessionPort } from '../../../dist/Infrastructure/BookkeeperRuntime.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const currentCases = (store) => store.TryCurrent('Casebook')?.Cases ?? new Map()

test('lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const { port } = scriptedBookkeeperPort()
    setSessionPort(port)
    const key = 'insp-finalize-1'
    notePrompt(key, 'What owns PromptAuthority?')
    collect(collector, key, 'read', { path: 'a.txt' }, 'hello')
    const rawA = 'PromptAuthority is owned by the Host.'
    noteAnswer(key, rawA)
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)

    const store = acquire(gitCommonDir(dir))
    const fetched = resultOf(await fetchCase(store, 10, key))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.A, rawA)
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(listItems(fetched.value.Observations).length, 1)
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
    cleanup()
  }
})

test('lifecycle_cleanupInspector_never_publishes_case', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const key = 'insp-cleanup-1'
    notePrompt(key, 'Q cleanup')
    collect(collector, key, 'read', { path: 'b.txt' }, 'body')
    noteAnswer(key, 'A cleanup')
    assert.equal(count(collector, key) > 0, true)
    cleanupInspector(key)
    assert.equal(count(collector, key), 0)
    const store = acquire(gitCommonDir(dir))
    assert.equal(currentCases(store).size, 0)
    assert.equal(resultOf(await fetchCase(store, 10, key)).value == null, true)
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)
    assert.equal(currentCases(store).size, 0)
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_missing_answer_is_noop_finalize', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const key = 'insp-no-a'
    notePrompt(key, 'Q only')
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)
    assert.equal(currentCases(acquire(gitCommonDir(dir))).size, 0)
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_touchAccess_and_touchCaseAccess_advance_integrated_access_order', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const { port } = scriptedBookkeeperPort()
    setSessionPort(port)
    const key = 'insp-access-1'
    notePrompt(key, 'Q')
    noteAnswer(key, 'A')
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)
    const store = acquire(gitCommonDir(dir))
    const initial = resultOf(await fetchCase(store, 10, key)).value.LastAccessOrder
    assert.equal(resultOf(await touchCaseAccess(store, key)).ok, true)
    const direct = resultOf(await fetchCase(store, 10, key)).value.LastAccessOrder
    assert.ok(direct >= initial)
    await touchAccess(dir, key)
    const host = resultOf(await fetchCase(store, 10, key)).value.LastAccessOrder
    assert.ok(host >= direct)
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_disabled_marker_skips_publication', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-off-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    setEnabled(dir)
    const key = 'insp-off'
    notePrompt(key, 'Q')
    noteAnswer(key, 'A')
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)
    assert.equal(currentCases(acquire(gitCommonDir(dir))).size, 0)
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
