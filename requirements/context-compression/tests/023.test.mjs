import assert from 'node:assert/strict'
import test from 'node:test'
import * as commitConv from '../../../dist/Context/Companion/EnforcerCycleCommitConvergenceSurface.js'
import * as parked from '../../../dist/Context/Companion/ParkedTransformSurface.js'

test('WHAT[CONTEXT-COMPRESSION-023] ENFORCER_park_never_expires_without_an_event', () => {
  assert.equal(commitConv.parkNeverExpiresWithoutEvent, true)
})

test('WHAT[CONTEXT-COMPRESSION-023] CTX_023_park_has_no_clock_or_timeout_dependency', () => {
  assert.equal(parked.hasNoClockOrTimeoutDependency, true)
})
