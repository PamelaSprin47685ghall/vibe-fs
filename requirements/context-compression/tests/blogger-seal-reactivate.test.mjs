// Moved from tests/unit/enforcer/blogger-seal-reactivate.test.mjs (cutover Wave 2a); owner: context-compression.
//
// Blogger seal after join/return + Authority Root reactivation.
// Drain = physical slot (setDrainWindow / isDrainOpen / openDrain); busy = hasFlight.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as bloggerRuntime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as parkedTransform from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as handle from '../../../dist/Execution/Delegation/Handle/Surface.js'

const KEY = 'ses-blog'
const authorityRoot = (value) => value
const ctx = () => bloggerRuntime.main({
  requestId: 'request-main',
  mainSession: 'ses-main',
  bloggerSession: KEY,
  toml: '[[new_work_to_record]]\nuser = "work"',
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'digest-1',
  frameEpoch: 0,
  deltaDigest: 'delta-1',
  observedEpoch: 0,
})

test('WHAT[CONTEXT-COMPRESSION-018] HANDLE_lifecycle_CompletedAwaitingJoin_and_Retired_seal_blogger', () => {
  const completed = handle.scenario('complete')
  assert.equal(completed.ok, true)
  assert.equal(completed.record.lifecycle, 'CompletedAwaitingJoin')

  const retired = handle.scenario('retire')
  assert.equal(retired.ok, true)
  assert.equal(retired.record.lifecycle, 'Retired')
})

test('WHAT[CONTEXT-COMPRESSION-018] HANDLE_lifecycle_Abandoned_seals_blogger', () => {
  const abandoned = handle.scenario('abandon')
  assert.equal(abandoned.ok, true)
  assert.equal(abandoned.record.lifecycle, 'Abandoned')
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_cell_has_no_sealed_mirror_durable_is_truth', () => {
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
  // The drain permit remains opaque; the owner exposes only its semantic state.
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)

  assert.equal(bloggerRuntime.decideMaterial(false, false, false, ctx()), 'Start')
  parkedTransform.claimCurrentRequest(scope, KEY, ctx())
  assert.equal(parkedTransform.hasFlight(scope, KEY), true)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_durable_seal_blocks_idle_unless_reactivated', () => {
  assert.equal(bloggerRuntime.blocksNewRequest(false, false, false), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, false), true)

  const scope = parkedTransform.scope()
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.openDrain(authorityRoot('root-r1')))
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, true), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_parked_waiter_survives_reactivate_so_offer_not_start', () => {
  // Authority Root on main must not demote the waiter fact. Idle + parked
  // waiter = Start only when nothing waits; with a waiter the material
  // Offer-resumes it (ENFORCER-050). Drain open does not register flight.
  const scope = parkedTransform.scope()
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.openDrain(authorityRoot('root-r1')))
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)

  assert.equal(bloggerRuntime.decideMaterial(false, true, false, ctx()), 'Offer')
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_reactivated_catchup_forceSeal_blocks_again', () => {
  // Durable handle sealed + DrainWindow.Open lets one drain window through;
  // once caught up, host forceSeal must permanently re-block.
  // Gate is pure booleans: hasFlight + drainOpen.
  const scope = parkedTransform.scope()
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.closedDrain())
  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.openDrain(authorityRoot('root-r1')))
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, true), false)

  assert.equal(bloggerRuntime.decideMaterial(false, false, false, ctx()), 'Start')
  parkedTransform.claimCurrentRequest(scope, KEY, ctx())
  assert.equal(parkedTransform.hasFlight(scope, KEY), true)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  // Flag still true after commit — host must forceSeal when tryRefresh returns None.
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), true)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, true), false)

  parkedTransform.setDrainWindow(scope, KEY, bloggerRuntime.closedDrain())
  assert.equal(parkedTransform.hasFlight(scope, KEY), false, 'forceSeal only closes the drain window')
  assert.equal(parkedTransform.isDrainOpen(scope, KEY), false)
  assert.equal(bloggerRuntime.blocksNewRequest(true, false, false), true, 'durable seal re-blocks after catch-up')
})
