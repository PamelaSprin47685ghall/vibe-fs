// requirements/process-execution/tests/large-gate-runner.test.mjs
// Owner: process-execution.
//
// PROC-016: Large Gate 大输出单持有者互斥门禁与并发保护

import assert from 'node:assert/strict'
import test from 'node:test'

const { getCount, release, runLargeEstimate } = await import('../../../dist/Process/LargeGateSurface.js')

test('WHAT[PROC-016] large_estimate_acquires_and_releases_the_gate', async () => {
  while (getCount() === 0) release()

  let gateCountDuringRun = undefined
  const result = await runLargeEstimate(() => {
    gateCountDuringRun = getCount()
  })

  assert.equal(result, true)
  assert.equal(gateCountDuringRun, 0, 'the gate is held while the large process runs')
  assert.equal(getCount(), 1, 'the gate is released after the run')
})
