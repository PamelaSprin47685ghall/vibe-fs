// Legitimate work whose TOTAL exceeds the silence window while no single test approaches the
// per-test bound. Only verdict renewal keeps this run alive.
//
// This fixture exists because of a measured hole in the first draft of `gate-unit-runner-cases.mjs`:
// with the verdict feed disconnected entirely, all four behavioural cases still passed. Each of them
// finishes inside one silence window, so a watchdog armed at spawn and never renewed reaches the
// same verdict as one fed correctly — right outcome, wrong reason, and no coverage of the wiring.
//
// Five sequential tests at 800ms each: every one under the 1000ms per-test bound, four seconds in
// total against a 3000ms window. With the feed wired, each verdict re-arms and the run completes.
// With it disconnected, the window expires mid-run and the child is killed.
import test from 'node:test'
import assert from 'node:assert/strict'

for (const index of [1, 2, 3, 4, 5]) {
  test(`slow but progressing ${index}`, async () => {
    await new Promise((resolve) => setTimeout(resolve, 800))
    assert.equal(index, index)
  })
}
