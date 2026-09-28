import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import * as cycle from '../../../dist/Context/Companion/Blogger/Runtime/CycleSurface.js'

const materialize = { kind: 'materialize', requestId: 'request', blogger: 'blogger', digest: 'context' }
const receipt = { kind: 'entry', requestId: 'request', run: 'provider-run' }

test('WHAT[effect-accounting-004] actual worktree confirmation absorbs a repeated request and confirmation', () => {
  const requested = change.requestWorktree(change.empty(), 'worktree', '/repo/worktree', 'job')
  const confirmed = change.acceptWorktree(requested, 'worktree', '/repo/worktree', 'job')
  const repeated = change.acceptWorktree(change.requestWorktree(confirmed, 'worktree', '/repo/worktree', 'job'), 'worktree', '/repo/worktree', 'job')
  assert.equal(change.worktreeEffect(repeated, 'worktree'), 'Created')
  assert.equal(change.worktreeEffect(repeated, 'another-worktree'), null)
})

test('WHAT[effect-accounting-004] repeated Blogger materialization before acceptance creates one open request', () => {
  assert.deepEqual(cycle.scenario([materialize, materialize]), {
    ok: true,
    state: { openRequests: 1, openBloggers: 1, receipts: 0, requestBindings: 0 },
  })
})

test.todo('WHAT[effect-accounting-004] actual Blogger commit absorbs exact receipt replay and refuses conflicting payload; low-level receipt rejection alone does not prove a violation; GAP-100, GAP-105')

test.todo('WHAT[effect-accounting-004] actual admission does not reopen an already accepted Blogger request; isolated materialization does not exercise command admission; GAP-100, GAP-105')
