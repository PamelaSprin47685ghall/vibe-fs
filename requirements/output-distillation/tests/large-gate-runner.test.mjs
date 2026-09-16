// Split from tests/unit/process/process-runner.test.mjs (cutover Wave 2a); owner: output-distillation
//
// DISTILL-011 Large Gate 预算合同：Large estimate 的 run 期间 gate 被持有，
// run 结束释放（其余 run 生命周期断言 → process-execution；estimate 校验 → time-capability）。

import assert from 'node:assert/strict'
import test from 'node:test'

const { getCount, release, runLargeEstimate } = await import('../../../dist/Process/LargeGateSurface.js')

// ── Large gate ───────────────────────────────────────────────────────────────

test('WHAT[DISTILL-011] EXEC_011_large_estimate_acquires_and_releases_the_gate', async () => {
  // Drain to a known state.
  while (getCount() === 0) release()

  let gateCountDuringRun = undefined
  const result = await runLargeEstimate(() => {
    gateCountDuringRun = getCount()
  })

  assert.equal(result, true)
  assert.equal(gateCountDuringRun, 0, 'the gate is held while the large process runs')
  assert.equal(getCount(), 1, 'the gate is released after the run')
})
