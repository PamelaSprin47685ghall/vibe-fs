import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')
const fissionHost = await import('../../../dist/OpenCode/Host/FissionHostSurface.js')

const root = resolve(import.meta.dirname, '../../..')
const read = (p) => readFileSync(resolve(root, p), 'utf8')

const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] convergence requires all lane records and all completion deliveries', () => {
  const bundle = [0, 1, 2].reduce(
    (state, lane) => mustOk(fission.workBundleAdd(lane, `ref-${lane}`, state)).bundle,
    fission.workBundleEmpty,
  )
  let delivery = fission.deliveryEmpty(3)
  for (const lane of [0, 1, 2]) delivery = mustOk(fission.deliveryMark('pre-child', lane, delivery)).delivery

  assert.equal(fission.convergenceReady(3, ['pre-child'], bundle, delivery), true)
  const incomplete = mustOk(fission.workBundleAdd(0, 'ref-0', fission.workBundleEmpty)).bundle
  assert.equal(fission.convergenceReady(3, ['pre-child'], incomplete, delivery), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] ring successor wraps and forwards past already-closed lanes to the next live present', () => {
  assert.equal(fission.ringSuccessor(4, 0, [0, 1, 2, 3]), null, 'all lanes closed leaves the group finalizer holding the bundle')
  assert.equal(fission.ringSuccessor(4, 1, [0, 1, 3]), 2, 'lane 1 forwards directly to live successor lane 2')
  assert.equal(fission.ringSuccessor(4, 1, [0, 1, 2]), 3, 'closed successor lane 2 forwards mechanically to lane 3')
  assert.equal(fission.ringSuccessor(4, 3, [1, 2, 3]), 0, 'lane N-1 wraps to lane 0')
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] observeLaneTurn and OrdinaryTurnWorkflow absorb Fission-replaced owner turns without sending continuations', async () => {
  const owner = 'retired-owner-workflow'
  fission.markSilentInterrupt(owner)

  try {
    const observed = await fissionHost.observeReplacedOwner(owner)
    assertJsData(observed, 'Fission host observation')
    assert.deepEqual(observed, {
      handled: true,
      continuationSent: false,
      terminalNotified: false,
    })
  } finally {
    fission.clearOwner(owner)
  }
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] exact Fission terminal bridge reconciles a completed lane even when session idle is missing', async () => {
  const observed = await fissionHost.missingIdleTerminalBridgeScenario()
  assertJsData(observed, 'Fission missing-idle terminal bridge observation')
  assert.equal(observed.outcome, 'TurnCompleted')
  assert.equal(observed.physicalUserMessageId, 'fission-missing-idle-user')
  assert.equal(observed.providerRun, 'fission-missing-idle-run')
  assert.ok(observed.snapshotReads >= 1)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] Host convergence performs ring takeover before reporting the old logical owner', () => {
  const host = read('src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs')
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const facts = read('src/Wanxiangshu/Execution/Fission/Facts.fs')
  const projection = read('src/Wanxiangshu/Execution/Fission/Projection.fs')

  assert.match(host, /FissionFact\.FissionTakeoverClaimed/)
  assert.match(host, /SendContinuation[\s\S]{0,1200}?ContinuationKind\.FissionHandoff[\s\S]{0,600}?AwaitMode\.Await/)
  assert.doesNotMatch(
    host,
    /TaskCompletionSource<PhysicalUserMessageId>|acceptedPhysicalId\.Task/,
    'lane-terminal observation must not block waiting for a future chat.message physical id',
  )
  assert.match(facts, /FissionTakeoverClaimed[\s\S]{0,500}?PromptKey:\s*PromptKey/)
  assert.doesNotMatch(host, /acceptedDispatchForPromptKey|takeoverPhysicalMessage|TakeoverTurnDisposition/)
  assert.match(host, /turn\.SessionId\s*=\s*takeover\.LaneSessionId/)
  assert.match(host, /CompletedTurnClassifier\.partsSessionText turn\.Parts/)
  assert.match(host, /SessionId\s*=\s*group\.OwnerSessionId[\s\S]{0,300}?TerminalReporter\.completeWithEvidence/)
  assert.match(host, /published\.SessionId\s*=\s*result\.SessionId\s*&&\s*published\.ProviderRun\s*=\s*result\.ProviderRun/)
  assert.doesNotMatch(host, /TerminalText\s*=\s*aggregate/)

  assert.match(host, /FissionRing\.finalLane group\.LaneCount/)
  assert.doesNotMatch(host, /LastMaterializedLaneIndex/)
  assert.doesNotMatch(projection, /LastMaterializedLaneIndex/)

  const terminalOperationAt = bootstrap.indexOf('let applyObservedTerminal')
  const terminalOperationEnd = bootstrap.indexOf('let startedEvidenceForTerminal', terminalOperationAt)
  assert.ok(terminalOperationAt >= 0 && terminalOperationEnd > terminalOperationAt, 'typed terminal operation must exist')
  const terminalOperation = bootstrap.slice(terminalOperationAt, terminalOperationEnd)
  assert.ok(terminalOperation.includes('(observation: ExactProviderTerminalObservation)'))
  const recoveryAt = terminalOperation.indexOf('scope.SignalChatRecovery(')
  const projectionAt = terminalOperation.indexOf('reconciler.NotifyProjectionChanged(')
  const fissionAt = terminalOperation.indexOf('FissionHost.observePhysicalExecutionEnd')
  assert.ok(
    recoveryAt >= 0 && recoveryAt < projectionAt && projectionAt < fissionAt,
    'the typed terminal operation must settle recovery, publish the exact projection, then open Fission reconciliation',
  )
  assert.match(
    terminalOperation,
    /FissionHost\.observePhysicalExecutionEnd[\s\S]{0,400}?observation\.SessionId\s+observation\.PhysicalUserMessageId/,
  )
  assert.ok(terminalOperation.includes('(fun sid -> reconciler.Kick(sid, ReconcileProgram.ReconcileWake.RetryWake))'))
  assert.doesNotMatch(terminalOperation, /FissionProjection\.tryMembershipOfLane|let isCurrentPhysical|let isFissionLane/)

  const terminalSettlementAt = bootstrap.indexOf('let settleExactTerminal')
  const terminalSettlementEnd = bootstrap.indexOf('let settleObservedTerminal', terminalSettlementAt)
  assert.ok(terminalSettlementAt >= 0 && terminalSettlementEnd > terminalSettlementAt, 'typed terminal settlement must exist')
  assert.ok(
    bootstrap.slice(terminalSettlementAt, terminalSettlementEnd).includes('applyObservedTerminal observation evidence disposition'),
    'the exact typed terminal callback must invoke the terminal operation',
  )
  assert.match(host, /let observePhysicalExecutionEnd/)
  assert.match(host, /tryCurrentPhysical sessionId/)
  assert.match(host, /FissionProjection\.tryMembershipOfLane/)
  assert.match(
    host,
    /if isCurrentPhysical && isFissionLane then[\s\S]{0,120}?kick sessionId/,
    'a successful exact Fission lane terminal must open a snapshot reconcile occasion even when OpenCode drops session.idle',
  )

  assert.match(host, /let routeAttemptAborted/)
  assert.match(host, /FissionRuntime\.isSilentInterrupt sessionId/)
  assert.match(bootstrap, /FissionHost\.routeAttemptAborted/)
  assert.doesNotMatch(bootstrap, /FissionRuntime\.isSilentInterrupt/)
})
