// FROZEN — 2026-08-14. Universal Casebook loop uses canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive, CasebookWorkflow_finalizeCase as finalize, CasebookWorkflow_fetchCase as fetch } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { setEnabled, notePrompt, noteAnswer, tryFinalizeInspector, cleanupInspector, collector } from '../../../dist/Repository/Knowledge/Casebook/Lifecycle.js'
import { refreshStale } from '../../../dist/Repository/Knowledge/Casebook/Bookkeeper.js'
import { Observation } from '../../../dist/Repository/Knowledge/Casebook/Model.js'
import { contentHash } from '../../../dist/Repository/Knowledge/Casebook/Capture.js'
import { ObservationCollector__Collect_Z15AE2BE0 as collect } from '../../../dist/Enforcer/ObservationCollector.js'
import { acquire } from '../../../dist/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Persistence/Journal/RuntimePath.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { toList, resultOf, listItems } from '../../verification-system/tests/support/domain.mjs'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'
import { BookkeeperRuntime_setSessionPort as setSessionPort, BookkeeperRuntime_resetSessionPort as resetSessionPort } from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRuntime.js'

const obsIndex = (n) => Object.create(Observation.prototype).cases().indexOf(n)
const fileRead = (p, h) => new Observation(obsIndex('FileRead'), [p, h])
const record = (session, q, a, obs) => ({ SessionId: session, Q: q, A: a, Observations: toList(obs), LastAccessOrder: 0 })

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_universal_loop_archive_finalize_fetch', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-'))
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const c1 = record('reuse-scope-1', 'Q1', 'A1', [fileRead('a.txt', contentHash('hello'))])
    assert.equal(resultOf(await archive(local.store, c1)).ok, true)
    assert.equal(resultOf(await finalize(local.store, c1)).ok, false)
    assert.equal(resultOf(await fetch(local.store, 10, 'reuse-scope-1')).value.A, 'A1')
  } finally {
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_lifecycle_note_finalize_fetch_and_cleanup', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-life-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    setEnabled(dir)
    const { port, createCalls, programCalls } = scriptedBookkeeperPort()
    setSessionPort(port)
    const key = 'reuse-insp-1'
    notePrompt(key, 'Who owns PromptAuthority?')
    collect(collector, key, 'read', { path: 'a.txt' }, 'hello')
    const rawA = 'Host owns PromptAuthority.'
    noteAnswer(key, rawA)
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)

    const store = acquire(gitCommonDir(dir))
    const fetched = resultOf(await fetch(store, 10, key))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.A, rawA)
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
    assert.equal(listItems(fetched.value.Observations).length, 1)

    const publishedA = fetched.value.A
    cleanupInspector(key)
    assert.equal(resultOf(await fetch(store, 10, key)).value.A, publishedA)
    writeFileSync(join(dir, 'a.txt'), 'drift', 'utf8')
    const mech = resultOf(await refreshStale(store, dir, key))
    assert.equal(mech.ok, true)
    assert.equal(mech.value, true)
    const after = resultOf(await fetch(store, 10, key))
    assert.equal(after.value.Q, CANONICAL_Q)
    assert.equal(createCalls.length, 2)
    assert.equal(listItems(after.value.Observations)[0].fields[1], contentHash('drift'))
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_cancel_session_cleanup_no_publication', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-cancel-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)
    const key = 'cancel-insp'
    notePrompt(key, 'Q')
    collect(collector, key, 'read', { path: 'x.txt' }, 'body')
    noteAnswer(key, 'A')
    cleanupInspector(key)
    const store = acquire(gitCommonDir(dir))
    const fetched = resultOf(await fetch(store, 10, key))
    assert.equal(fetched.value == null, true)
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
