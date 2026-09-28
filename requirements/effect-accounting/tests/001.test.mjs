import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import * as codec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

test('WHAT[effect-accounting-001] actual worktree projection distinguishes intent and confirmation', () => {
  const empty = change.empty()
  const requested = change.requestWorktree(empty, 'worktree', '/repo/worktree', 'job')
  const created = change.acceptWorktree(requested, 'worktree', '/repo/worktree', 'job')
  assert.equal(change.worktreeEffect(empty, 'worktree'), null)
  assert.equal(change.worktreeEffect(requested, 'worktree'), 'Requested')
  assert.equal(change.worktreeEffect(created, 'worktree'), 'Created')
})

test('WHAT[effect-accounting-001] intent and confirmation round-trip through distinct production fact cases', () => {
  const payload = { ManagerJobId: 'job', WorktreeIdentity: 'worktree', WorktreePath: '/repo/worktree' }
  const cases = ['WorktreeCreateRequested', 'WorktreeCreated']
  const bytes = cases.map((name) => codec.encode({ family: 'Orchestrator', case: name, payload }))
  assert.notEqual(bytes[0], bytes[1])
  for (let index = 0; index < cases.length; index += 1) {
    const decoded = codec.decode(bytes[index])
    assert.equal(decoded.ok, true, decoded.error)
    assert.equal(decoded.case, cases[index])
    assert.equal(decoded.line, bytes[index])
  }
})

test.todo('WHAT[effect-accounting-001] real negative compilation rejects confusing effect intent with physical confirmation')
