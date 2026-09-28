import assert from 'node:assert/strict'
import test from 'node:test'
import * as codec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

for (const name of ['DurableEffectRequested', 'DurableEffectAccepted']) {
  test(`WHAT[effect-accounting-010] legacy ${name} fact is explicitly rejected with migration guidance`, () => {
    const result = codec.decode(JSON.stringify(['Agent', ['Orchestrator', [name, {}]]]))
    assert.equal(result.ok, false)
    assert.equal(result.error, codec.pre050MigrationMessage)
  })
}

test('WHAT[effect-accounting-010] a current typed worktree fact is accepted', () => {
  const encoded = codec.encode({ family: 'Orchestrator', case: 'WorktreeCreateRequested', payload: {
    ManagerJobId: 'job', WorktreeIdentity: 'worktree', WorktreePath: '/repo/worktree',
  } })
  const decoded = codec.decode(encoded)
  assert.equal(decoded.ok, true, decoded.error)
  assert.equal(decoded.case, 'WorktreeCreateRequested')
  assert.equal(decoded.line, encoded)
})
