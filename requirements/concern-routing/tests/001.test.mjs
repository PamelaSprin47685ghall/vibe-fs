import assert from 'node:assert/strict'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

test('WHAT[concern-routing-001] subscribe is idempotent per live owner and keeps id-to-concern immutable', () => {
  let state = concern.empty()
  const first = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', state)
  assert.equal(first.ok, true)
  assert.equal(first.appended, true)
  state = first.state

  const replay = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', state)
  assert.equal(replay.ok, true)
  assert.equal(replay.appended, false)
  assert.equal(concern.subscribe('owner-a', 'new-call', 'build', 'build health', state).appended, false)
  assert.equal(concern.subscribe('owner-b', 'gen-2', 'build', 'build health', state).ok, false)
  assert.equal(concern.subscribe('owner-a', 'gen-2', 'build', 'different meaning', state).ok, false)
})

test('WHAT[concern-routing-001] generation identity cannot be reused for another owner, address or meaning', () => {
  const state = concern.subscribe('owner-a', 'generation', 'build', 'build health', concern.empty()).state
  for (const [owner, id, meaning] of [
    ['owner-b', 'build', 'build health'],
    ['owner-a', 'other', 'build health'],
    ['owner-a', 'build', 'different concern'],
  ]) {
    const rejected = concern.applySubscribedClaim(owner, 'generation', id, meaning, state)
    assert.equal(rejected.ok, false)
    assert.deepEqual(concern.prepare('owner-a', rejected.state), concern.prepare('owner-a', state))
  }
})

test.todo('WHAT[concern-routing-001] subscription remains isolated between real workspaces and conflicts do not overwrite durable owners (GAP-155)')

test('WHAT[concern-routing-001] subscribe rejects blank address fields without creating a mailbox', () => {
  for (const fixture of [
    { id: '', semanticAddress: 'build health', error: 'concern id must be non-empty' },
    { id: ' \t', semanticAddress: 'build health', error: 'concern id must be non-empty' },
    { id: 'build', semanticAddress: '', error: 'concern must be non-empty' },
    { id: 'build', semanticAddress: ' \n', error: 'concern must be non-empty' },
  ]) {
    const result = concern.subscribe('owner-a', 'gen-1', fixture.id, fixture.semanticAddress, concern.empty())
    assert.deepEqual(
      { ok: result.ok, error: result.error, appended: result.appended },
      { ok: false, error: fixture.error, appended: false },
    )
    assert.deepEqual(concern.prepare('owner-a', result.state).announcements, [])

    const replay = concern.applySubscribedClaim(
      'owner-a',
      'gen-1',
      fixture.id,
      fixture.semanticAddress,
      concern.empty(),
    )
    assert.deepEqual({ ok: replay.ok, error: replay.error }, { ok: false, error: fixture.error })
    assert.deepEqual(concern.prepare('owner-a', replay.state).announcements, [])
  }
})
