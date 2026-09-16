import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const {
  createRuntime,
  acquireExecutionAdmission,
  executionAdmissionTarget,
  commitExecutionAdmission,
  tryLease,
  releasePhysicalExecution,
  snapshotOccupied,
} = routing

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const key = (value) => `${value.model}|${value.reasoning}`
const acquireManaged = async (runtime, sessionId, physicalUserMessageId, role, participant, lenderSessionId = null) => {
  const acquisition = await acquireExecutionAdmission(
    runtime,
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    lenderSessionId,
  )
  if (acquisition.kind !== 'Acquired') return { kind: acquisition.kind, target: null }

  const projected = executionAdmissionTarget(runtime, acquisition.lease)
  const observed = {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target: projected,
  }
  const settlement = commitExecutionAdmission(runtime, acquisition.lease, observed)
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  return { kind: 'Acquired', target: projected }
}
const acquireTarget = async (...args) => {
  const outcome = await acquireManaged(...args)
  assert.equal(outcome.kind, 'Acquired')
  return outcome.target
}

const source = async (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[EMR-007] EMR_007_execution_release_is_idempotent_and_wakes_waiters_once', async () => {
  const runtime = createRuntime((_role, running) => running.length === 0 ? target('provider/one') : null)
  await acquireTarget(runtime, 'holder', 'msg-holder', 'coder', 'alice')
  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiter', 'inspector', 'bob')

  releasePhysicalExecution(runtime, 'holder', 'msg-holder')
  const acquired = await waiting
  assert.equal(acquired.model, 'provider/one')
  assert.equal(snapshotOccupied(runtime).length, 1)

  releasePhysicalExecution(runtime, 'holder', 'msg-holder')
  assert.equal(snapshotOccupied(runtime).length, 1, 'second release cannot remove somebody else\'s execution')
})

test('WHAT[EMR-007] EMR_007_late_terminal_for_superseded_physical_execution_cannot_release_current_lease', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  await acquireTarget(runtime, 'reused-session', 'msg-old', 'coder', 'alice')
  await acquireTarget(runtime, 'reused-session', 'msg-current', 'inspector', 'alice')

  releasePhysicalExecution(runtime, 'reused-session', 'msg-old')
  assert.equal(
    key(tryLease(runtime, 'reused-session', 'msg-current', 'inspector', 'alice', null)),
    'provider/inspector|none',
    'late exact terminal evidence for the old physical material must not touch the current lease',
  )
  assert.equal(snapshotOccupied(runtime).length, 1)

  releasePhysicalExecution(runtime, 'reused-session', 'msg-current')
  assert.equal(snapshotOccupied(runtime).length, 0, 'the matching physical terminal releases exactly one occurrence')
})

test('WHAT[EMR-007] EMR_007_exact_terminal_identity_releases_capacity_not_coarse_idle_or_business_completion', async () => {
  const recovery = await source('src/Wanxiangshu/OpenCode/Host/SessionRecoveryHost.fs')
  const codec = await source('src/Wanxiangshu/OpenCode/Codec/HostEventCodec.fs')
  const ordinary = await source('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')

  assert.match(codec, /tryDecodePhysicalExecutionEnd/)
  assert.match(codec, /isMessageUpdated\s*=\s*not \(isNull raw\) && HostEventEnvelope\.eventTypeOf raw = "message\.updated"/)
  assert.match(codec, /info\?parentID/)
  assert.match(recovery, /let release \(key: ChatExecutionKey\) =[\s\S]*ModelRouting\.releasePhysicalExecution key\.SessionId key\.PhysicalUserMessageId/)
  assert.match(recovery, /PhysicalReconciliationRequest\.ReleaseTerminalResource\(key, _, _\) ->[\s\S]{0,160}release key/)
  assert.doesNotMatch(recovery, /SessionIdle sessionId[\s\S]{0,260}ModelRouting\.releaseExecution sessionId/)
  assert.doesNotMatch(recovery, /AttemptAborted sessionId[\s\S]{0,260}ModelRouting\.releaseExecution sessionId/)
  assert.doesNotMatch(ordinary, /ModelRouting\.(releaseExecution|releaseSession)/,
    'application completion/finality must not own physical capacity release')
})

test('WHAT[EMR-007] EMR_007_chat_message_closes_the_old_idle_window_before_model_admission', async () => {
  const host = await source('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const chatHook = host.slice(host.indexOf('let chatMessageHook ='), host.indexOf('let cancelSignals'))
  const barrier = host.slice(host.indexOf('let observePhysicalAdmission'), host.indexOf('let chatMessageHook ='))
  const managedAdmission = host.slice(host.indexOf('let admitManagedChatMessage'), host.indexOf('let chatMessageHook ='))
  const classifiedAdmission = host.slice(host.indexOf('let continueClassifiedChatMessage'), host.indexOf('let chatMessageHook ='))
  const revoke = barrier.indexOf('Quiescence.ObservePhysicalUserMessage')
  const invoke = chatHook.indexOf('observePhysicalAdmission output sessionId physicalId')
  const continueClassified = chatHook.indexOf('continueClassifiedChatMessage intent output')
  const classify = chatHook.indexOf('PromptIngress.resolveDecision journal decoded')
  const acquire = managedAdmission.indexOf('ChatAdmissionTransaction.execute')

  assert.notEqual(revoke, -1, 'chat.message must close the preceding idle-send window')
  assert.notEqual(invoke, -1, 'chat.message must invoke the named physical admission barrier')
  assert.match(classifiedAdmission, /PendingPromptIntent _, Some durable, Some createTransaction ->\s*admitManagedChatMessage durable createTransaction intent output/)
  assert.notEqual(continueClassified, -1, 'chat.message must continue through the classified admission owner')
  assert.notEqual(classify, -1)
  assert.notEqual(acquire, -1)
  assert.ok(classify < invoke, 'pure exact intent resolution must precede the physical ingress barrier')
  assert.ok(invoke < continueClassified, 'physical ingress barrier must run before managed admission begins')
})
