import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname
const main = (toml = 'delta-1') => runtime.main({ toml })

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_160_material_event_resumes_park_with_typed_context', async () => {
  const scope = runtime.scope()
  const waiter = runtime.park(scope, 'ses-blogger')

  assert.equal(runtime.hasParked(scope, 'ses-blogger'), true)
  assert.equal(runtime.offerParked(scope, 'ses-blogger', main('delta-1')), 'Delivered')

  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.kind, 'Main')
  assert.equal(wake.context.toml, 'delta-1')
  assert.equal(runtime.hasParked(scope, 'ses-blogger'), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_162_cancel_is_an_explicit_event', async () => {
  const scope = runtime.scope()
  const waiter = runtime.park(scope, 'ses-blogger')

  runtime.cancelParked(scope, 'ses-blogger')

  assert.deepEqual(await waiter, { kind: 'Cancelled', context: null })
  assert.equal(runtime.hasParked(scope, 'ses-blogger'), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_050_offer_first_is_delivered_by_the_next_await', async () => {
  const scope = runtime.scope()

  assert.equal(runtime.offerParked(scope, 'ses-blogger', main('staged')), 'Staged')
  assert.equal(runtime.hasParked(scope, 'ses-blogger'), false)

  const wake = await runtime.park(scope, 'ses-blogger')
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'staged')
  assert.equal(runtime.consumeStaged(scope, 'ses-blogger'), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_160_two_awaits_share_one_material_event', async () => {
  const scope = runtime.scope()
  const first = runtime.park(scope, 'ses-blogger')
  const second = runtime.park(scope, 'ses-blogger')

  assert.equal(runtime.offerParked(scope, 'ses-blogger', main('one-event')), 'Delivered')

  const [a, b] = await Promise.all([first, second])
  assert.equal(a.kind, 'MaterialAvailable')
  assert.equal(a.context.toml, 'one-event')
  assert.deepEqual(b, a)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_162_dispose_cancels_every_material_wait', async () => {
  const scope = runtime.scope()
  const a = runtime.park(scope, 'ses-a')
  const b = runtime.park(scope, 'ses-b')

  runtime.dispose(scope)

  assert.equal((await a).kind, 'Cancelled')
  assert.equal((await b).kind, 'Cancelled')
  assert.equal(runtime.hasParked(scope, 'ses-a'), false)
  assert.equal(runtime.hasParked(scope, 'ses-b'), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_162_cancel_drops_staged_material_without_touching_flight', async () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  runtime.offerParked(scope, key, main('staged'))
  runtime.claimCurrentRequest(scope, key, main('already-flying'))
  runtime.cancelParked(scope, key)

  assert.equal(runtime.consumeStaged(scope, key), null)
  assert.equal(runtime.hasFlight(scope, key), true)
  assert.equal(runtime.peekCurrentRequest(scope, key)?.toml, 'already-flying')
  runtime.releaseCurrentRequest(scope, key, 'request-main')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_seal_cancels_wait_without_revoking_existing_flight', async () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'
  const waiter = runtime.park(scope, key)

  runtime.claimCurrentRequest(scope, key, main('seal-in-flight'))
  runtime.setDrainWindow(scope, key, runtime.openDrain('root-1'))
  runtime.sealRuntime(scope, key)

  assert.equal((await waiter).kind, 'Cancelled')
  assert.equal(runtime.isDrainOpen(scope, key), false)
  assert.equal(runtime.hasFlight(scope, key), true)
  assert.equal(runtime.peekCurrentRequest(scope, key)?.toml, 'seal-in-flight')
  runtime.releaseCurrentRequest(scope, key, 'request-main')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_161_sessions_are_independent', async () => {
  const scope = runtime.scope()
  const a = runtime.park(scope, 'ses-a')
  const b = runtime.park(scope, 'ses-b')

  runtime.offerParked(scope, 'ses-b', main('b'))
  assert.equal((await b).context.toml, 'b')
  assert.equal(runtime.hasParked(scope, 'ses-a'), true)

  runtime.cancelParked(scope, 'ses-a')
  assert.equal((await a).kind, 'Cancelled')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_CurrentRequest_is_physical_flight_ownership', () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  assert.equal(runtime.hasFlight(scope, key), false)
  runtime.claimCurrentRequest(scope, key, main('coverage-delta'))
  assert.equal(runtime.hasFlight(scope, key), true)
  assert.equal(runtime.tryGetFlight(scope, key)?.toml, 'coverage-delta')
  runtime.releaseCurrentRequest(scope, key, 'request-main')
  assert.equal(runtime.hasFlight(scope, key), false)
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_flight_claim_never_overwrites_another_request', () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  assert.equal(
    runtime.claimCurrentRequest(scope, key, runtime.main({ requestId: 'req-a', toml: 'first' })),
    'Claimed',
  )
  assert.equal(
    runtime.claimCurrentRequest(scope, key, runtime.main({ requestId: 'req-b', toml: 'second' })),
    'Conflict:req-a',
  )
  assert.equal(runtime.currentRequest(scope, key).toml, 'first')
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_stale_release_cannot_clear_a_newer_owner', () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  assert.equal(
    runtime.claimCurrentRequest(scope, key, runtime.main({ requestId: 'req-a', toml: 'first' })),
    'Claimed',
  )
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-b'), 'Conflict:req-a')
  assert.equal(runtime.currentRequest(scope, key).toml, 'first')
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Released')
  assert.equal(runtime.hasFlight(scope, key), false)
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_materialization_admission_is_cross_instance_single_flight', async () => {
  const firstScope = runtime.scope()
  const secondScope = runtime.scope()
  const key = 'ses-blogger'
  const first = await runtime.acquireMaterialization(firstScope, key)
  let secondAcquired = false

  const second = runtime.acquireMaterialization(secondScope, key).then((lease) => {
    secondAcquired = true
    return lease
  })

  await Promise.resolve()
  assert.equal(secondAcquired, false)

  runtime.releaseMaterialization(first)
  const secondLease = await second
  assert.equal(secondAcquired, true)
  runtime.releaseMaterialization(secondLease)
})

test('WHAT[CONTEXT-COMPRESSION-023] CTX_023_park_has_no_clock_or_timeout_dependency', () => {
  const parked = readFileSync(
    `${ROOT}src/Wanxiangshu/Context/Companion/Blogger/Runtime/ParkedTransform.fs`,
    'utf8',
  )
  const scope = readFileSync(
    `${ROOT}src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs`,
    'utf8',
  )

  for (const [name, text] of [['ParkedTransform', parked], ['PluginScope', scope]]) {
    assert.doesNotMatch(text, /TimeSpan|ITimerPort|nodeTimerPort|\.Delay\b|deadline|timeout/i, `${name} must be time-independent`)
  }
})
