import assert from 'node:assert/strict'
import test from 'node:test'
import * as companionRetry from '../../../dist/Context/Companion/CompanionRetryPolicySurface.js'

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_failed_blogger_main_with_material_dispatches_squash', () => {
  assert.equal(companionRetry.dispatchOnFailure('BloggerMain', true), 'Squash')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_failed_squash_always_retries_as_main', () => {
  assert.equal(companionRetry.dispatchOnFailure('BloggerSquash', true), 'Main')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_retry_dispatch_distinguishes_missing_projection_from_no_active_run', () => {
  assert.ok(companionRetry.distinguishesMissingProjection)
})
