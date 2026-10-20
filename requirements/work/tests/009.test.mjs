import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { deliverOnce } from './support/contract.mjs'
import { withReview, scores } from './support/plugin.mjs'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as scopeMod from '../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle
const ownershipHost = life
const verifyOwnership =
  scopeMod.verifyOwnershipRetirementContract ||
  (scopeMod.ToolRuntimeScopeSurface && scopeMod.ToolRuntimeScopeSurface.verifyOwnershipRetirementContract)

test('WHAT[work-009] new pipe ownership blocks the current family while fixed DevOps keeps its own scope across retirement', async () => {
  const result = await scopeMod.ToolRuntimeScopeSurface.verifyProcessRetirementContract()
  assert.equal(result.admittedEngineer, true)
  assert.equal(result.admittedDevOps, true)
  assert.equal(result.before.filter(value => value.startsWith('tool-owned-work:')).length, 1)
  assert.deepEqual(result.afterEngineer, [])
  assert.equal(result.devopsOwn.filter(value => value.startsWith('tool-owned-work:')).length, 1)
})

test('WHAT[work-009] a live owned resource blocks deliver without retiring', () => {
  const blocked = deliverOnce({ sequence: ['S1', 'S2', 'S3'], blockers: ['pty'], deliveryAuthorized: false, head: 1, projection: 'S1', incumbencyId: 'i' })
  assert.equal(blocked.written[0].type, 'RetirementBlocked')
  assert.equal(blocked.written.some((event) => event.type === 'IncumbencyRetired'), false)
  assert.equal(blocked.finished, false)
})

test('WHAT[work-009] the second deliver reports the live blocker instead of reporting an end', async () => {
  await withReview(async ({ hooks, runtime, session, execute }) => {
    await execute(scores('REVISE'))
    const forked = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'Ada', charge: 'implement' },
      { sessionID: session, callID: 'call-fork', messageID: 'run-fork', agent: 'manager' },
    )
    assert.match(String(forked), /carries this charge now|现已接下这项托付/)
    // The first call only confirms; Algorithm E4 does not read the blocker set.
    assert.match(
      await hooks.tool.deliver.execute({}, { sessionID: session, callID: 'deliver-confirm', messageID: 'run-confirm', agent: 'manager' }),
      /confirmation_required = true/,
    )
    const blocked = await hooks.tool.deliver.execute(
      {},
      { sessionID: session, callID: 'deliver-call', messageID: 'run-retire', agent: 'manager' },
    )
    // Algorithm E8: the blocker is durable and the call reports an unfinished
    // tenure with exactly what to close, so the runner may deliver again.
    assert.match(blocked, /finished = false/)
    assert.match(blocked, /Close or join exactly those resources/)
    assert.doesNotMatch(blocked, /Finished\. Do not continue work\./)
    const after = life.inspected(await journalSurface.JournalSurface_initializeStateRoot(runtime.directory), session)
    assert.equal(Array.from(after.Retirements ?? []).length, 0)
    assert.equal(after.Delivered, false)
    assert.notEqual(after.BlockerDigest ?? '', '')
  }, { session: 'blocked-second-deliver' })
})

test('WHAT[work-009] ToolRuntimeScope ownership tracks tool-owned-work and PTY leases, keeps blockers across Cancel/Drain, and clears only on Ended', async () => {
  assert.equal(typeof verifyOwnership, 'function', 'verifyOwnershipRetirementContract must be exported')
  const res = await verifyOwnership({
    managerSessionId: 'manager-road-009',
    incumbentId: 'tenure-009',
    devopsSessionId: 'devops-009',
    engineerSessionId: 'engineer-009',
  })

  assert.equal(res.admittedWork, true)
  assert.equal(
    res.blockersWithWorkAndPty.some((b) => b.startsWith('tool-owned-work:')),
    true,
    'active tool-owned-work lease must appear in RetirementBlockersFor',
  )
  assert.equal(
    res.blockersWithWorkAndPty.includes(`fork:engineer-009:pty:${res.engineerPtyId}`),
    true,
    'active engineer PTY lease must appear in RetirementBlockersFor',
  )
  assert.equal(
    res.blockersWithWorkAndPty.some((b) => b.includes(res.devopsPtyId)),
    false,
    'fixed DevOps PTY belongs to DevOps scope and must not block Manager tenure retirement',
  )

  // Cancel request does not decrement or clear unsettled blockers
  assert.deepEqual(
    res.blockersAfterCancelRequest,
    res.blockersWithWorkAndPty,
    'requesting cancellation on a PTY lease must not remove it from blockers before physical exit',
  )

  // Tenure freeze uses Ownership Drain and clears on Unfreeze
  assert.equal(res.frozeFirst, true)
  assert.equal(res.frozenWhileDraining, true)
  assert.equal(res.frozenAfterUnfreeze, false)

  // Ending owned work removes only the tool-owned-work lease
  assert.equal(
    res.blockersAfterWorkEnded.some((b) => b.startsWith('tool-owned-work:')),
    false,
    'settled tool-owned-work lease must be removed from blockers',
  )
  assert.equal(
    res.blockersAfterWorkEnded.includes(`fork:engineer-009:pty:${res.engineerPtyId}`),
    true,
    'unsettled engineer PTY must remain a blocker until its own Ended',
  )

  // Ending engineer PTY clears the remaining tenure blocker while DevOps PTY survives
  assert.deepEqual(res.blockersAfterEngineerPtyEnded, [])
  assert.deepEqual(res.devopsPtysSurviving, [res.devopsPtyId])
})

