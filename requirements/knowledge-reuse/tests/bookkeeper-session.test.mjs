// FROZEN — 2026-08-14. Bookkeeper session tests use local EventStore Current only.
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
import * as bookkeeperRefresh from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
export const CANONICAL_Q = 'Canonical maintained question'
export const CANONICAL_A = 'Summary of Inspector answers across turns.'

export const scriptedBookkeeperPort = () => {
  const createCalls = []
  const prompts = []
  const programCalls = []
  const terminals = new Set()
  let seq = 0
  const port = {
    CreateChildSession: async () => {
      throw new Error('Bookkeeper must not attach to a deleted physical parent')
    },
    CreateSiblingSession: async (_ownerSessionId, physicalParentId) => {
      seq += 1
      const child = `bk-child-${seq}`
      createCalls.push({ child, physicalParentId })
      return bookkeeper.acceptedSession(child)
    },
    AbortSession: async () => bookkeeper.aborted(),
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return { Dispose: () => terminals.delete(callback) }
    },
    SendPrompt: async (childSession, text) => {
      prompts.push(text)
      const sid = bookkeeper.sessionValue(childSession)
      const tx = bookkeeper.txIdFor(sid)
      assert.notEqual(tx, '', 'SendPrompt must run against a bound Bookkeeper tx')
      const out = await bookkeeper.runProgram(
        sid,
        `class Js extends JsProgram { async run() { this.setQuestion(${JSON.stringify(CANONICAL_Q)}); this.setAnswer(${JSON.stringify(CANONICAL_A)}); return { changed: true }; } }`,
      )
      assert.equal(String(out).includes('changed = true'), true, out)
      programCalls.push(tx)
      for (const callback of terminals) callback(bookkeeper.sessionId(sid), bookkeeper.completed(sid))
      return bookkeeper.acceptedPrompt()
    },
  }
  return { port, createCalls, prompts, programCalls }
}

export const installBookkeeperRuntime = (port, ownerSessionIds) => {
  const installed = bookkeeper.setRuntime(
    port,
    ownerSessionIds.map((sessionId) => ({
      sessionId,
      logicalRunId: `bookkeeper-run-${sessionId}`,
      authorityRootUserMessageId: `bookkeeper-root-${sessionId}`,
      agent: 'inspector',
    })),
  )
  assert.equal(installed.ok, true, installed.error)
}

const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })
const openStore = (dir, writerId) => eventStore.create(join(dir, '.git'), writerId)

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_create_child_once_per_refresh_via_js_bookkeeper', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-refresh-'))
  const handle = eventStore.create(dir, 'bookkeeper-session-refresh')
  const { port, createCalls, programCalls, prompts } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-session-refresh', 'Q keep', 'A keep', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    await installBookkeeperRuntime(port, ['s-session-refresh'])
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-session-refresh')
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(createCalls[0].physicalParentId, undefined)
    assert.equal(programCalls.length >= 1, true)
    assert.equal(prompts.some((text) => String(text).includes('CaseRefresh')), true)
    const fetched = await casebook.fetchCase(handle, 10, 's-session-refresh')
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.equal(fetched.value.a, CANONICAL_A)
  } finally {
    bookkeeper.resetRuntime()
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_create_child_once_and_cleanup_never_runs_bookkeeper', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-fin-'))
  const { port, createCalls, programCalls, prompts } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    lifecycle.enable(dir)
    const key = 'insp-session-fin'
    await installBookkeeperRuntime(port, [key])
    lifecycle.notePrompt(key, 'Who owns PromptAuthority?')
    lifecycle.noteAnswer(key, 'Host owns PromptAuthority.')
    lifecycle.notePrompt(key, 'Where do Case facts live?')
    lifecycle.collect(key, 'read', { path: 'a.txt' }, 'hello')
    lifecycle.noteAnswer(key, 'Unified EventStore only.')
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)
    assert.equal(createCalls.length, 1)
    assert.equal(createCalls[0].physicalParentId, undefined)
    assert.equal(programCalls.length >= 1, true)
    assert.equal(prompts.some((text) => String(text).includes('CaseFinalize')), true)

    const handle = openStore(dir, 'bookkeeper-session-finalize-read')
    const fetched = await casebook.fetchCase(handle, 10, key)
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.equal(fetched.value.a, CANONICAL_A)
    const before = createCalls.length
    lifecycle.notePrompt(key, 'cleanup Q')
    lifecycle.noteAnswer(key, 'cleanup A')
    lifecycle.cleanup(key)
    assert.equal(createCalls.length, before)
    eventStore.dispose(handle)
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-005] CASE006_missing_runtime_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-noport-'))
  const handle = eventStore.create(dir, 'bookkeeper-session-noport')
  try {
    bookkeeper.resetRuntime()
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-noport', 'Q keep', 'A keep', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-noport')
    assert.equal(refreshed.ok, false)
    assert.match(String(refreshed.error), /runtime unavailable/)
    const fetched = await casebook.fetchCase(handle, 10, 's-noport')
    assert.equal(fetched.value.q, 'Q keep')
    assert.equal(fetched.value.a, 'A keep')
  } finally {
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})
