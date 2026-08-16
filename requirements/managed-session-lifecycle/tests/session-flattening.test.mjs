import assert from 'node:assert/strict'
import test from 'node:test'
import { familyCascade } from './support/managed-surface.mjs'

test('WHAT[MANAGED-SESSION-003] HOST_015_abort_children_cascade_stays_keyed_on_family_root', () => {
  const observed = familyCascade(['child-1', 'child-2'])
  assert.deepEqual(observed.createdParents, ['root', 'root'])
  assert.deepEqual(observed.aborted.sort(), ['child-1', 'child-2'])
})