test('WHAT[work-009] durable ownership host commits Reserve before granting start permission and retains blockers across Cancel and Drain', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-ownership-009-'))
  const commonDir = join(root, '.Herdchestra')
  mkdirSync(commonDir, { recursive: true })
  try {
    const reg = 'work-009'
    const openWork = life.executeOwnershipDurable(commonDir, reg, 'open-root', {
      type: 'OpenScope',
      scopeId: reg,
      parentId: null,
    })
    assert.equal(openWork.Status, 'Committed')

    const reserve = life.executeOwnershipDurable(commonDir, reg, 'reserve-pty-1', {
      type: 'Reserve',
      resourceId: 'pty-1',
      scopeId: reg,
      kind: 'process',
    })
    const lease = life.mayStartResource(reserve)
    assert.ok(lease, 'first Committed Reserve must yield a start lease')

    const replayReserve = life.executeOwnershipDurable(commonDir, reg, 'reserve-pty-1', {
      type: 'Reserve',
      resourceId: 'pty-1',
      scopeId: reg,
      kind: 'process',
    })
    assert.equal(replayReserve.Status, 'Replayed')
    assert.equal(life.mayStartResource(replayReserve), undefined, 'Replayed Reserve must not grant a second physical start')

    const started = life.executeOwnershipDurable(commonDir, reg, 'start-pty-1', {
      type: 'Started',
      lease: { registryId: lease.RegistryId, resourceId: lease.ResourceId, token: lease.Token },
      binding: { externalId: 'pid-101', incarnationId: 'inc-1' },
    })
    assert.equal(started.Status, 'Committed')

    // Cancel and Drain must not clear unsettled blockers
    life.executeOwnershipDurable(commonDir, reg, 'cancel-pty-1', {
      type: 'Cancel',
      lease: { registryId: lease.RegistryId, resourceId: lease.ResourceId, token: lease.Token },
    })
    life.executeOwnershipDurable(commonDir, reg, 'drain-root', {
      type: 'Drain',
      scopeId: reg,
    })
    const blockersWhileDraining = life.ownershipBlockersDurable(commonDir, reg, reg)
    assert.equal(blockersWhileDraining.length, 1)
    assert.equal(blockersWhileDraining[0].CancellationRequested, true)

    // Physical Ended clears the blocker
    const ended = life.executeOwnershipDurable(commonDir, reg, 'end-pty-1', {
      type: 'Ended',
      lease: { registryId: lease.RegistryId, resourceId: lease.ResourceId, token: lease.Token },
      binding: { externalId: 'pid-101', incarnationId: 'inc-1' },
      outcome: 'Cancelled',
    })
    assert.equal(ended.Status, 'Committed')
    assert.equal(life.ownershipBlockersDurable(commonDir, reg, reg).length, 0)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[work-009] a process-local reservation never grants durable launch permission', () => {
  const registry = ownershipHost.createOwnership('local-work-009', [])
  ownershipHost.executeOwnershipInMemory(registry, 'open-local', {
    type: 'OpenScope', scopeId: 'callbacks', parentId: null,
  })
  const receipt = ownershipHost.executeOwnershipInMemory(registry, 'reserve-local', {
    type: 'Reserve', resourceId: 'callback-1', scopeId: 'callbacks', kind: 'tool',
  })

  assert.equal(receipt.Status, 'Applied')
  assert.equal(ownershipHost.mayStartResource(receipt), undefined)
  assert.ok(ownershipHost.mayStartInMemoryResource(receipt))
  const replay = ownershipHost.executeOwnershipInMemory(registry, 'reserve-local', {
    type: 'Reserve', resourceId: 'callback-1', scopeId: 'callbacks', kind: 'tool',
  })
  assert.equal(ownershipHost.mayStartInMemoryResource(replay), undefined)
})

test('WHAT[work-009] an unknown ownership scope is an error rather than proof of no resources', () => {
  const registry = ownershipHost.createOwnership('query-work-009', [])
  assert.throws(() => ownershipHost.ownershipBlockersInMemory(registry, 'missing'), /scope/i)
  assert.throws(() => ownershipHost.ownershipScopePhaseInMemory(registry, 'missing'), /scope/i)
  ownershipHost.executeOwnershipInMemory(registry, 'open-known', {
    type: 'OpenScope', scopeId: 'known', parentId: null,
  })
  assert.deepEqual(Array.from(ownershipHost.ownershipBlockersInMemory(registry, 'known')), [])
  assert.equal(ownershipHost.ownershipScopePhaseInMemory(registry, 'known'), 'Open')
})

test('WHAT[work-009] durable blocker queries refuse a missing owner and a missing scope', () => {
  assert.throws(() => ownershipHost.ownershipBlockersDurable('', 'registry', 'scope'), /common|directory/i)
  const root = mkdtempSync(join(tmpdir(), 'wxs-ownership-query-009-'))
  try {
    assert.throws(() => ownershipHost.ownershipBlockersDurable(root, '', 'scope'), /registry/i)
    assert.throws(() => ownershipHost.ownershipBlockersDurable(root, 'registry', 'scope'), /scope/i)
    assert.equal(ownershipHost.tryOwnershipBlockersDurable(root, 'registry', 'scope'), undefined)
    life.executeOwnershipDurable(root, 'registry', 'open-known', {
      type: 'OpenScope', scopeId: 'known', parentId: null,
    })
    assert.throws(() => ownershipHost.ownershipBlockersDurable(root, 'registry', 'missing'), /scope/i)
    assert.throws(() => ownershipHost.tryOwnershipBlockersDurable(root, 'registry', 'missing'), /scope/i)
    assert.deepEqual(Array.from(ownershipHost.ownershipBlockersDurable(root, 'registry', 'known')), [])
    assert.deepEqual(Array.from(ownershipHost.tryOwnershipBlockersDurable(root, 'registry', 'known')), [])
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
