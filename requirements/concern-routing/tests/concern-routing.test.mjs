import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[CONCERN-ROUTING-001] subscribe is idempotent per live owner and keeps id-to-concern immutable', () => {
  let state = concern.empty()
  const first = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', state)
  assert.equal(first.ok, true)
  assert.equal(first.appended, true)
  state = first.state

  const replay = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', state)
  assert.equal(replay.ok, true)
  assert.equal(replay.appended, false)
  assert.equal(concern.subscribe('owner-b', 'gen-2', 'build', 'build health', state).ok, false)
  assert.equal(concern.subscribe('owner-a', 'gen-2', 'build', 'different meaning', state).ok, false)
})

test('WHAT[CONCERN-ROUTING-002] subscription announcement is sticky once per recipient Pair Hint coverage', () => {
  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  const first = concern.prepare('recipient-a', state)
  assert.deepEqual(first.announcements, [{ id: 'build', concern: 'build health' }])
  state = concern.place('recipient-a', first.announcedGenerations, first.deliveredMessages, state).state
  assert.deepEqual(concern.prepare('recipient-a', state).announcements, [])
  assert.deepEqual(concern.prepare('recipient-b', state).announcements, [{ id: 'build', concern: 'build health' }])
})

test('WHAT[CONCERN-ROUTING-003] publish fails closed for unknown and stale generations instead of retargeting', () => {
  assert.equal(concern.publish('sender', 'msg-0', 'missing', 'x', concern.empty()).ok, false)

  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  state = concern.retire('owner-a', 'build', 'gen-1', state).state
  state = concern.subscribe('owner-b', 'gen-2', 'build', 'build health', state).state
  const stale = concern.applyPublishedClaim('sender', 'msg-1', 'build', 'gen-1', 'old generation', state)
  assert.equal(stale.ok, false)
})

test('WHAT[CONCERN-ROUTING-004] messages wait for the next Pair Hint and coverage commits only on placement', () => {
  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  state = concern.publish('sender', 'msg-1', 'build', 'failure found', state).state

  const preparedA = concern.prepare('owner-a', state)
  const preparedB = concern.prepare('owner-a', state)
  assert.deepEqual(preparedA, preparedB, 'uncommitted placement must be byte-stable and non-consuming')
  assert.deepEqual(preparedA.messages, [{ id: 'build', message: 'failure found' }])

  state = concern.place('owner-a', preparedA.announcedGenerations, preparedA.deliveredMessages, state).state
  assert.deepEqual(concern.prepare('owner-a', state).messages, [])
})

test('WHAT[CONCERN-ROUTING-005] peer routing carries no authority or obligation vocabulary', () => {
  const source = [
    read('src/Wanxiangshu/Interaction/Concern/Facts.fs'),
    read('src/Wanxiangshu/Interaction/Concern/Projection.fs'),
    read('src/Wanxiangshu/OpenCode/Tools/ConcernTools.fs'),
  ].join('\n')
  assert.doesNotMatch(source, /PromptAuthority|AuthorityRoot|MagicTodo|Obligation/)
})

test('WHAT[CONCERN-ROUTING-006] retirement prevents old messages crossing into a same-concern replacement generation', () => {
  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  state = concern.publish('sender', 'msg-old', 'build', 'old message', state).state
  state = concern.retire('owner-a', 'build', 'gen-1', state).state
  const rebound = concern.subscribe('owner-b', 'gen-2', 'build', 'build health', state)
  assert.equal(rebound.ok, true)
  assert.equal(rebound.appended, true)
  state = rebound.state
  assert.deepEqual(concern.prepare('owner-b', state).messages, [])
})

test('WHAT[CONCERN-ROUTING-007] routing state remains mailbox facts plus bounded delivery coverage, not an organization workflow', () => {
  const source = read('src/Wanxiangshu/Interaction/Concern/Projection.fs')
  assert.doesNotMatch(source, /\b(Priority|Deadline|OrganizationGraph|PresenceAuthority|WorkflowEngine|AckProtocol)\b/)
})

