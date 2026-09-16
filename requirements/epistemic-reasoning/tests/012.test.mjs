import assert from 'node:assert/strict'
import test from 'node:test'
import * as kernel from '../../../dist/Sphinx/KernelSurface.js'

test('WHAT[EPI-012] closure_is_idempotent_at_fixed_point', () => {
  const state = kernel.start('Q')
  const c1 = kernel.computeClosure(state)
  const c2 = kernel.computeClosure(c1)
  assert.deepEqual(c1, c2)
})
