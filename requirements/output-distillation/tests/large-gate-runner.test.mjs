// Split from tests/unit/process/process-runner.test.mjs (cutover Wave 2a); owner: output-distillation
//
// DISTILL-011 Large Gate 预算合同：Large estimate 的 run 期间 gate 被持有，
// run 结束释放（其余 run 生命周期断言 → process-execution；estimate 校验 → time-capability）。

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, liveToken, processRequest } from '../../verification-system/tests/support/domain.mjs'
import { lib } from '../../verification-system/tests/support/domain.mjs'

const { runWithLauncher } = await import('../../../dist/Process/ProcessRunner.js')
const { fromSeconds } = await lib('TimeSpan.js')

const CTX = {
  WorkingDirectory: undefined,
  HardLimit: fromSeconds(3600),
  Environment: undefined,
}

const cmd = processRequest.command({ fileName: 'sh', args: ['-c', 'echo hi'] })
const estimate = (runtimeSeconds = 10, outputBytes = 1024, memory = 'Medium') =>
  processRequest.estimate({ runtimeSeconds, outputBytes, memory })

// ── Large gate ───────────────────────────────────────────────────────────────

test('EXEC_011_large_estimate_acquires_and_releases_the_gate', async () => {
  const { acquire, release, getCount } = await import('../../../dist/Process/LargeGate.js')
  // Drain to a known state.
  while (getCount() === 0) release()

  let gateCountDuringRun = undefined
  const observingLauncher = async (_cmd, _ct) => {
    gateCountDuringRun = getCount()
    return [0, new Uint8Array(0), new Uint8Array(0)]
  }

  const result = await runWithLauncher(observingLauncher, cmd, estimate(10, 1024, 'Large'), CTX, liveToken())

  assert.equal(caseOf(result), 'Ok')
  assert.equal(gateCountDuringRun, 0, 'the gate is held while the large process runs')
  assert.equal(getCount(), 1, 'the gate is released after the run')
})
