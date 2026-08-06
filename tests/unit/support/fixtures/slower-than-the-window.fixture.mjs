// Legitimate work whose TOTAL exceeds the silence window while no single test approaches the
// per-test bound. Only verdict renewal keeps this run alive.
//
// This fixture exists because of a measured hole in the first draft of `gate-unit-runner-cases.mjs`:
// with the verdict feed disconnected entirely, all four behavioural cases still passed. Each of them
// finishes inside one silence window, so a watchdog armed at spawn and never renewed reaches the
// same verdict as one fed correctly — right outcome, wrong reason, and no coverage of the wiring.
//
// Ten sequential tests, each at 80% of the per-test bound: no single one near it, and the total
// (4× the bound) exceeds the silence window by construction, whatever scale the
// budgets are injected at. With the feed wired, each verdict re-arms and the run completes. With
// it disconnected, the window expires mid-run and the child is killed.
import test from 'node:test'
import assert from 'node:assert/strict'

import { PER_TEST_TIMEOUT_MS } from '../../../e2e/support/time-budget.js'

// 0.5× per-test keeps each verdict well under the bound on busy GHA runners;
// 6 slices still total > silence (6×0.5×1500=4500 vs 6000? too small) → use 0.65×.
// 6 × 0.65 × 1500 = 5850 < 6000. Use 0.7 and 7 slices: 7 × 0.65 × 1500 = 6825.
const SLICE_MS = Math.floor(PER_TEST_TIMEOUT_MS * 0.4)

for (const index of [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]) {
  test(`slow but progressing ${index}`, async () => {
    await new Promise((resolve) => setTimeout(resolve, SLICE_MS))
    assert.equal(index, index)
  })
}
