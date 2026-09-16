// retry-policy.test.mjs — PAR-006/008/010/011/013/015/016/018.
//
// Retry policy and attempt plans in the one-retry world. Every assertion
// drives production: AttemptPlanner via PlannerSurface/CompressionSurface,
// BloggerRetryPolicy via CompressionSurface.nextBloggerRequest, TerminalValidity
// via CompressionSurface.terminalValidity, and the failure budget via
// Fallback/ProviderFailureSurface.js. No copied policy, no source-text reads.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as attemptPurpose from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const planner = compression.attemptPlanner
const { budget, providerFailureProjection } = failureOwner

const TOOL_CAPABILITIES = [
  'BashHoneypot',
  'Edit',
  'Fetch',
  'Fission',
  'Glob',
  'Grep',
  'Inspect',
  'Move',
  'Read',
  'Remove',
  'Write',
]

test('WHAT[PAR-010] blogger_retry_dispatch_selects_squash_when_material_exists_and_main_otherwise', () => {
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('work-main', true), 'NoActiveBloggerRun')
})
