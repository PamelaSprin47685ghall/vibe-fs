import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'

test('WHAT[provider-attempt-recovery-010] Blogger retry policy selects maintenance only with material', () => {
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('work-main', true), 'NoActiveBloggerRun')
})

test.todo('WHAT[provider-attempt-recovery-010] actual licensed Blogger retry sends Squash before Main when eligible and accounts for Squash outcome without success reset (GAP-139)')
