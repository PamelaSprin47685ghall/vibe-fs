// Blogger seal after join/return + Authority Root reactivation.
// Drain = physical slot (setDrainWindow / isDrainOpen / openDrain); busy = hasFlight.
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  bloggerRuntime,
  bloggerRequestContext,
  authorityRoot,
  handleProjection,
  handleId,
  sessionId,
  roles,
  parkedTransform,
} from '../support/domain.mjs'

const KEY = 'ses-blog'
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
  const linked = handleProjection.link(h, child, 'fast-coder', roles.of('Coder'), proj)
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

test('HANDLE_lifecycle_Abandoned_seals_blogger', () => {
  let proj = handleProjection.empty
  const h = handleId.agent('agent-ab')
  const child = sessionId('ses-child-ab')
  const linked = handleProjection.link(h, child, 'fast-coder', roles.of('Coder'), proj)
  assert.equal(linked.ok, true)
  proj = linked.value
  const abandoned = handleProjection.abandon(h, 'ParentCancelled', proj)
  assert.equal(abandoned.ok, true)
  assert.equal(handleProjection.recordSealsBlogger(handleProjection.tryFind(h, abandoned.value)), true)
})

test('BLOGGER_RUNTIME_cell_has_no_sealed_mirror_durable_is_truth', () => {
  // DSL-003: handle seal is a durable journal fact read at every entry
  // (blocksNew in the Coordinator). forceSeal only closes the physical drain
  // window; busy is physical HasFlight.
  // blocksNewRequest is pure over (durableSealed, hasFlight, drainOpen).
  const scope = parkedTransform.scope()
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.closedDrain())
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), false, 'forceSeal leaves drain closed')
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, false), true, 'durable seal blocks')
  assert.equal(bloggerRuntime.blocksNewRequest(false, false, false), false, 'no mirror: unsealed durable unblocks')
  assert.equal(
    bloggerRuntime.blocksNewRequest(true, true, false),
    false,
    'hasFlight does not block via this gate (SkippedInFlight path)',
  )

  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.openDrain(authorityRoot('root-r1')))
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)
  assert.equal(
    bloggerRuntime.blocksNewRequest(true, false, true),
    false,
    'drain window lets the cycle through',
  )
  // openDrain mints an unforgeable DrainPermit (module-private constructor).
  assert.equal(bloggerRuntime.drainOpenOf(parkedTransform.getDrainWindow(scope, KEY)), true)

  assert.equal(bloggerRuntime.decideMaterial(false, false, ctx()), 'Start')
  parkedTransform.setCurrentRequest(scope, KEY, ctx())
  assert.equal(parkedTransform.hasFlight(scope, KEY), true)
})

test('BLOGGER_RUNTIME_durable_seal_blocks_idle_unless_reactivated', () => {
  assert.equal(bloggerRuntime.blocksNewRequest(false, false, false), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, false), true)

  const scope = parkedTransform.scope()
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.openDrain(authorityRoot('root-r1')))
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, true), false)
})

test('BLOGGER_RUNTIME_parked_waiter_survives_reactivate_so_offer_not_start', () => {
  // Authority Root on main must not demote the waiter fact. Idle + parked
  // waiter = Start only when nothing waits; with a waiter the material
  // Offer-resumes it (ENFORCER-050). Drain open does not register flight.
  const scope = parkedTransform.scope()
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.openDrain(authorityRoot('root-r1')))
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)

  assert.equal(bloggerRuntime.decideMaterial(true, false, ctx()), 'Offer')
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
})

test('BLOGGER_RUNTIME_reactivated_catchup_forceSeal_blocks_again', () => {
  // Durable handle sealed + DrainWindow.Open lets one drain window through;
  // once caught up, host forceSeal must permanently re-block.
  // Gate is pure booleans: hasFlight + drainOpen.
  const scope = parkedTransform.scope()
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.closedDrain())
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.openDrain(authorityRoot('root-r1')))
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, true), false)

  assert.equal(bloggerRuntime.decideMaterial(false, false, ctx()), 'Start')
  parkedTransform.setCurrentRequest(scope, KEY, ctx())
  assert.equal(parkedTransform.hasFlight(scope, KEY), true)
  parkedTransform.clearCurrentRequest(scope, KEY)
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  // Flag still true after commit — host must forceSeal when tryRefresh returns None.
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, true), false)

  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.closedDrain())
  assert.equal(parkedTransform.hasFlight(scope, KEY), false, 'forceSeal only closes the drain window')
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, false), true, 'durable seal re-blocks after catch-up')
})
