import assert from 'node:assert/strict'
import test from 'node:test'
import * as merge from '../../../dist/Persistence/EventStore/MergeSurface.js'
import { event } from './support/events.mjs'

test('WHAT[durable-convergence-006] merge retains competing facts despite opposing revision and wall-clock values', () => {
  const older = event('a'.repeat(40), [], { revision: 900, ObservedAt: '2026-01-01T00:00:00Z' })
  const newer = event('b'.repeat(40), [], { revision: 1, ObservedAt: '2026-09-26T00:00:00Z' })
  for (const streams of [[['old', [older]], ['new', [newer]]], [['new', [newer]], ['old', [older]]]]) {
    const result = merge.merge(streams)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.deepEqual(result.events, [older, newer])
  }
})
