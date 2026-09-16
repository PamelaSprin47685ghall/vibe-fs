import assert from 'node:assert/strict'
import test from 'node:test'
import * as opening from '../../../dist/OpenCode/Host/LifecycleOpeningSurface.js'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as floor from '../../../dist/OpenCode/Host/OpeningFloorSurface.js'
import * as obligationEnvelopeSurface from '../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js'

test('WHAT[OBLIGATION-LEDGER-017] WorkActivated is an inert legacy fact: it fixes ProtectedPrefixEnd once but never re-decides work eligibility', () => {
  assert.equal(opening.testWorkActivatedInertLegacy(), true)
})

test('WHAT[OBLIGATION-LEDGER-017] zero-work planComplete=true with empty obligations is a valid T1 commitment', async () => {
  const r = await membrane.testZeroWorkPlanCompleteTrueValid()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1 BlindPlan does not enlarge the structural Opening floor', () => {
  assert.equal(floor.testPreT1BlindPlanNoFloorEnlarge(), true)
})

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1: no CurrentLife → no floor', () => {
  assert.equal(floor.testPreT1NoLifeNoFloor(), true)
})

test('WHAT[OBLIGATION-LEDGER-017] static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs', () => {
  assert.equal(floor.testBloggerCompanionZeroPrefixEndRefs(), true)
})
