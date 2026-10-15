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
  const commonDir = join(root, '.Herdchestra')
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

test('WHAT[work-039] resources appearing during prompt admission block retirement and later settle without another prompt', async () => {
  await withWorkspace(async (_store, root, commonDir) => {
    const workId = 'delivery-late-resource'
    const content = '# accepted while a resource became active\n'
    const active = authorize(root, commonDir, workId, content)
    let busy = false
    let queries = 0
    let prompts = 0
    const hasBlocker = () => { queries += 1; return busy }
    const result = await surface.executeDeliver(
      commonDir, root, workId, active, 'resource-run', 'resource-call', hasBlocker,
      {
        create: async () => 'exec-resource',
        prompt: async () => {
          prompts += 1
          busy = true
          return { kind: 'Started' }
        },
        discard: async () => assert.fail('an accepted execution must not be discarded'),
      },
      40n,
    )
    assert.equal(result.ok, false)
    assert.match(result.error, /live resources/)
    assert.ok(queries >= 2, 'retirement must recheck resources after the asynchronous prompt')
    assert.equal(host.inspected(commonDir, workId).PendingDelivery, true)
    const before = host.inspect(commonDir, workId).revision
    const blocked = surface.reconcileBoundExecution(commonDir, workId, 'exec-resource', content, ['owned-live-process'])
    assert.equal(blocked.ok, false)
    assert.equal(host.inspect(commonDir, workId).revision, before)
    busy = false
    const settled = surface.reconcileBoundExecution(commonDir, workId, 'exec-resource', content, [])
    assert.equal(settled.ok, true)
    assert.equal(settled.delivered, true)
    assert.equal(host.inspected(commonDir, workId).Delivered, true)
    assert.equal(prompts, 1)
  })
})

test('WHAT[work-039] committed reservation starts exactly once, sends the plan byte-for-byte and records the session id', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-23a'
    const content = '# Plan with tricky bytes\n\n- line with\ttab\n- trailing newline\n'
    const active = authorize(root, commonDir, workId, content)
    let createCalls = 0
    let promptCalls = 0

    const result = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'delivery-run',
      'delivery-call',
      false,
      {
        create: async (operationId, agent) => {
          createCalls += 1
          assert.ok(operationId)
          assert.equal(agent, 'manager')
          return 'exec-verbatim-42'
        },
        prompt: async (sessionId, agent, prompt) => {
          promptCalls += 1
          assert.equal(sessionId, 'exec-verbatim-42')
          assert.equal(agent, 'manager')
          assert.equal(prompt, content)
          assert.equal(host.inspect(commonDir, workId).pendingDelivery.execSessionId, sessionId)
          return { kind: 'Started' }
        },
        discard: async () => {
          assert.fail('a started execution session must not be discarded')
        },
      },
      40n,
    )

    assert.equal(result.ok, true, result.error)
    assert.equal(result.delivered, true)
    assert.equal(result.execSessionId, 'exec-verbatim-42')
    assert.equal(createCalls, 1)
    assert.equal(promptCalls, 1)
    const work = host.inspected(commonDir, workId)
    assert.equal(work.ActiveId, undefined)
    assert.equal(work.Delivered, true)
  })
})

test('WHAT[work-039] unknown spawn acceptance stays pending and replay never starts a second execution', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-23b'
    const active = authorize(root, commonDir, workId, '# Plan content')
    let createCalls = 0
    let promptCalls = 0

    const unknown = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'unknown-run',
      'unknown-call',
      false,
      {
        create: async () => {
          createCalls += 1
          return 'exec-unknown'
        },
        prompt: async () => {
          promptCalls += 1
          return { kind: 'Unknown', reason: 'transport closed after the request may have landed' }
        },
        discard: async () => {
          assert.fail('unknown prompt acceptance must not discard its bound session')
        },
      },
      40n,
    )

    assert.equal(unknown.ok, false)
    assert.match(unknown.error, /outcome is unknown/)
    assert.equal(createCalls, 1)
    assert.equal(promptCalls, 1)
    assert.equal(host.inspected(commonDir, workId).PendingDelivery, true)
    const pending = host.inspect(commonDir, workId).pendingDelivery
    assert.equal(pending.kind, 'ObserveExecution')
    assert.ok(pending.operationId)
    assert.equal(pending.execSessionId, 'exec-unknown')

    let replayEffects = 0
    const replay = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'unknown-run',
      'unknown-call',
      false,
      {
        create: async () => { replayEffects += 1; return 'must-not-create' },
        prompt: async () => { replayEffects += 1; return { kind: 'Started' } },
        discard: async () => { replayEffects += 1 },
      },
      40n,
    )
    assert.equal(replay.ok, false)
    assert.match(replay.error, /remains pending/)
    assert.equal(replayEffects, 0)
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

    let rejectedReplayEffects = 0
    const rejectedReplay = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'unknown-run',
      'unknown-call',
      false,
      {
        create: async () => { rejectedReplayEffects += 1; return 'must-not-create' },
        prompt: async () => { rejectedReplayEffects += 1; return { kind: 'Started' } },
        discard: async () => { rejectedReplayEffects += 1 },
      },
      40n,
    )
    assert.equal(rejectedReplay.ok, false)
    assert.match(rejectedReplay.error, /proved the callback failed before any external effect/)
    assert.equal(rejectedReplayEffects, 0)

    let secondCreates = 0
    let secondPrompts = 0
    const retry = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'retry-run',
      'retry-call',
      false,
      {
        create: async () => {
          secondCreates += 1
          return 'exec-after-reconcile'
        },
        prompt: async () => {
          secondPrompts += 1
          return { kind: 'Started' }
        },
        discard: async () => {
          assert.fail('the accepted retry must not be discarded')
        },
      },
      50n,
    )
    assert.equal(retry.ok, true, retry.error)
    assert.equal(retry.delivered, true)
    assert.equal(retry.execSessionId, 'exec-after-reconcile')
    assert.equal(secondCreates, 1)
    assert.equal(secondPrompts, 1)
  })
})

