/**
 * Concurrency cap parser and limiter for node:test runner.
 *
 * NODE_TEST_CONCURRENCY:
 * - 'false' -> alias for 1 (serial)
 * - positive integer -> concurrency number
 * - unset / empty -> true (runner default in-process parallelism)
 * - NaN, <=0, non-integer, Infinity -> must throw input error
 */

export function parseConcurrency(value) {
  if (value === undefined || value === null || value === '') {
    return true
  }
  if (value === 'false' || value === false) {
    return 1
  }
  if (value === 'true' || value === true) {
    return true
  }
  const n = Number(value)
  if (!Number.isFinite(n) || !Number.isInteger(n) || n <= 0) {
    throw new TypeError(
      `Invalid NODE_TEST_CONCURRENCY: ${JSON.stringify(value)}. Must be a positive integer, 'false', or unset.`,
    )
  }
  // Note: concurrency probe 待测
  return n
}

export function assertConcurrency(value) {
  return parseConcurrency(value)
}
