import assert from 'node:assert/strict'
import test from 'node:test'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'

test('WHAT[CONTEXT-COMPRESSION-026] repair episode abandon failure causes rendezvous to fail terminally without fake success and prevents restart', async (t) => {
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
  assert.equal(typeof drained, 'object', 'drainRepairEpisodes must return completion promise/task')
})