test('WHAT[work-039] a definite prompt refusal discards the bound empty session before NotStarted unlocks a new operation', async () => {
  await withWorkspace(async (_store, root, commonDir) => {
    const workId = 'work-23c'
    const active = authorize(root, commonDir, workId, '# Plan content')
    const order = []

    const refused = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'refused-run',
      'refused-call',
      false,
      {
        create: async () => {
          order.push('create')
          return 'exec-refused'
        },
        prompt: async sessionId => {
          order.push('prompt')
          assert.equal(host.inspect(commonDir, workId).pendingDelivery.execSessionId, sessionId)
          return { kind: 'NotStarted', reason: 'Host refused the first prompt before acceptance' }
        },
        discard: async sessionId => {
          order.push('discard')
          assert.equal(sessionId, 'exec-refused')
          assert.equal(host.inspect(commonDir, workId).pendingDelivery.execSessionId, sessionId)
        },
      },
      40n,
    )

    assert.equal(refused.ok, false)
    assert.match(refused.error, /refused the first prompt/)
    assert.deepEqual(order, ['create', 'prompt', 'discard'])
    assert.equal(host.inspected(commonDir, workId).PendingDelivery, false)
    assert.equal(host.inspected(commonDir, workId).Delivered, false)

    let retryCreates = 0
    const retry = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'refused-retry-run',
      'refused-retry-call',
      false,
      {
        create: async () => {
          retryCreates += 1
          return 'exec-after-refusal'
        },
        prompt: async () => ({ kind: 'Started' }),
        discard: async () => assert.fail('accepted retry must not be discarded'),
      },
      50n,
    )
    assert.equal(retry.ok, true, retry.error)
    assert.equal(retryCreates, 1)
  })
})

test('WHAT[work-039] failed cleanup after a definite prompt refusal keeps the operation pending', async () => {
  await withWorkspace(async (_store, root, commonDir) => {
    const workId = 'work-23d'
    const active = authorize(root, commonDir, workId, '# Plan content')

    const result = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'cleanup-run',
      'cleanup-call',
      false,
      {
        create: async () => 'exec-cleanup-failed',
        prompt: async () => ({ kind: 'NotStarted', reason: 'prompt definitely refused' }),
        discard: async () => {
          throw new Error('session abort failed')
        },
      },
      40n,
    )

    assert.equal(result.ok, false)
    assert.match(result.error, /could not be discarded/)
    const pending = host.inspected(commonDir, workId)
    assert.equal(pending.PendingDelivery, true)
    assert.equal(pending.PendingExecSessionId, 'exec-cleanup-failed')
    assert.equal(pending.Delivered, false)
  })
})

test('WHAT[work-039] unknown session creation leaves an unbound operation and replay never creates again', async () => {
  await withWorkspace(async (_store, root, commonDir) => {
    const workId = 'work-23e'
    const active = authorize(root, commonDir, workId, '# Plan content')
    let creates = 0

    const first = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'create-unknown-run',
      'create-unknown-call',
      false,
      {
        create: async () => {
          creates += 1
          throw new Error('connection closed after create request')
        },
        prompt: async () => assert.fail('unbound creation may not prompt'),
        discard: async () => assert.fail('unknown creation may not guess a session to discard'),
      },
      40n,
    )
    assert.equal(first.ok, false)
    assert.match(first.error, /creation outcome is unknown/)
    const pending = host.inspected(commonDir, workId)
    assert.equal(pending.PendingDelivery, true)
    assert.equal(pending.PendingExecSessionId, undefined)
    assert.equal(creates, 1)

    const replay = await surface.executeDeliver(
      commonDir,
      root,
      workId,
      active,
      'create-unknown-run',
      'create-unknown-call',
      false,
      {
        create: async () => {
          creates += 1
          return 'must-not-create'
        },
        prompt: async () => assert.fail('replay may not prompt'),
        discard: async () => assert.fail('replay may not discard'),
      },
      40n,
    )
    assert.equal(replay.ok, false)
    assert.match(replay.error, /remains pending/)
    assert.equal(creates, 1)
  })
})
