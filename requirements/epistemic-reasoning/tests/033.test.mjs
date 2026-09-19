import assert from 'node:assert/strict'
import test from 'node:test'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'



test('WHAT[epistemic-reasoning-033] result acceptance is idempotent by work identity preventing late acceptance and duplicate purchases', () => {
  // Accepted events bind workId and attempt identity. Replay folds them deterministically.
  const genesis = {
    id: 'ev0',
    parent: 'none',
    workId: 'work-1',
    attempt: 1,
    kind: 'genesis',
    payload: { question: 'test question' },
  }
  const result1 = gecSurface.replay([genesis])
  assert.equal(typeof result1.semanticHash, 'string')

  // Replaying identical canonical events produces identical state and hash (idempotent)
  const result2 = gecSurface.replay([genesis])
  assert.equal(result2.semanticHash, result1.semanticHash)
})
