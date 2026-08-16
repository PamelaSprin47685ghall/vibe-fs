// FROZEN — 2026-08-14. Lifecycle wiring observes canonical Casebook Current only.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  const handle = eventStore.EventStoreSurface_create(join(dir, '.git'), 'lifecycle-wiring')
  return {
    dir,
    handle,
    reopen: (writerId) => eventStore.EventStoreSurface_create(join(dir, '.git'), writerId),
    cleanup: () => {
      eventStore.EventStoreSurface_dispose(handle)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

test('WHAT[KNOWLEDGE-REUSE-002] lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once', async () => {
  const { dir, reopen, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port } = scriptedBookkeeperPort()
    bookkeeper.setSessionPort(port)
    const key = 'insp-finalize-1'
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.collect(key, 'read', { path: 'a.txt' }, 'hello')
    const rawA = 'PromptAuthority is owned by the Host.'
    lifecycle.noteAnswer(key, rawA)
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)

    const handle = reopen('lifecycle-finalize-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.equal(fetched.value.q, CANONICAL_Q)
      assert.notEqual(fetched.value.a, rawA)
      assert.equal(fetched.value.a, CANONICAL_A)
      assert.equal(fetched.value.observations.length, 1)
      const publishedA = fetched.value.a
      lifecycle.notePrompt(key, 'Q2')
      lifecycle.noteAnswer(key, 'A2')
      const second = await lifecycle.tryFinalize(dir, key)
      assert.equal(second.ok, false)
      assert.match(String(second.error), /already finalized/)
      assert.equal((await casebook.fetchCase(handle, 10, key)).value.a, publishedA)
    } finally {
      eventStore.EventStoreSurface_dispose(handle)
    }
  } finally {
    bookkeeper.resetSessionPort()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] lifecycle_cleanupInspector_never_publishes_case', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const key = 'insp-cleanup-1'
    lifecycle.notePrompt(key, 'Q cleanup')
    lifecycle.collect(key, 'read', { path: 'b.txt' }, 'body')
    lifecycle.noteAnswer(key, 'A cleanup')
    assert.ok(lifecycle.observationCount(key) > 0)
    lifecycle.cleanup(key)
    assert.equal(lifecycle.observationCount(key), 0)
    assert.equal((await casebook.fetchCase(handle, 10, key)).value, null)
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)
    assert.equal((await casebook.fetchCase(handle, 10, key)).value, null)
  } finally {
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] lifecycle_missing_answer_is_noop_finalize', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const key = 'insp-no-a'
    lifecycle.notePrompt(key, 'Q only')
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)
    assert.equal((await casebook.fetchCase(handle, 10, key)).value, null)
  } finally {
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-008] lifecycle_touchAccess_and_touchCaseAccess_advance_integrated_access_order', async () => {
  const { dir, reopen, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port } = scriptedBookkeeperPort()
    bookkeeper.setSessionPort(port)
    const key = 'insp-access-1'
    lifecycle.notePrompt(key, 'Q')
    lifecycle.noteAnswer(key, 'A')
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)

    const initialHandle = reopen('lifecycle-access-initial')
    let initial
    try {
      const initialResult = await casebook.fetchCase(initialHandle, 10, key)
      assert.equal(initialResult.ok, true)
      initial = initialResult.value.lastAccessOrder
    } finally {
      eventStore.EventStoreSurface_dispose(initialHandle)
    }

    const directHandle = reopen('lifecycle-access-direct')
    let direct
    try {
      assert.equal((await casebook.touchAccess(directHandle, key)).ok, true)
      const directResult = await casebook.fetchCase(directHandle, 10, key)
      assert.equal(directResult.ok, true)
      direct = directResult.value.lastAccessOrder
    } finally {
      eventStore.EventStoreSurface_dispose(directHandle)
    }
    assert.ok(direct >= initial)

    await lifecycle.touchAccess(dir, key)
    const hostHandle = reopen('lifecycle-access-host')
    let host
    try {
      const hostResult = await casebook.fetchCase(hostHandle, 10, key)
      assert.equal(hostResult.ok, true)
      host = hostResult.value.lastAccessOrder
    } finally {
      eventStore.EventStoreSurface_dispose(hostHandle)
    }
    assert.ok(host >= direct)
  } finally {
    bookkeeper.resetSessionPort()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-009] lifecycle_disabled_marker_skips_publication', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-off-'))
  execFileSync('git', ['init', '--quiet', dir])
  const handle = eventStore.EventStoreSurface_create(join(dir, '.git'), 'lifecycle-off')
  try {
    lifecycle.enable(dir)
    const key = 'insp-off'
    lifecycle.notePrompt(key, 'Q')
    lifecycle.noteAnswer(key, 'A')
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)
    assert.equal((await casebook.fetchCase(handle, 10, key)).value, null)
  } finally {
    lifecycle.disable()
    eventStore.EventStoreSurface_dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})
