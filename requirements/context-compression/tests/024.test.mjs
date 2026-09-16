import assert from 'node:assert/strict'
import test from 'node:test'
import * as cleanBreak from '../../../dist/Context/Companion/BloggerBoundaryCleanBreakSurface.js'
import * as flight from '../../../dist/Context/Companion/BloggerFlightInterleavingSurface.js'
import * as companionRetry from '../../../dist/Context/Companion/CompanionRetryPolicySurface.js'
import * as cycleConv from '../../../dist/Context/Companion/EnforcerCycleConvergenceSurface.js'
import * as parked from '../../../dist/Context/Companion/ParkedTransformSurface.js'
import * as bloggerRequest from '../../../dist/Context/Companion/Blogger/Request.js'

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_flight_claim_conflict_keeps_foreign_owner_and_rejects_stale_release', () => {
  assert.ok(cleanBreak.flightClaimConflictKeepsOwner)
})

test('WHAT[CONTEXT-COMPRESSION-024] every valid flight interleave keeps B unaffected by late A', () => {
  assert.ok(flight.validInterleaveKeepsUnaffected)
})

test('WHAT[CONTEXT-COMPRESSION-024] superseded A callbacks are idempotent, never a second fatal', () => {
  assert.ok(flight.supersededCallbacksIdempotent)
})

test('WHAT[CONTEXT-COMPRESSION-024] stale release of A never releases B; new B producer never joins A slot', () => {
  assert.ok(flight.staleReleaseNeverReleasesB)
})

test('WHAT[CONTEXT-COMPRESSION-024] same-request epoch refresh keeps ownership; B still cannot intrude', () => {
  assert.ok(flight.epochRefreshKeepsOwnership)
})

test('WHAT[CONTEXT-COMPRESSION-024] B repair after supersede waits for the dead episode drain, flight still exact', () => {
  assert.ok(flight.repairWaitsForDrain)
})

test('WHAT[CONTEXT-COMPRESSION-024] scheduled promise order cannot move B terminal or capacity', () => {
  assert.ok(flight.promiseOrderCannotMoveTerminal)
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_request_scoped_repair_continues_only_for_the_current_request', () => {
  assert.ok(companionRetry.requestScopedRepairOnlyCurrent)
})

test('WHAT[CONTEXT-COMPRESSION-024] stale_terminal_cannot_reclaim_a_new_Blogger_request', () => {
  assert.ok(cycleConv.staleTerminalCannotReclaim)
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_flight_claim_never_overwrites_another_request', () => {
  assert.ok(parked.flightClaimNeverOverwrites)
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_stale_release_cannot_clear_a_newer_owner', () => {
  assert.ok(parked.staleReleaseCannotClearNewerOwner)
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_materialization_admission_is_cross_instance_single_flight', () => {
  assert.ok(parked.materializationAdmissionCrossInstance)
})
