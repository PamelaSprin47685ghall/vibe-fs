import assert from 'node:assert/strict'
import test from 'node:test'
import * as bloggerCrash from '../../../dist/Execution/Session/Recovery/BloggerLiveOwnerCrashSurface.js'
import * as cycleSurface from '../../../dist/Context/Companion/Blogger/Runtime/CycleSurface.js'

test('WHAT[CRASH-016] CRASH_016_blogger_flight_lease_dies_with_the_process_scope', async () => {
  const lease = bloggerCrash.createLease('ses-1')
  bloggerCrash.simulateProcessDeath(lease)
  assert.equal(bloggerCrash.isAlive(lease), false)
})
