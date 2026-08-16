// TIME-002 — invalid process estimates fail before a launcher can run.

import assert from 'node:assert/strict'
import test from 'node:test'

const process = await import('../../../dist/Process/Surface.js')

test('WHAT[TIME-002] EXEC_011_rejects_nan_runtime_estimate', () => {
  const result = process.validateEstimate(NaN, 1024)
  assert.equal(result.ok, false)
  assert.match(result.error, /finite positive number/)
})

test('WHAT[TIME-002] EXEC_011_rejects_zero_and_negative_runtime_estimate', () => {
  for (const bad of [0, -5, -Infinity, Infinity]) {
    const result = process.validateEstimate(bad, 1024)
    assert.equal(result.ok, false, String(bad))
    assert.match(result.error, /finite positive number/)
  }
})

test('WHAT[TIME-002] EXEC_011_rejects_negative_output_estimate', () => {
  const result = process.validateEstimate(10, -1)
  assert.equal(result.ok, false)
  assert.match(result.error, /non-negative/)
})
