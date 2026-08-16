import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import * as Wire from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostCall = (callId, tool, input) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output: 'pending' } })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const tool = (id, sessionId, parts) => ({ info: { id, role: 'tool', sessionID: sessionId }, parts })
const binding = (replica, budget) => Strength.runtimeBinding('owner', replica, `decision-${replica}`, `target-${replica}`, 'Coder', budget, 65536, `semantic-${replica}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const registered = (replica, budget) => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding(replica, budget)).ok, true)
  return runtime
}
const apply = async (runtime, output) => Strength.transformApply(H, runtime, output)

test('WHAT[SPEC-INV-009] STRENGTH_003_004_replica_initial_transform_replaces_bootstrap_with_frozen_owner_mirror', async () => {
  const runtime = registered('replica-initial', 'K1')
  const output = { messages: [user('u1', 'replica-initial', [hostText('Continue.')])] }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Ready')
  assert.deepEqual(outcome.batches, [])
  assert.deepEqual(outcome.aborted, [])
  const decoded = Wire.decodeMessageView(outcome.output)
  assert.equal(decoded.messages.length, 1)
  assert.equal(decoded.messages[0].parts[0].text, 'owner mirror')
})

test('WHAT[SPEC-INV-003] STRENGTH_003_K1_aborts_before_provider_request_2_after_one_complete_batch', async () => {
  const runtime = registered('replica-k1', 'K1')
  const output = { messages: [user('u1', 'replica-k1', [hostText('Continue.')]), assistant('a1', 'replica-k1', [hostCall('c1', 'read', { filePath: 'a' })]), tool('t1', 'replica-k1', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')])] }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Retired')
  assert.equal(outcome.reason, 'provider-request-budget-reached')
  assert.equal(outcome.batches.length, 1)
  assert.deepEqual(outcome.aborted, ['replica-k1'])
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-k1'), null)
  assert.equal((await apply(runtime, output)).kind, 'NotReplica')
})

test('WHAT[SPEC-INV-003] STRENGTH_003_K1_counts_OpenCode_completed_tool_part_as_one_real_request', async () => {
  const runtime = registered('replica-host-k1', 'K1')
  const output = { messages: [user('u1', 'replica-host-k1', [hostText('Continue.')]), assistant('a1', 'replica-host-k1', [hostResult('c1', 'read', { filePath: 'README.md' }, 'alpha')]), assistant('a2', 'replica-host-k1', [])] }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Retired')
  assert.equal(outcome.reason, 'provider-request-budget-reached')
  assert.equal(outcome.batches.length, 1)
  assert.equal(outcome.batches[0].exchanges[0].toolName, 'read')
  assert.equal(outcome.batches[0].exchanges[0].canonicalArguments, '{"filePath":"README.md"}')
  assert.equal(outcome.batches[0].exchanges[0].canonicalResult, 'alpha')
  assert.deepEqual(outcome.aborted, ['replica-host-k1'])
})

test('WHAT[SPEC-INV-003] STRENGTH_003_K2_allows_request_2_then_aborts_before_request_3', async () => {
  const runtime = registered('replica-k2', 'K2')
  const first = { messages: [user('u1', 'replica-k2', [hostText('Continue.')]), assistant('a1', 'replica-k2', [hostResult('c1', 'grep', { pattern: 'x' }, 'a:1:x')])] }
  assert.equal((await apply(runtime, first)).kind, 'Ready')
  const second = { messages: [user('u1', 'replica-k2', [hostText('Continue.')]), assistant('a1', 'replica-k2', [hostResult('c1', 'grep', { pattern: 'x' }, 'a:1:x')]), assistant('a2', 'replica-k2', [hostResult('c2', 'glob', { pattern: '**/*.fs' }, 'a.fs')])] }
  const retired = await apply(runtime, second)
  assert.equal(retired.kind, 'Retired')
  assert.equal(retired.reason, 'provider-request-budget-reached')
  assert.equal(retired.batches.length, 2)
  assert.deepEqual(retired.aborted, ['replica-k2'])
})
