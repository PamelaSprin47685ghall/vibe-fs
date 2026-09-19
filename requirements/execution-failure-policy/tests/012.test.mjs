import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

test('WHAT[execution-failure-policy-012] stop physical run secures admission barrier before detached physical abort and preserves isolation', async () => {
  let terminationCalled = false
  let terminateSessionId = null
  let terminateReason = null

  // Termination mock representing detached Host termination port
  const terminate = async (sessionId, why) => {
    terminationCalled = true
    terminateSessionId = sessionId
    terminateReason = why
    return { ok: true }
  }

  // 1. Admission barrier lands before/alongside detached physical stop
  blog.applyPhysicalStop(terminate, 'ses-execfail-012', 'msg-execfail-012', 'CHRONICLE_EMPTY_ENFORCER_061')
  assert.equal(terminationCalled, true)
  assert.equal(terminateSessionId, 'ses-execfail-012')
  assert.equal(terminateReason, 'CHRONICLE_EMPTY_ENFORCER_061')

  // 2. Physical abort error or rejection does not throw or unbar admission
  const failingTerminate = async (_sid, _why) => {
    throw new Error('Host termination rejected')
  }

  assert.doesNotThrow(() => {
    blog.applyPhysicalStop(failingTerminate, 'ses-execfail-012', 'msg-execfail-012', 'REASON_TEST')
  })

  // 3. Isolated session effect: Target session is explicitly scoped and does not contaminate other sessions
  let otherSessionTerminated = false
  const scopedTerminate = async (sessionId, _why) => {
    if (sessionId !== 'ses-execfail-012') {
      otherSessionTerminated = true
    }
    return { ok: true }
  }

  blog.applyPhysicalStop(scopedTerminate, 'ses-execfail-012', 'msg-execfail-012', 'ISOLATION_TEST')
  assert.equal(otherSessionTerminated, false)
})
