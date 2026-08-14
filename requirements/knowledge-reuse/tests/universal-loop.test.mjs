// G6-G: universal loop proof — CasebookLifecycle API end-to-end
// (notePrompt → noteAnswer → tryFinalize → fetch fresh; cleanup no write;
// CancelSession-style cleanup) + workflow archive/finalize guard.
import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  CasebookWorkflow_archiveInspectorResult as archive,
  CasebookWorkflow_finalizeCase as finalize,
  CasebookWorkflow_fetchCase as fetch,
} from '../../../dist/Infrastructure/CasebookWorkflow.js'
import {
  setEnabled,
  notePrompt,
  noteAnswer,
  tryFinalizeInspector,
  cleanupInspector,
} from '../../../dist/Infrastructure/CasebookLifecycle.js'
import { refreshStale } from '../../../dist/Infrastructure/CasebookBookkeeper.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { contentHash } from '../../../dist/Infrastructure/CasebookCapture.js'
import {
  ObservationCollector__Collect_Z15AE2BE0 as collect,
} from '../../../dist/Infrastructure/ObservationCollector.js'
import { collector } from '../../../dist/Infrastructure/CasebookLifecycle.js'
import { acquire } from '../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Journal/RuntimePath.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { toList, resultOf, listItems } from '../../verification-system/tests/support/domain.mjs'
import {
  CANONICAL_A,
  CANONICAL_Q,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'


const obsIndex = (n) => Object.create(Observation.prototype).cases().indexOf(n)
const fileRead = (p, h) => new Observation(obsIndex('FileRead'), [p, h])

test('G6_G_universal_loop_archive_finalize_fetch', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-'))
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const c1 = {
      SessionId: 'reuse-scope-1',
      Q: 'Q1',
      A: 'A1',
      Observations: toList([fileRead('a.txt', contentHash('hello'))]),
      LastAccessOrder: 0,
    }
    const r1 = resultOf(await archive(store, raw, c1))
    assert.equal(r1.ok, true)
    // same scope second finalize must be refused
    const second = resultOf(await finalize(store, raw, c1))
    assert.equal(second.ok, false)
    // final fetch should still see first case
    const fetched = resultOf(await fetch(store, raw, 10, 'reuse-scope-1'))
    assert.equal(fetched.value.A, 'A1')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('G6_G_lifecycle_note_finalize_fetch_and_cleanup', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-life-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    setEnabled(dir)
    const { port, createCalls, programCalls } = scriptedBookkeeperPort()
    setSessionPort(port)

    const sessionId = 'reuse-insp-1'
    notePrompt(sessionId, 'Who owns PromptAuthority?')
    collect(collector, sessionId, 'read', { path: 'a.txt' }, 'hello')
    const rawA = 'Host owns PromptAuthority.'
    noteAnswer(sessionId, rawA)

    const fin = resultOf(await tryFinalizeInspector(dir, sessionId))
    assert.equal(fin.ok, true, `finalize ok: ${JSON.stringify(fin.error)}`)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(await fetch(store, raw, 10, sessionId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.Q, 'Who owns PromptAuthority?')
    assert.notEqual(fetched.value.A, rawA, 'finalize synthesizes A via edit-qa')
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(fetched.value.A.includes('evidence:'), false)
    assert.equal(createCalls.length, 1, 'exactly one Bookkeeper child on finalize')
    assert.equal(programCalls.length >= 1, true)
    assert.equal(listItems(fetched.value.Observations).length, 1)

    const publishedA = fetched.value.A

    // CancelSession-style cleanup never writes (draft already taken by finalize)
    cleanupInspector(sessionId)
    const still = resultOf(await fetch(store, raw, 10, sessionId))
    assert.equal(still.value.A, publishedA, 'cleanup must not delete published case')

    // worktree drift → Bookkeeper synthesizes again and advances obs
    writeFileSync(join(dir, 'a.txt'), 'drift', 'utf8')
    const mech = resultOf(await refreshStale(store, raw, dir, sessionId))
    assert.equal(mech.ok, true)
    assert.equal(mech.value, true)
    const after = resultOf(await fetch(store, raw, 10, sessionId))
    assert.equal(after.value.Q, CANONICAL_Q)
    assert.equal(after.value.A.includes('evidence:'), false)
    assert.equal(createCalls.length, 2, 'one Bookkeeper child per finalize and refresh')
    assert.equal(listItems(after.value.Observations)[0].fields[1], contentHash('drift'))
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('G6_G_cancel_session_cleanup_no_publication', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-cancel-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)

    const sessionId = 'cancel-insp'
    notePrompt(sessionId, 'Q')
    collect(collector, sessionId, 'read', { path: 'x.txt' }, 'body')
    noteAnswer(sessionId, 'A')
    // unexpected SessionDeleted → cleanup only
    cleanupInspector(sessionId)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(await fetch(store, raw, 10, sessionId))
    assert.equal(fetched.value === undefined || fetched.value === null, true)
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
