import assert from 'node:assert/strict'
import test from 'node:test'
import * as merge from '../../../dist/Persistence/EventStore/MergeSurface.js'
import { event } from './support/events.mjs'

// Whole-writer expiry and preservation of the retained file are exercised in 011.
test('WHAT[durable-convergence-001] merge preserves distinct concurrent facts and removes only exact duplicates', () => {
  const left = event('a'.repeat(40), [], { value: 'left' })
  const right = event('b'.repeat(40), [], { value: 'right' })
  const result = merge.merge([
    ['writer-left', [left]],
    ['writer-right', [right]],
    ['writer-copy', [structuredClone(left)]],
  ])
  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.deepEqual(result.events, [left, right])
})
