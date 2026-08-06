// Legitimate work whose TOTAL exceeds the silence window while no single test
// approaches the per-test bound. Only verdict renewal keeps this run alive.
//
// Slice length is a FIXED short wall-clock budget (not a fraction of
// PER_TEST_TIMEOUT_MS): GHA concurrent load stretches proportional slices
// past the per-test bound even when the fraction looks safe on a quiet laptop.
// Count is chosen so COUNT * SLICE_MS > silence (injected by unit-runner-cases).
import test from 'node:test'
import assert from 'node:assert/strict'

const SLICE_MS = Math.max(20, Number(process.env.UNIT_RUNNER_PROBE_SLICE_MS) || 80)
const COUNT = Math.max(3, Number(process.env.UNIT_RUNNER_PROBE_SLICE_COUNT) || 100)

for (let index = 1; index <= COUNT; index += 1) {
  test(`slow but progressing ${index}`, async () => {
    await new Promise((resolve) => setTimeout(resolve, SLICE_MS))
    assert.equal(index, index)
  })
}
