// MANAGED-SESSION-021/022: case finalize settles to a closed union and
// the outer evidence decision retains the identity on every unsettled path.
// Only a durably-settled finalize releases the identity; NotCommitted/Unknown
// stay resumable and PhaseConflict keeps the identity for the incident.
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
} from '../../knowledge-reuse/tests/bookkeeper-session.test.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delegate-settle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return {
    dir,
    cleanup: () => rmSync(dir, { recursive: true, force: true }),
  }
}

test('WHAT[MANAGED-SESSION-021] CASE_SETTLE_finalized_releases_identity_exactly_once', async () => {
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

    // Duplicate finalize for the same session is the PhaseConflict path: the
    // completion is neither re-executed nor forgotten — the original Case is
    // retained and no second Bookkeeper child is created.
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

test('WHAT[MANAGED-SESSION-021] CASE_SETTLE_nothing_to_finalize_is_not_a_failure', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port, createCalls } = scriptedBookkeeperPort()
    const key = 'insp-settle-empty'
    await installBookkeeperRuntime(port, [key])

    // No draft at all: NothingToFinalize releases the identity without error
    // and without running Bookkeeper.
    const settled = await lifecycle.tryFinalize(dir, key)
    assert.equal(settled.ok, true)
    assert.equal(createCalls.length, 0)
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[MANAGED-SESSION-022] CASE_SETTLE_uncommitted_finalize_does_not_publish_a_case', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    // No Bookkeeper runtime installed: the CaseFinalize transaction cannot be
    // attempted. The finalize is NotCommitted — the identity is retained for
    // resume and no half-published Case exists.
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

test('WHAT[MANAGED-SESSION-021] CASE_SETTLE_identity_retention_is_owner_driven_not_finally', async () => {
  const { readFileSync } = await import('node:fs')
  const { fileURLToPath } = await import('node:url')
  const root = fileURLToPath(new URL('../../..', import.meta.url))
  const { join: joinPath } = await import('node:path')
  const deletion = readFileSync(joinPath(root, 'src/Wanxiangshu/OpenCode/Host/HostSessionDeletion.fs'), 'utf8')

  // The outer evidence decision must run AFTER the exact finalize evidence is
  // captured: no unconditional finally may drop the identity a failed finalize
  // still needs for resume.
  assert.doesNotMatch(deletion, /finally\s*\n\s*scope\.DropSessionIdentity/)
  assert.match(deletion, /CaseFinalizeCommitment\.Finalized/)
  assert.match(deletion, /CaseFinalizeCommitment\.NothingToFinalize/)
  assert.match(deletion, /CaseFinalizeCommitment\.NotCommitted/)
  assert.match(deletion, /CaseFinalizeCommitment\.Unknown/)
  assert.match(deletion, /CaseFinalizeCommitment\.PhaseConflict/)
})
