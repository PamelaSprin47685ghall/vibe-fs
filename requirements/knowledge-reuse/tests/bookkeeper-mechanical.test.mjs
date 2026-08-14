// FROZEN — 2026-08-14. Bookkeeper reads/writes only canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { CasebookWorkflow_needsRefresh as needsRefresh } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { refreshStale } from '../../../dist/Repository/Knowledge/Casebook/Bookkeeper.js'
import { contentHash as hash } from '../../../dist/Repository/Knowledge/Casebook/Capture.js'
import { Observation } from '../../../dist/Repository/Knowledge/Casebook/Model.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { listItems, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'
import { BookkeeperRuntime_setSessionPort as setSessionPort, BookkeeperRuntime_resetSessionPort as resetSessionPort } from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRuntime.js'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const record = (sessionId, q, a, observations) => ({ SessionId: sessionId, Q: q, A: a, Observations: toList(observations), LastAccessOrder: 0 })

test('CASE006_synthesis_refresh_publishes_refreshed_with_revised_a', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    const store = local.store
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal(resultOf(await archive(store, record('s-mech-1', 'Q keep', 'A keep', [fileRead('a.txt', hash('hello'))]))).ok, true)
    assert.equal(resultOf(await needsRefresh(store, 10, 's-mech-1', dir)).value, false)
    assert.equal(resultOf(await refreshStale(store, dir, 's-mech-1')).value, false)
    assert.equal(createCalls.length, 0)

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    assert.equal(resultOf(await needsRefresh(store, 10, 's-mech-1', dir)).value, true)
    setSessionPort(port)
    const refreshed = resultOf(await refreshStale(store, dir, 's-mech-1'))
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const fetched = resultOf(await fetchCase(store, 10, 's-mech-1'))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(listItems(fetched.value.Observations)[0].fields[1], hash('changed'))
    assert.equal(resultOf(await needsRefresh(store, 10, 's-mech-1', dir)).value, false)
    assert.ok(store.TryCurrent('Casebook'))
  } finally {
    resetSessionPort()
    local.close()
    cleanup()
  }
})

test('CASE006_mechanical_refresh_no_case_is_noop', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    const r = resultOf(await refreshStale(local.store, dir, 'missing'))
    assert.equal(r.ok, true)
    assert.equal(r.value, false)
  } finally {
    local.close()
    cleanup()
  }
})

test('CASE006_mechanical_refresh_missing_file_still_publishes', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    const store = local.store
    writeFileSync(join(dir, 'gone.txt'), 'x', 'utf8')
    assert.equal(resultOf(await archive(store, record('s-gone', 'Q', 'A', [fileRead('gone.txt', hash('x'))]))).ok, true)
    rmSync(join(dir, 'gone.txt'), { force: true })
    setSessionPort(port)
    const r = resultOf(await refreshStale(store, dir, 's-gone'))
    assert.equal(r.ok, true)
    assert.equal(r.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
    const fetched = resultOf(await fetchCase(store, 10, 's-gone'))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.equal(listItems(fetched.value.Observations).length, 0)
  } finally {
    resetSessionPort()
    local.close()
    cleanup()
  }
})
