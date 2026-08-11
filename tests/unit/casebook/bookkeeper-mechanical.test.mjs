import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { CasebookWorkflow_needsRefresh as needsRefresh } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { refreshStale } from '../../../dist/Infrastructure/CasebookBookkeeper.js'
import { contentHash as hash } from '../../../dist/Infrastructure/CasebookCapture.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { loadEvents } from '../../../dist/Infrastructure/CasebookStore.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { caseOf, listItems, resultOf, toList } from '../support/domain.mjs'
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

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('CASE006_synthesis_refresh_publishes_refreshed_with_revised_a', async () => {
  const { dir, cleanup } = sandbox()
  const { port, createCalls, editQaCalls } = scriptedBookkeeperPort()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = {
      SessionId: 's-mech-1',
      Q: 'Q keep',
      A: 'A keep',
      Observations: toList([fileRead('a.txt', hash('hello'))]),
      LastAccessOrder: 0,
    }
    assert.equal(resultOf(archive(store, raw, caseRec)).ok, true)

    assert.equal(resultOf(needsRefresh(store, raw, 10, 's-mech-1', dir)).value, false)
    assert.equal(resultOf(await refreshStale(store, raw, dir, 's-mech-1')).value, false)
    assert.equal(createCalls.length, 0, 'fresh case must not open a Bookkeeper child')

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    assert.equal(resultOf(needsRefresh(store, raw, 10, 's-mech-1', dir)).value, true)

    setSessionPort(port)
    const refreshed = resultOf(await refreshStale(store, raw, dir, 's-mech-1'))
    assert.equal(refreshed.ok, true, `refreshStale ok, got ${JSON.stringify(refreshed.error)}`)
    assert.equal(refreshed.value, true, 'bookkeeper refresh must publish')
    assert.equal(createCalls.length, 1)
    assert.equal(editQaCalls.length >= 2, true, 'edit-qa must be invoked')

    const fetched = resultOf(fetchCase(store, raw, 10, 's-mech-1'))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.Q, 'Q keep')
    assert.notEqual(fetched.value.A, 'A keep')
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(fetched.value.A.includes('evidence:'), false)
    const obs = listItems(fetched.value.Observations)
    assert.equal(obs.length, 1)
    assert.equal(obs[0].fields[1], hash('changed'), 'observation hash advanced to worktree')

    assert.equal(resultOf(needsRefresh(store, raw, 10, 's-mech-1', dir)).value, false)

    const events = listItems(resultOf(loadEvents(raw, store.OpenSnapshot())).value)
    const kinds = events.map((e) => caseOf(e))
    assert.equal(kinds.includes('CaseCaptured'), true)
    assert.equal(kinds.includes('CaseRefreshed'), true)
  } finally {
    resetSessionPort()
    cleanup()
  }
})

test('CASE006_mechanical_refresh_no_case_is_noop', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    const r = resultOf(await refreshStale(store, raw, dir, 'missing'))
    assert.equal(r.ok, true)
    assert.equal(r.value, false)
  } finally {
    cleanup()
  }
})

test('CASE006_mechanical_refresh_missing_file_still_publishes', async () => {
  const { dir, cleanup } = sandbox()
  const { port, createCalls, editQaCalls } = scriptedBookkeeperPort()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'gone.txt'), 'x', 'utf8')
    const caseRec = {
      SessionId: 's-gone',
      Q: 'Q',
      A: 'A',
      Observations: toList([fileRead('gone.txt', hash('x'))]),
      LastAccessOrder: 0,
    }
    assert.equal(resultOf(archive(store, raw, caseRec)).ok, true)
    rmSync(join(dir, 'gone.txt'), { force: true })

    setSessionPort(port)
    const r = resultOf(await refreshStale(store, raw, dir, 's-gone'))
    assert.equal(r.ok, true)
    assert.equal(r.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(editQaCalls.length >= 2, true)

    const fetched = resultOf(fetchCase(store, raw, 10, 's-gone'))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.A, 'A')
    assert.equal(fetched.value.A.includes('evidence:'), false)
    assert.equal(listItems(fetched.value.Observations).length, 0)
  } finally {
    resetSessionPort()
    cleanup()
  }
})
