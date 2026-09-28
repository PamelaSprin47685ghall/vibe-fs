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
import {
  CANONICAL_A,
  installBookkeeperRuntime,
  scriptedBookkeeperPort,
} from '../../knowledge-reuse/tests/support/bookkeeper-session-support.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delegate-settle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return {
    dir,
    cleanup: () => rmSync(dir, { recursive: true, force: true }),
  }
}

test('WHAT[managed-session-lifecycle-022] CASE_SETTLE_uncommitted_finalize_does_not_publish_a_case', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    // No Bookkeeper runtime: observe archive refusal, not identity retention.
    const key = 'insp-settle-uncommitted'
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.noteAnswer(key, 'Host owns PromptAuthority.')

    const settled = await lifecycle.tryFinalize(dir, key)
    assert.equal(settled.ok, false)
    assert.match(String(settled.error), /bookkeeper runtime unavailable/)

    const handle = eventStore.create(join(dir, '.git'), 'insp-settle-uncommitted-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.equal(fetched.value, null)
    } finally {
      eventStore.dispose(handle)
    }
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[managed-session-lifecycle-022] archive commits once and duplicate finalize creates no second Bookkeeper child', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port, createCalls } = scriptedBookkeeperPort()
    const key = 'insp-settle-finalized'
    await installBookkeeperRuntime(port, [key])
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.noteAnswer(key, 'Host owns PromptAuthority.')
    const first = await lifecycle.tryFinalize(dir, key)
    assert.equal(first.ok, true)
    assert.equal(createCalls.length, 1)

    const handle = eventStore.create(join(dir, '.git'), 'insp-settle-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.notEqual(fetched.value, null)
    } finally {
      eventStore.dispose(handle)
    }

    lifecycle.notePrompt(key, 'second finalize must not publish')
    lifecycle.noteAnswer(key, 'should be refused')
    const second = await lifecycle.tryFinalize(dir, key)
    assert.equal(second.ok, false)
    assert.match(String(second.error), /already finalized/)
    assert.equal(createCalls.length, 1)

    const reread = eventStore.create(join(dir, '.git'), 'insp-settle-reread')
    try {
      const still = await casebook.fetchCase(reread, 10, key)
      assert.equal(still.ok, true)
      assert.equal(still.value.sessionId, key)
      assert.equal(still.value.a, CANONICAL_A)
    } finally {
      eventStore.dispose(reread)
    }
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[managed-session-lifecycle-022] no draft finalizes successfully without creating a Bookkeeper child', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port, createCalls } = scriptedBookkeeperPort()
    const key = 'insp-settle-empty'
    await installBookkeeperRuntime(port, [key])
    const settled = await lifecycle.tryFinalize(dir, key)
    assert.equal(settled.ok, true)
    assert.equal(createCalls.length, 0)
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})

test.todo('WHAT[managed-session-lifecycle-022] real deletion preserves exact Inspector identity for NotCommitted, Unknown and PhaseConflict; only committed or empty finalization releases it (GAP-133)')
