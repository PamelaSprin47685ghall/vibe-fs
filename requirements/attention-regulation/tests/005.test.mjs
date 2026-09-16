import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as attention from '../../../dist/Interaction/Attention/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[ATTENTION-REGULATION-005] resurfacing consumes deferred visibility once without activating work', () => {
  let state = attention.empty()
  state = attention.record('ses-a', 'call-1', 'one', state)
  state = attention.record('ses-a', 'call-2', 'two', state)
  state = attention.resurface('ses-a', 'learn-1', ['call-1', 'call-2'], state)
  state = attention.resurface('ses-a', 'learn-1', ['call-1', 'call-2'], state)
  assert.deepEqual(attention.pending('ses-a', state), [])

  const projection = read('src/Wanxiangshu/Interaction/Attention/Projection.fs')
  assert.doesNotMatch(projection, /StartWork|Activate|Delegate|Background/)
})
