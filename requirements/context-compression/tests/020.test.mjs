import assert from 'node:assert/strict'
import test from 'node:test'
import * as floor from '../../../dist/Context/Companion/OpeningFloorSurface.js'

test('WHAT[CONTEXT-COMPRESSION-020] todowrite_material_does_not_redefine_the_owned_opening_floor', () => {
  assert.equal(floor.todoWriteDoesNotRedefineFloor, true)
})

test('WHAT[CONTEXT-COMPRESSION-020] todowrite call and matching result are retained across a Y cutoff', () => {
  assert.equal(floor.todoWriteRetainedAcrossYCutoff, true)
})
