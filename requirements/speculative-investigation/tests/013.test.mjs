import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const turn = (sessionId, providerRun, outcome = 'completed') => ({ sessionId, providerRun, outcome, parts: [] })
const binding = (purpose, owner, replica, target) => {
  const handle = Strength.replicaRuntimeCreate(65536)
  const attached = Strength.replicaAttach(handle, Strength.runtimeBinding(owner, replica, `decision-${replica}`, target, 'DevOps', 'K1', 65536, 'semantic-digest', []), purpose)
  assert.equal(attached.ok, true, attached.error)
  return { handle, completion: attached.value.completion }
}

test('WHAT[speculative-investigation-013] actual Replica runtime closes DryRun only on exact owner and target terminal, without closing Treatment', async () => {
  const dry = binding('DryRun', 'owner-dry', 'replica-dry', 'target-dry')
  const treatment = binding('Treatment', 'owner-treatment', 'replica-treatment', 'target-treatment')
  try {
    for (const unrelated of [turn('owner-dry', 'other-run'), turn('other-owner', 'target-dry')]) {
      await Strength.replicaCloseDryRun(dry.handle, unrelated)
      assert.equal(Strength.replicaPeek(dry.handle, 'replica-dry').terminal, null)
      assert.deepEqual(Strength.replicaAborted(dry.handle), [])
    }
    await Strength.replicaCloseDryRun(treatment.handle, turn('owner-treatment', 'target-treatment'))
    assert.equal(Strength.replicaPeek(treatment.handle, 'replica-treatment').terminal, null)
    await Strength.replicaCloseDryRun(dry.handle, turn('owner-dry', 'target-dry'))
    assert.equal((await Strength.replicaAwaitOutcome(dry.completion)).terminal.kind, 'Cancelled')
    assert.deepEqual(Strength.replicaAborted(dry.handle), ['replica-dry'])
    await Strength.replicaCloseDryRun(dry.handle, turn('owner-dry', 'target-dry'))
    assert.deepEqual(Strength.replicaAborted(dry.handle), ['replica-dry'])
  } finally {
    Strength.replicaDispose(dry.handle)
    Strength.replicaDispose(treatment.handle)
    await Strength.replicaAwaitOutcome(treatment.completion)
  }
})

test.todo('WHAT[speculative-investigation-013] GAP-183: actual DryRun startup returns while the real child remains live and never changes Owner projection or Prepared/Promoted facts')
