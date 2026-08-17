// FROZEN — 2026-08-14. Universal Casebook loop uses canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_universal_loop_archive_finalize_fetch', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-'))
  const handle = eventStore.create(dir, 'universal-archive')
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const c1 = record('reuse-scope-1', 'Q1', 'A1', [fileRead('a.txt', casebook.contentHash('hello'))])
    assert.equal((await casebook.archive(handle, c1)).ok, true)
    assert.equal((await casebook.finalize(handle, c1)).ok, false)
    assert.equal((await casebook.fetchCase(handle, 10, 'reuse-scope-1')).value.a, 'A1')
  } finally {
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_lifecycle_note_finalize_fetch_and_cleanup', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-life-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    lifecycle.enable(dir)
    const { port, createCalls, programCalls } = scriptedBookkeeperPort()
    bookkeeper.setSessionPort(port)
    const key = 'reuse-insp-1'
    lifecycle.notePrompt(key, 'Who owns PromptAuthority?')
    lifecycle.collect(key, 'read', { path: 'a.txt' }, 'hello')
    const rawA = 'Host owns PromptAuthority.'
    lifecycle.noteAnswer(key, rawA)
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)

    const handle = eventStore.create(join(dir, '.git'), 'universal-lifecycle-read')
    const fetched = await casebook.fetchCase(handle, 10, key)
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.notEqual(fetched.value.a, rawA)
    assert.equal(fetched.value.a, CANONICAL_A)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
    assert.equal(fetched.value.observations.length, 1)

    const publishedA = fetched.value.a
    lifecycle.cleanup(key)
    assert.equal((await casebook.fetchCase(handle, 10, key)).value.a, publishedA)
    writeFileSync(join(dir, 'a.txt'), 'drift', 'utf8')
    const refreshed = await bookkeeper.refreshStale(handle, dir, key)
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    const after = await casebook.fetchCase(handle, 10, key)
    assert.equal(after.value.q, CANONICAL_Q)
    assert.equal(createCalls.length, 2)
    assert.equal(after.value.observations[0].contentHash, casebook.contentHash('drift'))
    eventStore.dispose(handle)
  } finally {
    bookkeeper.resetSessionPort()
    lifecycle.disable()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_cancel_session_cleanup_no_publication', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-cancel-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    lifecycle.enable(dir)
    const key = 'cancel-insp'
    lifecycle.notePrompt(key, 'Q')
    lifecycle.collect(key, 'read', { path: 'x.txt' }, 'body')
    lifecycle.noteAnswer(key, 'A')
    lifecycle.cleanup(key)
    const handle = eventStore.create(join(dir, '.git'), 'universal-cancel-read')
    assert.equal((await casebook.fetchCase(handle, 10, key)).value, null)
    eventStore.dispose(handle)
  } finally {
    lifecycle.disable()
    rmSync(dir, { recursive: true, force: true })
  }
})
