// FROZEN — 2026-08-14. Synthesis uses canonical Casebook Current; no history scan.
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
const FAIL_A = 'A-must-fail-synthesis'
const failingPort = () => {
  let seq = 0
  return {
    port: {
      CreateChildSession: async () => {
        throw new Error('Bookkeeper must not attach to a deleted physical parent')
      },
      CreateSiblingSession: async () => bookkeeper.acceptedSession(`bk-fail-${++seq}`),
      AbortSession: async () => bookkeeper.aborted(),
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => bookkeeper.failedPrompt('injected synth failure'),
    },
  }
}
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_injected_synthesizer_error_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-err-'))
  const handle = eventStore.create(dir, 'bookkeeper-synthesis-error')
  const { port } = failingPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-err-1', 'Q keep', FAIL_A, [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    bookkeeper.setSessionPort(port)
    const refreshed = await bookkeeper.refreshStale(handle, dir, 's-err-1')
    assert.equal(refreshed.ok, false)
    assert.match(String(refreshed.error), /injected synth failure/)
    const fetched = await casebook.fetchCase(handle, 10, 's-err-1')
    assert.equal(fetched.value.a, FAIL_A)
    assert.equal(fetched.value.q, 'Q keep')
    assert.equal(fetched.value.observations[0].contentHash, casebook.contentHash('hello'))
  } finally {
    bookkeeper.resetSessionPort()
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_synthesizer_runs_once_per_stale_refresh', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-once-'))
  const handle = eventStore.create(dir, 'bookkeeper-synthesis-once')
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-once', 'Q-count-synth-once', 'A once', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    bookkeeper.setSessionPort(port)
    const refreshed = await bookkeeper.refreshStale(handle, dir, 's-once')
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
  } finally {
    bookkeeper.resetSessionPort()
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_uses_synthesizer_not_raw_noteAnswer', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-fin-'))
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    lifecycle.enable(dir)
    bookkeeper.setSessionPort(port)
    const key = 'insp-synth-fin'
    const rawA = 'PromptAuthority is owned by the Host.'
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.collect(key, 'read', { path: 'a.txt' }, 'hello')
    lifecycle.noteAnswer(key, rawA)
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const handle = eventStore.create(join(dir, '.git'), 'bookkeeper-synthesis-finalize-read')
    const fetched = await casebook.fetchCase(handle, 10, key)
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.notEqual(fetched.value.a, rawA)
    assert.equal(fetched.value.a, CANONICAL_A)
    const publishedA = fetched.value.a
    lifecycle.notePrompt(key, 'Q2')
    lifecycle.noteAnswer(key, 'A2')
    const second = await lifecycle.tryFinalize(dir, key)
    assert.equal(second.ok, false)
    assert.match(String(second.error), /already finalized/)
    assert.equal((await casebook.fetchCase(handle, 10, key)).value.a, publishedA)
    eventStore.dispose(handle)
  } finally {
    bookkeeper.resetSessionPort()
    lifecycle.disable()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_cleanup_never_synthesizes', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-cleanup-'))
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    lifecycle.enable(dir)
    bookkeeper.setSessionPort(port)
    const key = 'insp-cleanup-synth'
    lifecycle.notePrompt(key, 'Q-cleanup-never-synth')
    lifecycle.collect(key, 'read', { path: 'b.txt' }, 'body')
    lifecycle.noteAnswer(key, 'A cleanup')
    lifecycle.cleanup(key)
    assert.equal(createCalls.length, 0)
    assert.equal(programCalls.length, 0)
    const handle = eventStore.create(join(dir, '.git'), 'bookkeeper-synthesis-cleanup-read')
    const fetched = await casebook.fetchCase(handle, 10, key)
    assert.equal(fetched.value, null)
    eventStore.dispose(handle)
  } finally {
    bookkeeper.resetSessionPort()
    lifecycle.disable()
    rmSync(dir, { recursive: true, force: true })
  }
})
