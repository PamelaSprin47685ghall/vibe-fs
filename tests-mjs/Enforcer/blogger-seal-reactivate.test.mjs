// Blogger seal after join/return + Authority Root reactivation.
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  bloggerRuntime,
  bloggerRequestContext,
  handleProjection,
  handleId,
  sessionId,
  roles,
} from '../domain.mjs'

const ctx = () =>
  bloggerRequestContext.main({
    requestId: 'req-1',
    mainSession: 'ses-main',
    bloggerSession: 'ses-blog',
    toml: 'delta-a',
    previousIngested: 0,
    nextIngested: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    nextDigest: 'd1',
    deltaDigest: 'sha-a',
  })

test('HANDLE_lifecycle_CompletedAwaitingJoin_and_Retired_seal_blogger', () => {
  let proj = handleProjection.empty
  const h = handleId.agent('agent-1')
  const child = sessionId('ses-child')
  const linked = handleProjection.link(h, child, 'fast-coder', roles.Coder, proj)
  assert.equal(linked.ok, true)
  proj = linked.value
  assert.equal(handleProjection.recordSealsBlogger(handleProjection.tryFind(h, proj)), false)

  const done = handleProjection.complete(h, 'Terminal', proj)
  assert.equal(done.ok, true)
  proj = done.value
  assert.equal(handleProjection.recordSealsBlogger(handleProjection.tryFind(h, proj)), true)

  const retired = handleProjection.retire(h, proj)
  assert.equal(retired.ok, true)
  proj = retired.value
  assert.equal(handleProjection.recordSealsBlogger(handleProjection.tryFind(h, proj)), true)
})

test('BLOGGER_RUNTIME_Sealed_ignores_material_until_reactivate', () => {
  const sealed = bloggerRuntime.forceSeal(bloggerRuntime.idle)
  assert.equal(bloggerRuntime.stateOf(sealed), 'Sealed')
  assert.equal(bloggerRuntime.blocksNewRequest(true, sealed), true)
  assert.equal(bloggerRuntime.blocksNewRequest(false, sealed), true)

  const ignored = bloggerRuntime.onMaterial(sealed, ctx())
  assert.equal(ignored.ok, true)
  assert.equal(ignored.decision, 'Ignore')
  assert.equal(bloggerRuntime.stateOf(ignored.state), 'Sealed')

  const live = bloggerRuntime.onReactivate(sealed)
  assert.equal(bloggerRuntime.stateOf(live), 'Idle')
  assert.equal(bloggerRuntime.reactivatedOf(live), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, live), false)

  const started = bloggerRuntime.onMaterial(live, ctx())
  assert.equal(started.ok, true)
  assert.equal(started.decision, 'Start')
  assert.equal(bloggerRuntime.stateOf(started.state), 'InFlight')
})

test('BLOGGER_RUNTIME_durable_seal_blocks_idle_unless_reactivated', () => {
  const idle = bloggerRuntime.idle
  assert.equal(bloggerRuntime.blocksNewRequest(false, idle), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, idle), true)

  const reactivated = bloggerRuntime.onReactivate(idle)
  assert.equal(bloggerRuntime.blocksNewRequest(true, reactivated), false)
})

test('BLOGGER_RUNTIME_InFlight_survives_onSeal_flag_clear_only', () => {
  const started = bloggerRuntime.onMaterial(bloggerRuntime.idle, ctx())
  assert.equal(started.decision, 'Start')
  const sealedSoft = bloggerRuntime.onSeal(started.state)
  assert.equal(bloggerRuntime.stateOf(sealedSoft), 'InFlight')
  assert.equal(bloggerRuntime.reactivatedOf(sealedSoft), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, sealedSoft), false)
})
