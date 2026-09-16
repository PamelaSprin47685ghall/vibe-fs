import assert from 'node:assert/strict'
import test from 'node:test'
import * as closure from '../../../dist/Execution/Session/ReusedSessionRunClosureSurface.js'
import * as prop from '../../../dist/Execution/Session/SubagentReuseClosurePropertySurface.js'

test('WHAT[MANAGED-SESSION-020] fresh identity waits for exact durable prior-run closure on the public plugin canary', () => {
  assert.equal(closure.testFreshIdentityWaitsClosure(), true)
})

test('WHAT[MANAGED-SESSION-020] subagent session reuse property verifies prior-run closure across arbitrary agent sequences', () => {
  assert.equal(prop.testPriorRunClosureProperty(), true)
})

test('WHAT[MANAGED-SESSION-020] subagent authority closure cleans claims, continuations, and sequences while retaining history', () => {
  assert.equal(prop.testAuthorityClosureCleans(), true)
})

test('WHAT[MANAGED-SESSION-020] Manager AgentOwnerRoot remains active for owner-directed post-life recovery', () => {
  assert.equal(prop.testManagerAgentOwnerRootActive(), true)
})

test('WHAT[MANAGED-SESSION-020] mutant: omitting child-work closure causes fast-check to detect ActiveRunIdentityConflict', () => {
  assert.equal(prop.testOmittingClosureDetected(), true)
})
