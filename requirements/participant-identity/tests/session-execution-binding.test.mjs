// PID-008 / PROMPT-006: external user messages own participant proof for a
// fixed participant+Role; model execution is leased by the scheduler for that
// Role, and the binding surface only exposes the semantic result of that
// ownership protocol. No PeerAgent, EffectiveAgent, or cursor selection exists.

import assert from 'node:assert/strict'
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test, { after } from 'node:test'

const originalHome = process.env.HOME
const home = await mkdtemp(join(tmpdir(), 'wanxiangshu-binding-home-'))
process.env.HOME = home
await mkdir(join(home, '.config', 'opencode'), { recursive: true })
await writeFile(
  join(home, '.config', 'opencode', 'wanxiangshu.mjs'),
  `
export default function route(role) {
  return { model: 'test/deep', reasoning: 'high' }
}
`,
  'utf8',
)

const binding = await import('../../../dist/OpenCode/Host/SessionBindingSurface.js')
const routing = await import('../../../dist/OpenCode/Host/ModelRoutingSurface.js')
await routing.initialize()

const modelFor = (_participant) => ({ providerID: 'test', modelID: 'deep', variant: 'high' })

const assertPrepared = (result, participant) => {
  assert.equal(result.ok, true, result.error)
  assert.equal(result.value.agent, participant)
  assert.equal(result.value.modelProvided, false, 'dispatch remains model-free')
}

const assertNoLegacyRoutingFields = (value, label) => {
  const text = JSON.stringify(value)
  for (const token of ['PeerAgent', 'peerAgent', 'eerAgent', 'EffectiveAgent', 'effectiveAgent', 'ffectiveAgent', 'cursor', 'Cursor']) {
    assert.equal(text.includes(token), false, `${label} must not contain ${token}: ${text}`)
  }
}

const acquireLease = async (sessionId, physicalUserMessageId, role, participant) => {
  const outcome = await routing.acquireSharedExecutionAdmission(
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    null,
  )
  assert.equal(outcome.kind, 'Acquired')
  const target = routing.sharedExecutionAdmissionTarget(outcome.lease)
  const exact = {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target,
  }
  assertNoLegacyRoutingFields(exact, 'admission identity')
  return { lease: outcome.lease, exact }
}

const admitPhysicalExecution = async (sessionId, role, participant) => {
  const physicalId = `msg-binding-${sessionId}`
  const admission = await acquireLease(sessionId, physicalId, role, participant)
  const target = admission.exact.target
  assert.equal(target.model, 'test/deep')
  assert.equal(target.reasoning, 'high')
  assert.deepEqual(
    routing.releaseSharedExecutionAdmissionBeforeProvider(admission.lease, admission.exact),
    { kind: 'Applied' },
  )
  return { target, exact: admission.exact }
}

after(async () => {
  if (originalHome === undefined) delete process.env.HOME
  else process.env.HOME = originalHome
  await rm(home, { recursive: true, force: true })
})

test('WHAT[PID-008] root_requires_external_participant_proof_then_model_is_scheduler_owned', async () => {
  const root = 'ses_binding_root'
  const model = modelFor('coder')

  const unproven = binding.prepareUserFacing(root, 'coder', false, model)
  assert.equal(unproven.ok, false)
  assert.match(unproven.error, /no observed user binding/i)

  binding.observeUserFacingAgent(root, 'coder')
  assertPrepared(binding.prepareUserFacing(root, 'coder', false, model), 'coder')
  const first = await admitPhysicalExecution(root, 'coder', 'coder')

  const temporary = binding.prepareUserFacing(root, 'coder', true, modelFor('coder'))
  assertPrepared(temporary, 'coder')
  const second = await admitPhysicalExecution(`${root}-override`, 'coder', 'coder')

  // Fresh physical retries keep the same fixed participant+Role even as the
  // scheduler hands out a new routing target admission.
  assert.equal(second.exact.participant, first.exact.participant)
  assert.equal(second.exact.role, first.exact.role)
  assert.deepEqual(second.target, first.target)

  // A preserve request cannot use a foreign override as a new base.
  assertPrepared(binding.prepareUserFacing(root, 'coder', false, model), 'coder')
  await admitPhysicalExecution(`${root}-restored`, 'coder', 'coder')

  const foreign = binding.prepareUserFacing(root, 'inspector', true, modelFor('inspector'))
  assert.equal(foreign.ok, false)
  assert.match(foreign.error, /must equal authority participant/i)

  binding.observeUserFacingAgent(root, 'inspector')
  assertPrepared(binding.prepareUserFacing(root, 'inspector', false, modelFor('inspector')), 'inspector')
  await admitPhysicalExecution(`${root}-switched`, 'inspector', 'inspector')

  binding.drop(root)
})

test('WHAT[PID-008] parented_session_uses_stable_participant_lease_and_authorized_peer_only', async () => {
  const parent = 'ses_parent'
  const child = 'ses_child'
  const created = binding.bindChild(parent, child, 'distiller')
  assert.equal(created.ok, true, created.error)

  assertPrepared(binding.prepareManaged(child, 'distiller', false, modelFor('distiller')), 'distiller')
  await admitPhysicalExecution(child, 'distiller', 'distiller')

  const peer = binding.prepareManaged(child, 'distiller', true, modelFor('distiller'))
  assertPrepared(peer, 'distiller')
  await admitPhysicalExecution(`${child}-peer`, 'distiller', 'distiller')

  const foreign = binding.prepareManaged(child, 'coder', true, modelFor('coder'))
  assert.equal(foreign.ok, false)
  assert.match(foreign.error, /must equal authority participant/i)

  binding.drop(child)
})

test('WHAT[PID-008] provider_reasoning_variant_must_match_the_exact_lease', async () => {
  const parent = 'ses_variant_parent'
  const child = 'ses_variant_exact'
  assert.equal(binding.bindChild(parent, child, 'distiller').ok, true)

  const physicalId = 'msg-variant-exact'
  const expected = modelFor('distiller')
  const admission = await acquireLease(child, physicalId, 'distiller', 'distiller')
  const target = admission.exact.target
  assert.deepEqual(target, { model: 'test/deep', reasoning: 'high' })
  assert.deepEqual(routing.commitSharedExecutionAdmission(admission.lease, admission.exact), { kind: 'Applied' })

  binding.acceptPromptExecution(child, 'prompt-variant-exact', physicalId, 'distiller', expected)
  assert.equal(binding.beginProviderAttempt(child, physicalId, 'prompt-variant-exact').ok, true)

  const valid = binding.validateObservedProvider(child, 'distiller', expected)
  assert.equal(valid.ok, true)
  assert.equal(valid.value, true)

  const drift = binding.validateObservedProvider(child, 'distiller', { ...expected, variant: 'default' })
  assert.equal(drift.ok, false)
  assert.match(drift.error, /model\/reasoning drift/i)
  assert.match(drift.error, /test\/deep\[high\] -> test\/deep\[default\]/)

  binding.drop(child)
})
