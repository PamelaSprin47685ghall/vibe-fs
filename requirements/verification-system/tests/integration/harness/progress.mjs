/**
 * Mid-case feed for the harness suite dog (VERIFY-004).
 *
 * Case-complete is the default renewal. A case that legitimately runs longer than
 * WATCHDOG_TIMEOUT_MS must call harnessProgress on its own causal steps — same rule
 * as e2e canaries, not a wider silence window.
 */

/** @type {null | ((progress: { reason: string, lane?: string, blocking?: boolean }) => void)} */
let feed = null

export function bindHarnessFeed(fn) {
  feed = fn
}

export function harnessProgress(reason, lane = 'harness') {
  feed?.({ reason, lane, blocking: true })
}
