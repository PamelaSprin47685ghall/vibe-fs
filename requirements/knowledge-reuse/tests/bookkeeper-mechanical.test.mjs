// FROZEN — 2026-08-14. Bookkeeper reads/writes only canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import * as bookkeeperRefresh from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js'
import {
  CANONICAL_A,
  CANONICAL_Q,
  installBookkeeperRuntime,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-'))
  const handle = eventStore.create(dir, 'bookkeeper-mechanical')
  return {
    dir,
    handle,
    cleanup: () => {
      eventStore.dispose(handle)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_synthesis_refresh_publishes_refreshed_with_revised_a', async () => {
  const { dir, handle, cleanup } = sandbox()
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-mech-1', 'Q keep', 'A keep', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    assert.equal((await casebook.needsRefresh(handle, 10, 's-mech-1', dir)).value, false)
    assert.equal((await bookkeeperRefresh.refreshStale(handle, dir, 's-mech-1')).value, false)
    assert.equal(createCalls.length, 0)

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    assert.equal((await casebook.needsRefresh(handle, 10, 's-mech-1', dir)).value, true)
    await installBookkeeperRuntime(port, ['s-mech-1'])
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-mech-1')
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const fetched = await casebook.fetchCase(handle, 10, 's-mech-1')
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.equal(fetched.value.a, CANONICAL_A)
    assert.equal(fetched.value.observations[0].contentHash, casebook.contentHash('changed'))
    assert.equal((await casebook.needsRefresh(handle, 10, 's-mech-1', dir)).value, false)
    assert.notEqual(fetched.value, null)
  } finally {
    bookkeeper.resetRuntime()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_mechanical_refresh_no_case_is_noop', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    const result = await bookkeeperRefresh.refreshStale(handle, dir, 'missing')
    assert.equal(result.ok, true)
    assert.equal(result.value, false)
  } finally {
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_mechanical_refresh_missing_file_still_publishes', async () => {
  const { dir, handle, cleanup } = sandbox()
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'gone.txt'), 'x', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-gone', 'Q', 'A', [fileRead('gone.txt', casebook.contentHash('x'))]))).ok, true)
    rmSync(join(dir, 'gone.txt'), { force: true })
    await installBookkeeperRuntime(port, ['s-gone'])
    const result = await bookkeeperRefresh.refreshStale(handle, dir, 's-gone')
    assert.equal(result.ok, true)
    assert.equal(result.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
    const fetched = await casebook.fetchCase(handle, 10, 's-gone')
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.equal(fetched.value.observations.length, 0)
  } finally {
    bookkeeper.resetRuntime()
    cleanup()
  }
})
