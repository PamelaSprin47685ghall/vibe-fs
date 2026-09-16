import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-005] DP_005_prompt_key_is_deterministic_and_moves_with_every_component', () => {
  const base = {
    sessionId: 'ses-1',
    logicalRunId: 'run-1',
    authorityRootId: 'auth-1',
    origin: 'user',
    payloadDigest: 'sha256-abc',
    sequence: 0,
  }
  const key1 = dispatch.derivePromptKey(base)
  const key2 = dispatch.derivePromptKey(base)
  assert.equal(key1, key2)

  const diffSession = dispatch.derivePromptKey({ ...base, sessionId: 'ses-2' })
  assert.notEqual(key1, diffSession)

  const diffRun = dispatch.derivePromptKey({ ...base, logicalRunId: 'run-2' })
  assert.notEqual(key1, diffRun)

  const diffAuth = dispatch.derivePromptKey({ ...base, authorityRootId: 'auth-2' })
  assert.notEqual(key1, diffAuth)

  const diffDigest = dispatch.derivePromptKey({ ...base, payloadDigest: 'sha256-xyz' })
  assert.notEqual(key1, diffDigest)

  const diffSeq = dispatch.derivePromptKey({ ...base, sequence: 1 })
  assert.notEqual(key1, diffSeq)
})

test('WHAT[DISPATCH-PROTOCOL-005] DP_005_claim_scope_names_exactly_session_run_origin_and_payload', () => {
  const scope = dispatch.deriveClaimScope({
    sessionId: 's1',
    logicalRunId: 'r1',
    origin: 'tool',
    payloadDigest: 'd1',
  })
  assert.equal(scope, 's1:r1:tool:d1')
})
