import assert from 'node:assert/strict'
import test from 'node:test'
import * as fission from '../../../dist/Execution/Fission/Surface.js'
import * as fissionHost from '../../../dist/OpenCode/Host/FissionHostSurface.js'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const mustOk = result => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[intra-participant-parallelism-009] convergence predicate rejects each missing record or broadcast debt', () => {
  const bundle = [0, 1, 2].reduce(
    (state, lane) => mustOk(fission.workBundleAdd(lane, `ref-${lane}`, state)).bundle,
    fission.workBundleEmpty,
  )
  let delivery = fission.deliveryEmpty(3)
  for (const lane of [0, 1, 2]) {
    assert.equal(fission.convergenceReady(3, ['pre-child'], bundle, delivery), false)
    delivery = mustOk(fission.deliveryMark('pre-child', lane, delivery)).delivery
  }
  assert.equal(fission.convergenceReady(3, ['pre-child'], bundle, delivery), true)
  assert.equal(fission.convergenceReady(3, ['pre-child', 'other-child'], bundle, delivery), false)
  for (const absent of [0, 1, 2]) {
    const incomplete = [0, 1, 2].filter(lane => lane !== absent).reduce(
      (state, lane) => mustOk(fission.workBundleAdd(lane, `ref-${lane}`, state)).bundle,
      fission.workBundleEmpty,
    )
    assert.equal(fission.convergenceReady(3, ['pre-child'], incomplete, delivery), false)
  }
})

test('WHAT[intra-participant-parallelism-009] ring routing skips closed lanes and wraps to the next live lane', () => {
  assert.equal(fission.ringSuccessor(4, 0, [0, 1, 2, 3]), null)
  assert.equal(fission.ringSuccessor(4, 1, [0, 1, 3]), 2)
  assert.equal(fission.ringSuccessor(4, 1, [0, 1, 2]), 3)
  assert.equal(fission.ringSuccessor(4, 3, [1, 2, 3]), 0)
})

test('WHAT[intra-participant-parallelism-009] actual turn observers absorb a marked retired owner without continuation or terminal notification', async () => {
  const owner = 'retired-owner-workflow'
  fission.markSilentInterrupt(owner)
  try {
    const observed = await fissionHost.observeReplacedOwner(owner)
    assertJsData(observed, 'Fission host observation')
    assert.deepEqual(observed, { handled: true, continuationSent: false, terminalNotified: false })
  } finally {
    fission.clearOwner(owner)
  }
})

test('WHAT[intra-participant-parallelism-009] an explicitly kicked reconciler observes the supplied completed execution without an idle event', async () => {
  const observed = await fissionHost.missingIdleTerminalBridgeScenario()
  assertJsData(observed, 'reconciler observation')
  assert.equal(observed.outcome, 'TurnCompleted')
  assert.equal(observed.physicalUserMessageId, 'fission-missing-idle-user')
  assert.equal(observed.providerRun, 'fission-missing-idle-run')
  assert.ok(observed.snapshotReads >= 1)
})

test.todo('WHAT[intra-participant-parallelism-009] GAP-158: actual terminal-to-Fission bridge, takeover continuation across recovery and one final completion in the original cell')
