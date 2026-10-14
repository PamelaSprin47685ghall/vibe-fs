import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as planning from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const surface = planning.PlanningSurface ?? planning
const host = lifecycle.LifecycleSurface ?? lifecycle

const withWorkspace = async body => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-039-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root, commonDir)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const accepted = receipt => {
  assert.ok(['Committed', 'Replayed', 'Observed'].includes(receipt.Status), JSON.stringify(receipt))
  return receipt
}

const authorize = (root, commonDir, workId, content) => {
  const execute = (requestId, command) => accepted(host.execute(commonDir, workId, requestId, command))
  execute('open', {
    type: 'Open',
    root,
    sequence: ['S1', 'S2', 'S3'],
    ownerId: workId,
    openingCursor: 0,
  })

  for (let stage = 1; stage <= 3; stage++) {
    const active = host.inspected(commonDir, workId).ActiveId
    execute(`review-${stage}`, {
      type: 'Review',
      incumbencyId: active,
      reviewerId: 'reviewer',
      findings: [],
    })
    if (stage < 3) {
      execute(`relay-${stage}`, {
        type: 'Deliver',
        incumbencyId: active,
        providerRunId: `relay-run-${stage}`,
        toolCallId: `relay-call-${stage}`,
        cursor: stage * 10,
        blockers: [],
        nextOwnerId: workId,
      })
    }
  }

  const written = surface.rewritePlanAtomic(root, workId, content)
  assert.equal(written.ok, true, written.error)
  const work = host.inspected(commonDir, workId)
  assert.equal(work.DeliveryAuthorized, true)
  return work.ActiveId
}

test('WHAT[work-039] committed reservation starts exactly once, sends the plan byte-for-byte and records the session id', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-23a'
    const content = '# Plan with tricky bytes\n\n- line with\ttab\n- trailing newline\n'
    const active = authorize(root, commonDir, workId, content)
    let spawnCalls = 0

    const result = await surface.executeDeliver(
      store,
      root,
      workId,
      active,
      'delivery-run',
      'delivery-call',
      false,
      async (agent, prompt) => {
        spawnCalls += 1
        assert.equal(agent, 'manager')
        assert.equal(prompt, content)
        return 'exec-verbatim-42'
      },
      40n,
    )

    assert.equal(result.ok, true, result.error)
    assert.equal(result.delivered, true)
    assert.equal(result.execSessionId, 'exec-verbatim-42')
    assert.equal(spawnCalls, 1)
    const work = host.inspected(commonDir, workId)
    assert.equal(work.ActiveId, undefined)
    assert.equal(work.Delivered, true)
  })
})

test('WHAT[work-039] unknown spawn acceptance stays pending and replay never starts a second execution', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-23b'
    const active = authorize(root, commonDir, workId, '# Plan content')
    let firstCalls = 0

    const unknown = await surface.executeDeliver(
      store,
      root,
      workId,
      active,
      'unknown-run',
      'unknown-call',
      false,
      async () => {
        firstCalls += 1
        throw new Error('transport closed after the request may have landed')
      },
      40n,
    )

    assert.equal(unknown.ok, false)
    assert.match(unknown.error, /outcome is unknown/)
    assert.equal(firstCalls, 1)
    assert.equal(host.inspected(commonDir, workId).PendingDelivery, true)
    const pending = host.inspect(commonDir, workId).pendingDelivery
    assert.equal(pending.kind, 'ObserveExecution')
    assert.ok(pending.operationId)

    let replayStarts = 0
    const replay = await surface.executeDeliver(
      store,
      root,
      workId,
      active,
      'unknown-run',
      'unknown-call',
      false,
      async () => {
        replayStarts += 1
        return 'must-not-start'
      },
      40n,
    )
    assert.equal(replay.ok, true)
    assert.equal(replay.message, 'ObserveExecution')
    assert.equal(replayStarts, 0)
    assert.equal(host.inspected(commonDir, workId).PendingDelivery, true)

    const refused = host.execute(commonDir, workId, 'reconcile-not-started', {
      type: 'ResolveDelivery',
      operationId: pending.operationId,
      outcome: 'NotStarted',
      reason: 'test observer proved the callback failed before any external effect',
    })
    assert.equal(refused.Status, 'Committed')
    assert.equal(refused.Kind, 'ExecutionRejected')
    assert.equal(host.inspected(commonDir, workId).PendingDelivery, false)

    let secondCalls = 0
    const retry = await surface.executeDeliver(
      store,
      root,
      workId,
      active,
      'retry-run',
      'retry-call',
      false,
      async () => {
        secondCalls += 1
        return 'exec-after-reconcile'
      },
      50n,
    )
    assert.equal(retry.ok, true, retry.error)
    assert.equal(retry.delivered, true)
    assert.equal(retry.execSessionId, 'exec-after-reconcile')
    assert.equal(secondCalls, 1)
  })
})
