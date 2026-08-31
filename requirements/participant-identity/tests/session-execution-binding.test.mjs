// PID-008 / PROMPT-006: external user messages own EffectiveAgent selection;
// model execution is leased by the scheduler and the binding surface only
// exposes the semantic result of that ownership protocol.

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
  if (role.startsWith('deep-')) return { model: 'test/deep', reasoning: 'high' }
  if (role.startsWith('fast-')) return { model: 'test/fast', reasoning: 'none' }
  return { model: 'test/system', reasoning: 'none' }
}
`,
  'utf8',
)

const binding = await import('../../../dist/OpenCode/Host/SessionBindingSurface.js')
const routing = await import('../../../dist/OpenCode/Host/ModelRoutingSurface.js')
await routing.initialize()

const modelFor = (agent) =>
  agent.startsWith('deep-')
    ? { providerID: 'test', modelID: 'deep', variant: 'high' }
    : { providerID: 'test', modelID: 'fast', variant: 'none' }

const assertPrepared = (result, agent) => {
  assert.equal(result.ok, true, result.error)
  assert.equal(result.value.agent, agent)
  assert.equal(result.value.modelProvided, false, 'dispatch remains model-free')
}

const acquireLease = async (sessionId, physicalUserMessageId, agent) => {
  const outcome = await routing.acquireSharedExecutionAdmission(
    sessionId,
    physicalUserMessageId,
    agent,
  )
  assert.equal(outcome.kind, 'Acquired')
  const target = routing.sharedExecutionAdmissionTarget(outcome.lease)
  return {
    lease: outcome.lease,
    exact: {
      sessionId,
      physicalUserMessageId,
      effectiveAgent: agent,
      target,
    },
  }
}

const admitPhysicalExecution = async (sessionId, agent) => {
  const physicalId = `msg-binding-${sessionId}`
  const admission = await acquireLease(sessionId, physicalId, agent)
  const target = admission.exact.target
  assert.equal(target.model, agent.startsWith('deep-') ? 'test/deep' : 'test/fast')
  assert.equal(target.reasoning, agent.startsWith('deep-') ? 'high' : 'none')
  assert.deepEqual(
    routing.releaseSharedExecutionAdmissionBeforeProvider(admission.lease, admission.exact),
    { kind: 'Applied' },
  )
  return target
}

after(async () => {
  if (originalHome === undefined) delete process.env.HOME
  else process.env.HOME = originalHome
  await rm(home, { recursive: true, force: true })
})

test('WHAT[PID-008] root_requires_external_agent_proof_then_model_is_scheduler_owned', async () => {
  const root = 'ses_binding_root'
  const model = modelFor('deep-coder')

  const unproven = binding.prepareUserFacing(root, 'deep-coder', false, model)
  assert.equal(unproven.ok, false)
  assert.match(unproven.error, /no observed user binding/i)

  binding.observeUserFacingAgent(root, 'deep-coder')
  assertPrepared(binding.prepareUserFacing(root, 'deep-coder', false, model), 'deep-coder')
  await admitPhysicalExecution(root, 'deep-coder')

  const temporary = binding.prepareUserFacing(root, 'fast-coder', true, modelFor('fast-coder'))
  assertPrepared(temporary, 'fast-coder')
  await admitPhysicalExecution(`${root}-override`, 'fast-coder')

  // A preserve request cannot use the peer override as a new base.
  assertPrepared(binding.prepareUserFacing(root, 'deep-coder', false, model), 'deep-coder')
  await admitPhysicalExecution(`${root}-restored`, 'deep-coder')

  binding.observeUserFacingAgent(root, 'fast-coder')
  assertPrepared(binding.prepareUserFacing(root, 'fast-coder', false, modelFor('fast-coder')), 'fast-coder')
  await admitPhysicalExecution(`${root}-switched`, 'fast-coder')

  binding.drop(root)
})

test('WHAT[PID-008] parented_session_uses_stable_agent_lease_and_authorized_peer_only', async () => {
  const parent = 'ses_parent'
  const child = 'ses_child'
  const created = binding.bindChild(parent, child, 'fast-distiller')
  assert.equal(created.ok, true, created.error)

  assertPrepared(binding.prepareManaged(child, 'fast-distiller', false, modelFor('fast-distiller')), 'fast-distiller')
  await admitPhysicalExecution(child, 'fast-distiller')

  const peer = binding.prepareManaged(child, 'deep-distiller', true, modelFor('deep-distiller'))
  assertPrepared(peer, 'deep-distiller')
  await admitPhysicalExecution(`${child}-peer`, 'deep-distiller')

  const foreign = binding.prepareManaged(child, 'fast-coder', true, modelFor('fast-coder'))
  assert.equal(foreign.ok, false)
  assert.match(foreign.error, /not the peer|override/i)

  binding.drop(child)
})

test('WHAT[PID-008] provider_reasoning_variant_must_match_the_exact_lease', async () => {
  const parent = 'ses_variant_parent'
  const child = 'ses_variant_exact'
  assert.equal(binding.bindChild(parent, child, 'deep-distiller').ok, true)

  const physicalId = 'msg-variant-exact'
  const expected = modelFor('deep-distiller')
  const admission = await acquireLease(child, physicalId, 'deep-distiller')
  const target = admission.exact.target
  assert.deepEqual(target, { model: 'test/deep', reasoning: 'high' })
  assert.deepEqual(routing.commitSharedExecutionAdmission(admission.lease, admission.exact), { kind: 'Applied' })

  binding.acceptPromptExecution(child, 'prompt-variant-exact', physicalId, 'deep-distiller', expected)
  assert.equal(binding.beginProviderAttempt(child, physicalId, 'prompt-variant-exact').ok, true)

  const valid = binding.validateObservedProvider(child, 'deep-distiller', expected)
  assert.equal(valid.ok, true)
  assert.equal(valid.value, true)

  const drift = binding.validateObservedProvider(child, 'deep-distiller', { ...expected, variant: 'default' })
  assert.equal(drift.ok, false)
  assert.match(drift.error, /model\/reasoning drift/i)
  assert.match(drift.error, /test\/deep\[high\] -> test\/deep\[default\]/)

  binding.drop(child)
})
