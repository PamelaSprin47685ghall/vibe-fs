// Legitimate work whose TOTAL exceeds the silence window while no single test
// approaches the per-test bound. Only verdict renewal keeps this run alive.
//
// Design (CI-stable):
// - Few long slices (not dozens of 80ms micro-slices): less IPC noise, clearer renewal.
// - Run serially under NODE_TEST_CONCURRENCY=1 from unit-runner-cases only.
//   Default unit suite keeps concurrency:true (efficient).
// - COUNT * SLICE_MS > silence; each SLICE_MS < per-test with real headroom.
import test from 'node:test'
import assert from 'node:assert/strict'

const SLICE_MS = Math.max(50, Number(process.env.UNIT_RUNNER_PROBE_SLICE_MS) || 1500)
const COUNT = Math.max(2, Number(process.env.UNIT_RUNNER_PROBE_SLICE_COUNT) || 5)

for (let index = 1; index <= COUNT; index += 1) {
  test(`slow but progressing ${index}`, async () => {
    await new Promise((resolve) => setTimeout(resolve, SLICE_MS))
    assert.equal(index, index)
  })
}
