import assert from 'node:assert/strict'
import { writeFileSync } from 'node:fs'
import * as sync from '../../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'

const [directory, scenario, receiptPath] = process.argv.slice(2)
const owner = `guard-owner-${scenario}`
const isLateAcceptance = scenario === 'late-guard' || scenario === 'fallback-late-guard'
const isFallback = scenario === 'fallback-old-guard' || scenario === 'fallback-late-guard'
const pendingAfterTurn = promise => Promise.race([
  promise.then(() => 'settled'),
  new Promise(resolve => setImmediate(() => resolve('pending'))),
])

for (const name of ['createForGuardRecovery', 'observeGuardDelta', 'awaitGuardInterrupt',
  'observeExactGuardAbort', 'closeGuardRecovery', 'confirmManagedPromptPhysical']) {
  assert.equal(typeof sync[name], 'function', `missing guard fixture capability: ${name}`)
}

const runtime = await sync.createForGuardRecovery(directory, [{ sessionId: owner, agent: 'manager' }])
try {
  const first = sync.startObserved(runtime, owner, 'FIRST-GUARD-CHARGE')
  const firstCompletion = sync.observedCompletion(first)
  await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
  assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 0, dispatch.admittedWithReceipt('guard-initial-receipt')), true)
  const initial = await sync.confirmManagedPromptPhysical(runtime, owner, 'Engineer', 0, 'guard-initial-physical')
  assert.equal(initial.ok, true, JSON.stringify(initial))
  assert.equal(initial.origin, 'AgentOwnerRoot')
  assert.equal((await sync.observedAdmission(first)).physicalUserMessageId, initial.physicalUserMessageId)

  const sourceRun = 'guard-source-provider-run'
  const rawGuardDelta = {
    type: 'message.part.delta',
    properties: { sessionID: initial.sessionId, messageID: sourceRun, partID: 'guard-stream-part',
      field: 'text', delta: ' retry'.repeat(2000) },
  }
  await sync.observeGuardDelta(runtime, rawGuardDelta)
  await sync.awaitGuardInterrupt(runtime, initial.sessionId, sourceRun)
  assert.equal(sync.promptCount(runtime, owner, 'Engineer'), 1, 'stream anomaly interrupts but cannot continue before reconcile')

  const aborted = sync.observeExactGuardAbort(runtime, initial.sessionId, initial.physicalUserMessageId,
    initial.authorityRootUserMessageId, sourceRun)
  await sync.awaitPromptCount(runtime, owner, 'Engineer', 2)
  const guardPrompt = sync.promptIdentity(runtime, owner, 'Engineer', 1)
  assert.notEqual(guardPrompt.promptKey, sync.promptIdentity(runtime, owner, 'Engineer', 0).promptKey)
  assert.equal(guardPrompt.sessionId, initial.sessionId)
  assert.equal(sync.promptOrigin(runtime, owner, 'Engineer', 1), 'DegenerationGuard')
  assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 1, dispatch.admittedWithReceipt('guard-host-receipt')), true)
  await aborted
  assert.equal(sync.promptClaimState(runtime, owner, 'Engineer', 1).kind, 'Pending')
  assert.equal(await pendingAfterTurn(firstCompletion), 'pending', 'a guard receipt does not complete the invocation')
  await sync.observeGuardDelta(runtime, rawGuardDelta)
  assert.equal(sync.promptCount(runtime, owner, 'Engineer'), 2, 'one armed source cannot send a second continuation')

  let successor = null
  if (!isLateAcceptance) {
    successor = await sync.confirmManagedPromptPhysical(runtime, owner, 'Engineer', 1, 'guard-successor-physical')
    assert.equal(successor.ok, true, JSON.stringify(successor))
    assert.equal(successor.origin, 'DegenerationGuard')
    assert.notEqual(successor.physicalUserMessageId, initial.physicalUserMessageId)
    assert.equal(successor.authorityRootUserMessageId, initial.authorityRootUserMessageId)
    assert.equal(await sync.settleExactTerminal(runtime, successor.sessionId, successor.physicalUserMessageId,
      successor.authorityRootUserMessageId, 'guard-successor-provider-run', 'GUARD-FORMAL-ANSWER', ''), true)
  } else {
    assert.equal(await sync.settleExactTerminal(runtime, initial.sessionId, initial.physicalUserMessageId,
      initial.authorityRootUserMessageId, 'guard-initial-completed-run', 'FIRST-FORMAL-ANSWER', ''), true)
    assert.equal(sync.promptClaimState(runtime, owner, 'Engineer', 1).kind, 'Pending', 'call completion cannot abandon a pending actual guard send')
  }
  const firstResult = await firstCompletion
  assert.equal(firstResult.ok, true)

  if (scenario !== 'own-guard') {
    const frontier = sync.handoffFrontier(runtime, owner, 'Engineer')
    const second = sync.startObserved(runtime, owner, 'SECOND-GUARD-CHARGE')
    const secondCompletion = sync.observedCompletion(second)
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 3)
    assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 2, dispatch.admittedWithReceipt('guard-second-receipt')), true)
    const secondAccepted = await sync.confirmManagedPromptPhysical(runtime, owner, 'Engineer', 2, 'guard-second-assignment-physical')
    assert.equal(secondAccepted.ok, true, JSON.stringify(secondAccepted))
    assert.equal(secondAccepted.origin, 'ManagedDelegationAssignment')
    assert.equal(secondAccepted.sessionId, initial.sessionId)
    assert.equal(secondAccepted.authorityRootUserMessageId, initial.authorityRootUserMessageId)
    assert.equal((await sync.observedAdmission(second)).physicalUserMessageId, secondAccepted.physicalUserMessageId)

    if (isLateAcceptance) {
      successor = await sync.confirmManagedPromptPhysical(runtime, owner, 'Engineer', 1, 'guard-late-successor-physical')
      assert.equal(successor.ok, true, JSON.stringify(successor))
      assert.equal(successor.origin, 'DegenerationGuard')
      assert.equal(successor.authorityRootUserMessageId, secondAccepted.authorityRootUserMessageId)
      assert.equal(sync.promptClaimState(runtime, owner, 'Engineer', 1).kind, 'Accepted')
    }
    const staleText = isLateAcceptance ? 'LATE-GUARD-ANSWER' : 'GUARD-FORMAL-ANSWER'
    const staleAccepted = isFallback
      ? sync.settleExactFallback(runtime, successor.sessionId, successor.physicalUserMessageId,
        successor.authorityRootUserMessageId, 'guard-successor-provider-run', staleText)
      : await sync.settleExactTerminal(runtime, successor.sessionId, successor.physicalUserMessageId,
        successor.authorityRootUserMessageId, 'guard-successor-provider-run', staleText, '')
    assert.equal(staleAccepted, false, 'an old actual guard acceptance cannot settle the new same-root invocation')
    assert.equal(await pendingAfterTurn(secondCompletion), 'pending')
    assert.deepEqual(sync.handoffFrontier(runtime, owner, 'Engineer'), frontier)

    const secondSourceRun = 'guard-second-source-provider-run'
    await sync.observeGuardDelta(runtime, {
      type: 'message.part.delta',
      properties: { sessionID: secondAccepted.sessionId, messageID: secondSourceRun, partID: 'guard-second-stream-part',
        field: 'text', delta: ' retry'.repeat(2000) },
    })
    await sync.awaitGuardInterrupt(runtime, secondAccepted.sessionId, secondSourceRun)
    const secondAborted = sync.observeExactGuardAbort(runtime, secondAccepted.sessionId,
      secondAccepted.physicalUserMessageId, secondAccepted.authorityRootUserMessageId, secondSourceRun)
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 4)
    const secondGuardPrompt = sync.promptIdentity(runtime, owner, 'Engineer', 3)
    assert.notEqual(secondGuardPrompt.promptKey, guardPrompt.promptKey)
    assert.equal(sync.promptOrigin(runtime, owner, 'Engineer', 3), 'DegenerationGuard')
    assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 3, dispatch.admittedWithReceipt('guard-second-successor-receipt')), true)
    await secondAborted
    const secondSuccessor = await sync.confirmManagedPromptPhysical(runtime, owner, 'Engineer', 3, 'guard-second-successor-physical')
    assert.equal(secondSuccessor.ok, true, JSON.stringify(secondSuccessor))
    assert.equal(secondSuccessor.origin, 'DegenerationGuard')
    assert.notEqual(secondSuccessor.physicalUserMessageId, secondAccepted.physicalUserMessageId)
    assert.equal(secondSuccessor.authorityRootUserMessageId, secondAccepted.authorityRootUserMessageId)
    assert.equal(await sync.settleExactTerminal(runtime, secondSuccessor.sessionId, secondSuccessor.physicalUserMessageId,
      secondSuccessor.authorityRootUserMessageId, 'guard-second-provider-run', 'SECOND-GUARD-ANSWER', ''), true)
    const result = await secondCompletion
    assert.equal(result.ok, true)
    assert.equal(result.value.physicalUserMessageId, secondSuccessor.physicalUserMessageId)
    assert.equal(result.value.providerRun, 'guard-second-provider-run')
    assert.equal(result.value.formalText, 'SECOND-GUARD-ANSWER')
  } else {
    assert.deepEqual(firstResult, { ok: true, value: {
      sessionId: successor.sessionId,
      physicalUserMessageId: successor.physicalUserMessageId,
      authorityRootUserMessageId: successor.authorityRootUserMessageId,
      providerRun: 'guard-successor-provider-run',
      formalText: 'GUARD-FORMAL-ANSWER',
    } })
  }
  assert.equal(sync.childCount(runtime), 1)
  assert.equal(sync.terminalListenerCount(runtime), 0)
  writeFileSync(receiptPath, JSON.stringify({ pid: process.pid, scenario, initial, guardPrompt, successor,
    firstResult, promptCount: sync.promptCount(runtime, owner, 'Engineer'), childCount: sync.childCount(runtime) }))
} finally {
  await sync.closeGuardRecovery(runtime)
}
