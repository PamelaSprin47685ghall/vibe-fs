import test from 'node:test'
import assert from 'node:assert/strict'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const budget = failureOwner.budget

const projection = failureOwner.providerFailureProjection

test('WHAT[context-compression-021] CTX_021_failed_blogger_main_with_material_dispatches_squash', () => {
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
})

test('WHAT[context-compression-021] CTX_021_failed_squash_always_retries_as_main', () => {
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
})

test('WHAT[context-compression-021] CTX_021_retry_dispatch_distinguishes_missing_projection_from_no_active_run', () => {
  assert.equal(compression.nextBloggerRequest('missing', true), 'MissingProjection')
  assert.equal(compression.nextBloggerRequest('work-main', true), 'NoActiveBloggerRun')
})
