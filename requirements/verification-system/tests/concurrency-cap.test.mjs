import assert from 'node:assert/strict'
import test from 'node:test'
import { parseConcurrency, assertConcurrency } from '../../../scripts/lib/concurrency-cap.mjs'

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency accepts unset, empty, and boolean values', () => {
  assert.equal(parseConcurrency(undefined), true)
  assert.equal(parseConcurrency(null), true)
  assert.equal(parseConcurrency(''), true)
  assert.equal(parseConcurrency(true), true)
  assert.equal(parseConcurrency('true'), true)
})

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency treats false as 1 alias', () => {
  assert.equal(parseConcurrency(false), 1)
  assert.equal(parseConcurrency('false'), 1)
})

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency accepts positive integers', () => {
  assert.equal(parseConcurrency(1), 1)
  assert.equal(parseConcurrency('1'), 1)
  assert.equal(parseConcurrency(4), 4)
  assert.equal(parseConcurrency('8'), 8)
})

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency throws input error on non-positive, NaN, or non-integer', () => {
  assert.throws(() => parseConcurrency(0), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('0'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(-1), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('-5'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(1.5), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('2.7'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(NaN), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('invalid'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(Infinity), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('-Infinity'), /Invalid NODE_TEST_CONCURRENCY/)
})

test('WHAT[VERIFICATION-SYSTEM-004] assertConcurrency forwards to parseConcurrency', () => {
  assert.equal(assertConcurrency('4'), 4)
  assert.throws(() => assertConcurrency('bad'), /Invalid NODE_TEST_CONCURRENCY/)
})
