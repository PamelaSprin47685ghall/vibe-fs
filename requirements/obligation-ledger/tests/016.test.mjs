import assert from 'node:assert/strict'
import test from 'node:test'
import * as opening from '../../../dist/OpenCode/Host/LifecycleOpeningSurface.js'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as proj from '../../../dist/OpenCode/Host/MagicTodoProjectionSurface.js'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'
import * as floor from '../../../dist/OpenCode/Host/OpeningFloorSurface.js'

test('WHAT[OBLIGATION-LEDGER-016] T1 revelation hook wraps the accepted result with entrustment', () => {
  assert.equal(opening.testT1RevelationWrapsResult(), true)
})

test('WHAT[OBLIGATION-LEDGER-016] first accepted planComplete=false stays at the Planning Table without commitment', async () => {
  const r = await membrane.testFirstPlanCompleteFalseStaysPlanning()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-016] projection latches the first true commitment and never reopens it', () => {
  assert.equal(proj.testLatchesFirstTrueCommitment(), true)
})

test('WHAT[OBLIGATION-LEDGER-016] planning checkpoints are allowed until the first irreversible true commitment', () => {
  assert.equal(boundary.testPlanningCheckpointsAllowedUntilCommitment(), true)
})

test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive boundary is independent from the compression floor', () => {
  assert.equal(floor.testT1ConstitutiveBoundaryIndependent(), true)
})

test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive body renders in Opening, not Recent', () => {
  assert.equal(floor.testT1ConstitutiveBodyInOpening(), true)
})

test('WHAT[OBLIGATION-LEDGER-016] XTrace.forOpening keeps T1 tools; forWorkRecord drops them', () => {
  assert.equal(floor.testXTraceForOpeningKeepsT1Tools(), true)
})
