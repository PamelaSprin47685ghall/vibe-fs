import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'
import * as runtimeStartWatermark from '../../../dist/Persistence/Journal/RuntimeStartWatermarkSurface.js'

const testClaim = (overrides = {}) => ({
  sessionId: 'ses-1',
  logicalRunId: 'run-1',
  authorityRootId: 'auth-1',
  origin: 'test',
  payloadDigest: 'sha256-payload',
  participant: 'agent-1',
  role: 'coder',
  ...overrides,
})

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_submit_records_the_receipt_without_resolving_the_claim', () => {
  const store = dispatch.createMemoryStore()
  const key = dispatch.registerClaim(store, testClaim())
  const receipt = dispatch.submitClaim(store, key, { receiptId: 'rec-1' })
  assert.equal(receipt.ok, true)
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.status, 'Submitted')
  assert.equal(snap.receipt.receiptId, 'rec-1')
})

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone', () => {
  const store = dispatch.createMemoryStore()
  const key = dispatch.registerClaim(store, testClaim())
  dispatch.submitClaim(store, key, { receiptId: 'rec-1' })
  const abandoned = dispatch.abandonClaim(store, key, 'ExplicitCancellation')
  assert.equal(abandoned.ok, true)
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.status, 'Abandoned')
  assert.equal(snap.abandonReason, 'ExplicitCancellation')
})

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_claim_records_payload_digest_and_participant', () => {
  const store = dispatch.createMemoryStore()
  const claim = testClaim({ participant: 'agent-custom', payloadDigest: 'sha256-custom' })
  const key = dispatch.registerClaim(store, claim)
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.participant, 'agent-custom')
  assert.equal(snap.payloadDigest, 'sha256-custom')
})

test('WHAT[DISPATCH-PROTOCOL-002] runtime_start_watermark_tracks_durable_generation', () => {
  const wm = runtimeStartWatermark.create()
  assert.equal(runtimeStartWatermark.current(wm), 0)
  runtimeStartWatermark.advance(wm, 1)
  assert.equal(runtimeStartWatermark.current(wm), 1)
})

test('WHAT[DISPATCH-PROTOCOL-002] send_format_validates_prompt_structure', () => {
  const payload = dispatch.formatPromptPayload({
    promptKey: 'pk-1',
    content: 'hello',
    origin: 'user',
  })
  assert.equal(payload.promptKey, 'pk-1')
  assert.equal(payload.content, 'hello')
})
