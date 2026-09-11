import assert from 'node:assert/strict'
import test from 'node:test'

import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'
import { assertEffectIsInjected, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const temporal = await import('../../../dist/Process/Surface.js')
const deadline = await import('../../../dist/Process/DeadlineSurface.js')
const START_MS = Date.parse('2000-01-01T00:00:00Z')
const settle = () => Promise.resolve()

const requireShard = (projects, shardId) => {
  const matches = [...projects.values()].filter((candidate) => candidate.shard === shardId)
  assert.equal(matches.length, 1, `${shardId} must resolve to exactly one production compile shard`)
  return matches[0]
}

const relSources = (project) => project.implementationFiles.map((p) => path.relative(ROOT, p)).sort()
const refShards = (project, projects) => project.references.map((refPath) => projects.get(refPath).shard).sort()
const consumersOf = (projects, shardId) => [...projects.values()]
  .filter((candidate) => refShards(candidate, projects).includes(shardId))
  .map((candidate) => candidate.shard)
  .sort()

test('WHAT[TIME-008] production inventory separates contracts adapter verification and representation', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = subsystemInventory.projects

  const capability = requireShard(projects, 'foundation-temporal-contract')
  const deadlineContract = requireShard(projects, 'process-deadline-contract')
  const projection = requireShard(projects, 'execution-session-sessionstartedatprojection')
  const nodeAdapter = requireShard(projects, 'process-node-timing-adapter')
  const virtualImplementation = requireShard(projects, 'process-virtual-timing')
  const representation = requireShard(projects, 'foundation-temporal')

  assert.equal(capability.subsystem, 'session-lifecycle')
  assert.equal(deadlineContract.subsystem, 'session-lifecycle')
  assert.equal(projection.subsystem, 'session-lifecycle')
  assert.equal(nodeAdapter.subsystem, 'session-lifecycle')
  assert.equal(virtualImplementation.subsystem, 'session-lifecycle')
  assert.equal(representation.subsystem, 'process')

  assert.deepEqual(relSources(capability), ['src/Wanxiangshu/Foundation/Temporal.fs'])
  assert.deepEqual(relSources(deadlineContract), ['src/Wanxiangshu/Process/Deadline.fs'])
  assert.deepEqual(relSources(projection), ['src/Wanxiangshu/Execution/Session/SessionStartedAtProjection.fs'])
  assert.deepEqual(relSources(nodeAdapter), ['src/Wanxiangshu/Process/NodeTiming.fs'])
  assert.deepEqual(relSources(virtualImplementation), ['src/Wanxiangshu/Process/VirtualTiming.fs'])
  assert.deepEqual(relSources(representation), [
    'src/Wanxiangshu/Process/DeadlineSurface.fs',
    'src/Wanxiangshu/Process/Surface.fs',
  ])

  assert.deepEqual(refShards(nodeAdapter, projects), [
    'async-support',
    'foundation-temporal-contract',
  ])
  assert.deepEqual(refShards(virtualImplementation, projects), [
    'async-support',
    'foundation-temporal-contract',
  ])
  for (const id of [
    'execution-session-sessionstartedatprojection',
    'foundation-temporal-contract',
    'process-deadline-contract',
    'process-node-timing-adapter',
    'process-virtual-timing',
  ]) assert.ok(refShards(representation, projects).includes(id), `representation needs ${id}`)

  assert.deepEqual(consumersOf(projects, 'foundation-temporal-contract'), [
    'delegation-fork-runtime',
    'delegation-host-adapter',
    'delegation-recovery-runtime',
    'enforcer-guidance-tip',
    'execution-session-sessionstartedatledger',
    'execution-session-wait-proof-surface',
    'execution-session-wait-runtime',
    'foundation-temporal',
    'opencode-host-messagevisibility',
    'plugin-composition',
    'process-node-timing-adapter',
    'process-virtual-timing',
    'verification-eventstorewritersurface',
  ])
  assert.deepEqual(consumersOf(projects, 'process-deadline-contract'), [
    'foundation-temporal',
    'process-processrequest',
  ])
  assert.deepEqual(consumersOf(projects, 'execution-session-sessionstartedatprojection'), [
    'composition-durable-fold',
    'composition-durable-projection',
    'execution-session-sessionstartedatledger',
    'foundation-temporal',
  ])
  assert.deepEqual(consumersOf(projects, 'process-node-timing-adapter'), [
    'delegation-runtime-surface',
    'execution-delegation-hostturnobservedsurface',
    'foundation-temporal',
    'opencode-host-hostsignalbootstrap',
    'plugin-composition',
    'process-largegatesurface',
    'process-processrequest',
  ])
  assert.deepEqual(consumersOf(projects, 'process-virtual-timing'), [
    'foundation-temporal',
    'verification-eventstorewritersurface',
  ])
  assert.deepEqual(consumersOf(projects, 'foundation-temporal'), [])
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

test('WHAT[TIME-008] temporal contracts exclude Node adapters mutable timers and SessionStartedAt projection', () => {
  assertPureContract()
  assertEffectIsInjected('timer')
})
