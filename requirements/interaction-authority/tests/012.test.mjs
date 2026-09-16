import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

test('WHAT[INTERACTION-AUTHORITY-012] IA_005_degeneration_guard_is_continuation', () => {
  for (const kind of ['DegenerationGuard', 'ManagerGuard']) {
    assert.deepEqual(authority.originForContinuation(kind), { kind: 'Continuation', label: kind })
  }
  assert.equal(authority.tryParseContinuationKind('HumanRoot'), null)
})
