import assert from 'node:assert/strict'
import test from 'node:test'

import { readOwnerProjectInventoryV1 } from '../../../scripts/checks/owner-projects.mjs'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const temporal = await import('../../../dist/Process/Surface.js')
const deadline = await import('../../../dist/Process/DeadlineSurface.js')
const START_MS = Date.parse('2000-01-01T00:00:00Z')
const settle = () => Promise.resolve()

const locality = (inventory, id) => {
  const matches = inventory.localities.filter((candidate) => candidate.id === id)
  assert.equal(matches.length, 1, `${id} must resolve to one production locality`)
  return matches[0]
}

const sourcePaths = (entry) => entry.sources.map(({ implementationPath }) => implementationPath)
const consumersOf = (inventory, id) => inventory.localities
  .filter(({ references }) => references.includes(id))
  .map(({ id: consumerId }) => consumerId)
  .sort()

test('WHAT[TIME-008] production inventory separates contracts adapter verification and representation', () => {
  const inventory = readOwnerProjectInventoryV1()
  const capability = locality(inventory, 'foundation-temporal-contract')
  const deadlineContract = locality(inventory, 'process-deadline-contract')
  const projection = locality(inventory, 'execution-session-sessionstartedatprojection')
  const nodeAdapter = locality(inventory, 'process-node-timing-adapter')
  const virtualImplementation = locality(inventory, 'process-virtual-timing')
  const representation = locality(inventory, 'foundation-temporal')

  assert.equal(capability.kind, 'contract')
  assert.equal(deadlineContract.kind, 'contract')
  assert.equal(projection.kind, 'contract')
  assert.equal(nodeAdapter.kind, 'adapter')
  assert.equal(virtualImplementation.kind, 'runtime')
  assert.equal(representation.kind, 'composition')
  assert.deepEqual(sourcePaths(capability), ['src/Wanxiangshu/Foundation/Temporal.fs'])
  assert.deepEqual(sourcePaths(deadlineContract), ['src/Wanxiangshu/Process/Deadline.fs'])
  assert.deepEqual(sourcePaths(projection), ['src/Wanxiangshu/Execution/Session/SessionStartedAtProjection.fs'])
  assert.deepEqual(sourcePaths(nodeAdapter), ['src/Wanxiangshu/Process/NodeTiming.fs'])
  assert.deepEqual(sourcePaths(virtualImplementation), ['src/Wanxiangshu/Process/VirtualTiming.fs'])
  assert.deepEqual(sourcePaths(representation), [
    'src/Wanxiangshu/Process/DeadlineSurface.fs',
    'src/Wanxiangshu/Process/Surface.fs',
  ])
  assert.deepEqual(nodeAdapter.references, [
    'foundation-taskresult',
    'foundation-temporal-contract',
  ])
  assert.deepEqual(virtualImplementation.references, [
    'foundation-taskresult',
    'foundation-temporal-contract',
  ])
  for (const id of [
    'execution-session-sessionstartedatprojection',
    'foundation-temporal-contract',
    'process-deadline-contract',
    'process-node-timing-adapter',
    'process-virtual-timing',
  ]) assert.ok(representation.references.includes(id), `representation needs ${id}`)

  assert.deepEqual(consumersOf(inventory, 'foundation-temporal-contract'), [
    'delegation-fork-runtime',
    'delegation-host-adapter',
    'delegation-recovery-runtime',
    'enforcer-guidance-tip',
    'execution-session-sessionstartedatledger',
    'execution-session-wait-proof-surface',
    'execution-session-wait-runtime',
    'foundation-temporal',
    'opencode-host-hostsignalbootstrap',
    'opencode-host-messagevisibility',
    'process-node-timing-adapter',
    'process-virtual-timing',
    'verification-eventstorewritersurface',
  ])
  assert.deepEqual(consumersOf(inventory, 'process-deadline-contract'), [
    'foundation-temporal',
    'process-processrequest',
  ])
  assert.deepEqual(consumersOf(inventory, 'execution-session-sessionstartedatprojection'), [
    'composition-durable-projection',
    'execution-session-sessionstartedatledger',
    'foundation-temporal',
    'strength-persistence-durabilityport',
  ])
  assert.deepEqual(consumersOf(inventory, 'process-node-timing-adapter'), [
    'delegation-fork-runtime',
    'delegation-host-adapter',
    'delegation-runtime-surface',
    'execution-delegation-hostturnobservedsurface',
    'foundation-temporal',
    'git-integrationgate',
    'mission-review-reviewfactfold',
    'opencode-host-hostsignalbootstrap',
    'process-largegatesurface',
    'process-processrequest',
  ])
  assert.deepEqual(consumersOf(inventory, 'process-virtual-timing'), [
    'foundation-temporal',
    'verification-eventstorewritersurface',
  ])
  assert.deepEqual(consumersOf(inventory, 'foundation-temporal'), [])
})

test('WHAT[TIME-008] clock and timer capabilities are opaque instance-bound values', async () => {
  const firstClock = temporal.createVirtualClock()
  const secondClock = temporal.createVirtualClock()
  const firstTimer = temporal.createVirtualTimer()
  const secondTimer = temporal.createVirtualTimer()
  const firstHandle = temporal.timerDelay(firstTimer, 10)
  const secondHandle = temporal.timerDelay(secondTimer, 10)
  let firstFired = 0
  let secondFired = 0

  assertOpaque(firstClock, 'clock capability')
  assertOpaque(firstTimer, 'timer capability')
  assertOpaque(firstHandle, 'deadline capability')
  temporal.timerAwait(firstHandle).then(() => {
    firstFired += 1
  })
  temporal.timerAwait(secondHandle).then(() => {
    secondFired += 1
  })

  temporal.clockAdvanceMs(firstClock, 10)
  temporal.timerAdvance(firstTimer, 10)
  await settle()

  assert.equal(Number(temporal.clockNowMs(firstClock)), START_MS + 10)
  assert.equal(Number(temporal.clockNowMs(secondClock)), START_MS)
  assert.equal(firstFired, 1)
  assert.equal(secondFired, 0)

  temporal.timerCancel(secondHandle)
  temporal.timerDispose(firstTimer)
  temporal.timerDispose(secondTimer)
})

test('WHAT[TIME-008] Deadline is immutable and decided only by explicit clock input', () => {
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)
  assertOpaque(value, 'deadline')

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', value), true)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:01Z', value), 4000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:04Z', value), false)
})

test('WHAT[TIME-008] Node capability construction cannot mutate virtual time', async () => {
  const virtualClock = temporal.createVirtualClock()
  const virtualTimer = temporal.createVirtualTimer()
  const virtualHandle = temporal.timerDelay(virtualTimer, 0)
  let virtualFired = 0
  temporal.timerAwait(virtualHandle).then(() => {
    virtualFired += 1
  })

  const nodeClock = temporal.createNodeClock()
  const nodeTimer = temporal.createNodeTimer()
  await settle()

  assertOpaque(nodeClock, 'Node clock capability')
  assertOpaque(nodeTimer, 'Node timer capability')
  assert.equal(Number(temporal.clockNowMs(virtualClock)), START_MS)
  assert.equal(virtualFired, 0, 'constructing Node capabilities must not advance a virtual timer')

  temporal.timerAdvance(virtualTimer, 0)
  await settle()
  assert.equal(virtualFired, 1)
  temporal.nodeTimerDispose(nodeTimer)
  temporal.timerDispose(virtualTimer)
})
