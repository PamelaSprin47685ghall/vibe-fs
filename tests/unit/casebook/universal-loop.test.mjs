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
import { toList, resultOf, listItems } from '../support/domain.mjs'

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
    const r1 = resultOf(archive(store, raw, c1))
    assert.equal(r1.ok, true)
    // same scope second finalize must be refused
    const second = resultOf(finalize(store, raw, c1))
    assert.equal(second.ok, false)
    // final fetch should still see first case
    const fetched = resultOf(fetch(store, raw, 10, 'reuse-scope-1'))
    assert.equal(fetched.value.A, 'A1')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('G6_G_lifecycle_note_finalize_fetch_and_cleanup', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-life-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    setEnabled(dir)

    const sessionId = 'reuse-insp-1'
    notePrompt(sessionId, 'Who owns PromptAuthority?')
    collect(collector, sessionId, 'read', { path: 'a.txt' }, 'hello')
    noteAnswer(sessionId, 'Host owns PromptAuthority.')

    const fin = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(fin.ok, true, `finalize ok: ${JSON.stringify(fin.error)}`)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(fetch(store, raw, 10, sessionId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value.Q, 'Who owns PromptAuthority?')
    assert.equal(fetched.value.A, 'Host owns PromptAuthority.')
    assert.equal(listItems(fetched.value.Observations).length, 1)

    // CancelSession-style cleanup never writes (draft already taken by finalize)
    cleanupInspector(sessionId)
    const still = resultOf(fetch(store, raw, 10, sessionId))
    assert.equal(still.value.A, 'Host owns PromptAuthority.', 'cleanup must not delete published case')

    // worktree drift → mechanical Bookkeeper advances obs, keeps A
    writeFileSync(join(dir, 'a.txt'), 'drift', 'utf8')
    const mech = resultOf(refreshStale(store, raw, dir, sessionId))
    assert.equal(mech.ok, true)
    assert.equal(mech.value, true)
    const after = resultOf(fetch(store, raw, 10, sessionId))
    assert.equal(after.value.A, 'Host owns PromptAuthority.')
    assert.equal(listItems(after.value.Observations)[0].fields[1], contentHash('drift'))
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('G6_G_cancel_session_cleanup_no_publication', () => {
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
    const fetched = resultOf(fetch(store, raw, 10, sessionId))
    assert.equal(fetched.value === undefined || fetched.value === null, true)
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
