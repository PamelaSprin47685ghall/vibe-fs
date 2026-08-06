// Blogger seal after join/return + Authority Root reactivation.
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
} from '../support/domain.mjs'

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

test('HANDLE_lifecycle_Abandoned_seals_blogger', () => {
  let proj = handleProjection.empty
  const h = handleId.agent('agent-ab')
  const child = sessionId('ses-child-ab')
  const linked = handleProjection.link(h, child, 'fast-coder', roles.Coder, proj)
  assert.equal(linked.ok, true)
  proj = linked.value
  const abandoned = handleProjection.abandon(h, 'ParentCancelled', proj)
  assert.equal(abandoned.ok, true)
  assert.equal(handleProjection.recordSealsBlogger(handleProjection.tryFind(h, abandoned.value)), true)
})

test('BLOGGER_RUNTIME_cell_has_no_sealed_mirror_durable_is_truth', () => {
  // DSL-003: handle seal is a durable journal fact read at every entry
  // (blocksNew in the Coordinator), so the cell has no Sealed case. forceSeal
  // only closes the in-memory drain window; a cell mirror could only duplicate
  // — and drift from — the journal.
  const idle = bloggerRuntime.forceSeal(bloggerRuntime.idle)
  assert.equal(bloggerRuntime.stateOf(idle), 'Idle', 'forceSeal leaves the state alone')
  assert.equal(bloggerRuntime.blocksNewRequest(true, idle), true, 'durable seal blocks')
  assert.equal(bloggerRuntime.blocksNewRequest(false, idle), false, 'no mirror: unsealed durable unblocks')

  const live = bloggerRuntime.onReactivate(idle, authorityRoot('root-r1'))
  assert.equal(bloggerRuntime.stateOf(live), 'Idle')
  assert.equal(bloggerRuntime.reactivatedOf(live), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, live), false, 'drain window lets the cycle through')
  // The drain window holds an unforgeable DrainPermit (module-private
  // constructor): no caller can mint an open window for an arbitrary root, so
  // the recorded root is guaranteed by the type, not asserted by value.
  assert.equal(bloggerRuntime.reactivatedOf(live), true)

  const started = bloggerRuntime.onMaterial(false, live, ctx())
  assert.equal(started.ok, true)
  assert.equal(started.decision, 'Start')
  assert.equal(bloggerRuntime.stateOf(started.state), 'InFlight')
})

test('BLOGGER_RUNTIME_durable_seal_blocks_idle_unless_reactivated', () => {
  const idle = bloggerRuntime.idle
  assert.equal(bloggerRuntime.blocksNewRequest(false, idle), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, idle), true)

  const reactivated = bloggerRuntime.onReactivate(idle, authorityRoot('root-r1'))
  assert.equal(bloggerRuntime.stateOf(reactivated), 'Idle')
  assert.equal(bloggerRuntime.blocksNewRequest(true, reactivated), false)
})

test('BLOGGER_RUNTIME_parked_waiter_survives_onReactivate_so_offer_not_start', () => {
  // Authority Root on main must not demote the waiter fact. Idle + parked
  // waiter = Start (new prompt_async) only when nothing waits; with a waiter
  // the material Offer-resumes it (ENFORCER-050). The cell itself stays Idle —
  // the waiter is the host dictionary's physical fact.
  const reactivated = bloggerRuntime.onReactivate(bloggerRuntime.idle, authorityRoot('root-r1'))
  assert.equal(bloggerRuntime.stateOf(reactivated), 'Idle')
  assert.equal(bloggerRuntime.reactivatedOf(reactivated), true)

  const offered = bloggerRuntime.onMaterial(true, reactivated, ctx())
  assert.equal(offered.ok, true)
  assert.equal(offered.decision, 'Offer')
  assert.equal(bloggerRuntime.stateOf(offered.state), 'Idle')
})

test('BLOGGER_RUNTIME_reactivated_catchup_forceSeal_blocks_again', () => {
  // Durable handle sealed + DrainWindow.Open lets one drain window through;
  // once caught up, host forceSeal must permanently re-block.
  const reactivated = bloggerRuntime.onReactivate(bloggerRuntime.forceSeal(bloggerRuntime.idle), authorityRoot('root-r1'))
  assert.equal(bloggerRuntime.blocksNewRequest(true, reactivated), false)

  const started = bloggerRuntime.onMaterial(false, reactivated, ctx())
  assert.equal(started.decision, 'Start')
  const committed = bloggerRuntime.onCycleCommitted(started.state)
  assert.equal(committed.ok, true)
  assert.equal(bloggerRuntime.stateOf(committed.state), 'Idle')
  // Flag still true after commit — host must forceSeal when tryRefresh returns None.
  assert.equal(bloggerRuntime.reactivatedOf(committed.state), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, committed.state), false)

  const sealed = bloggerRuntime.forceSeal(committed.state)
  assert.equal(bloggerRuntime.stateOf(sealed), 'Idle', 'forceSeal only closes the drain window')
  assert.equal(bloggerRuntime.reactivatedOf(sealed), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, sealed), true, 'durable seal re-blocks after catch-up')
})
