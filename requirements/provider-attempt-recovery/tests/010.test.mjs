import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'

test('WHAT[provider-attempt-recovery-010] Blogger retry policy selects BloggerMain directly after squash retirement', () => {
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'MissingProjection')
  assert.equal(compression.nextBloggerRequest('work-main', true), 'NoActiveBloggerRun')
})

test.todo('WHAT[provider-attempt-recovery-010] actual licensed Blogger retry sends Main directly under the failure licence (GAP-139)')
