import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[concern-routing-002] subscription announcement is sticky once per recipient Pair Hint coverage', () => {
  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  const first = concern.prepare('recipient-a', state)
  assert.deepEqual(first.announcements, [{ id: 'build', concern: 'build health' }])
  state = concern.place('recipient-a', first.announcedGenerations, first.deliveredMessages, state).state
  assert.deepEqual(concern.prepare('recipient-a', state).announcements, [])
  assert.deepEqual(concern.prepare('recipient-b', state).announcements, [{ id: 'build', concern: 'build health' }])
})
