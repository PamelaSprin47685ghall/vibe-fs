import assert from 'node:assert/strict'
import test from 'node:test'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

test('WHAT[context-compression-026] repair episode registration rejects a foreign request and drains its current registration', async (t) => {
  const scope = runtime.createScope()
  t.after(() => {
    try {
      runtime.beginBloggerShutdown(scope)
    } catch {}
  })

  const key = 'ses-blog-cc026'
  const requestId = 'req-cc026'
  const authorityRoot = 'msg-auth-cc026'
  const mainSession = 'ses-main-cc026'

  const request = runtime.main({
    requestId,
    mainSession,
    bloggerSession: key,
    toml: 'content-cc026',
  })
  runtime.claimCurrentRequest(scope, key, request)

  // 1. Initial claim on fresh slot succeeds
  const firstClaim = runtime.claimRepairEpisode(scope, requestId, authorityRoot, mainSession, key)
  assert.equal(firstClaim, 'Claimed', 'first claim on a clean slot must succeed')

  // 2. Conflicting claim on the active repair episode fails closed
  const conflictingClaim = runtime.claimRepairEpisode(scope, 'req-cc026-other', authorityRoot, mainSession, key)
  assert.match(conflictingClaim, /^Error:/, 'conflicting claim must fail closed')

  // 3. Re-claiming with same identity is recognized as same episode (not a fresh budget restart)
  const sameClaim = runtime.claimRepairEpisode(scope, requestId, authorityRoot, mainSession, key)
  assert.equal(sameClaim, 'Claimed', 'same episode identity reclaims existing episode slot')

  // 4. Drain repair episodes unregisters the episode synchronously
  const drained = runtime.drainRepairEpisodes(scope)
  await drained
})

test('WHAT[context-compression-026] actual closed-writer settlement fails all observers without releasing flight or notifying completion', async (t) => {
  const directory = mkdtempSync(join(tmpdir(), 'blogger-abandon-failure-'))
  const opened = await journal.JournalSurface_boot(directory, 'repair-runtime', 4242, '9999-01-01T00:00:00Z')
  assert.equal(opened.ok, true)
  const scope = runtime.createScope()
  t.after(() => {
    runtime.dispose(scope)
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  })
  const key = 'ses-blog-failed-abandon'
  const physical = 'msg-repair-root'
  assert.equal((await dispatch.acceptHumanRoot(opened.journal, key, physical, 'blogger')).ok, true)
  const request = runtime.main({ requestId: 'req-failed-abandon', mainSession: 'ses-main', bloggerSession: key, toml: 'work' })
  assert.equal(runtime.claimCurrentRequest(scope, key, request), 'Claimed')
  let sends = 0
  let terminals = 0
  const idle = (run) => ({
    quiescent: true,
    context: { sessionId: key, physicalUserMessageId: physical, authorityRoot: physical, providerRun: run },
    sessionPort: {
      SubscribeTerminal: () => ({ Dispose() {} }),
      SubscribeFutureTerminal: () => ({ Dispose() {} }),
      SendPrompt: async () => { sends++; return dispatch.admittedWithReceipt('unexpected') },
    },
    rootWorkspace: { TryRead: () => undefined },
    eventPort: {
      SubscribeTerminalListener: () => ({ Dispose() {} }),
      SubscribeFutureTerminalListener: () => ({ Dispose() {} }),
      NotifyTerminal: () => { terminals++; return false },
    },
  })
  journal.JournalSurface_dispose(opened.journal)
  const failed = await Promise.allSettled([
    blog.observeIdleRepair(scope, opened.journal.journal, request, idle('run-1')),
    blog.observeTransformRepair(scope, opened.journal.journal, request, 'run-1', []),
  ])
  assert.equal(failed[0].status, 'rejected')
  assert.equal(failed[1].status, 'rejected')
  assert.equal(failed[0].reason, failed[1].reason)
  assert.match(String(failed[0].reason), /abandon|closing|disposed|not attempted/i)
  for (const run of ['run-1', 'run-later']) {
    await assert.rejects(blog.observeIdleRepair(scope, opened.journal.journal, request, idle(run)), (error) => error === failed[0].reason)
  }
  assert.equal(runtime.currentRequest(scope, key).requestId, request.requestId)
  assert.equal(sends, 0)
  assert.equal(terminals, 0)
})
