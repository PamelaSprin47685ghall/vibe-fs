import assert from 'node:assert/strict'
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test, { after } from 'node:test'

const originalHome = process.env.HOME
const originalUserProfile = process.env.USERPROFILE
const home = await mkdtemp(join(tmpdir(), 'wanxiangshu-host-binding-home-'))
process.env.HOME = home
process.env.USERPROFILE = home
await mkdir(join(home, '.config', 'opencode'), { recursive: true })
await writeFile(
  join(home, '.config', 'opencode', 'wanxiangshu.mjs'),
  `
export default function route(role) {
  return { model: 'test/system', reasoning: 'none' }
}
`,
  'utf8',
)

import * as binding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { runListenerRefcountScenario } from './support/listener-refcount.mjs'

await routing.initialize()

after(async () => {
  if (originalHome === undefined) delete process.env.HOME
  else process.env.HOME = originalHome
  if (originalUserProfile === undefined) delete process.env.USERPROFILE
  else process.env.USERPROFILE = originalUserProfile
  await rm(home, { recursive: true, force: true })
})

const model = { providerID: 'openai', modelID: 'gpt-5' }
const modelFromLease = async (sessionId, physicalUserMessageId, agent) => {
  const outcome = await routing.acquireSharedExecutionAdmission(
    sessionId,
    physicalUserMessageId,
    agent,
  )
  assert.equal(outcome.kind, 'Acquired')
  const target = routing.sharedExecutionAdmissionTarget(outcome.lease)
  const settlement = routing.commitSharedExecutionAdmission(outcome.lease, {
    sessionId,
    physicalUserMessageId,
    effectiveAgent: agent,
    target,
  })
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  const [providerID, ...modelParts] = target.model.split('/')
  return { providerID, modelID: modelParts.join('/'), variant: target.reasoning }
}

test('WHAT[HOST-BOUNDARY-006] HOST-006_user_facing_agent_is_not_session_authority', () => {
  binding.drop('ses_binding_1')
  binding.observeUserFacingAgent('ses_binding_1', 'coder')
  const prepared = binding.prepareUserFacing('ses_binding_1', 'coder', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'coder')
  assert.equal(binding.tryAgent('ses_binding_1'), 'coder')
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_accept_prompt_execution_binds_physical_prompt_and_provider_model', async () => {
  binding.drop('ses_binding_2')
  const leasedModel = await modelFromLease('ses_binding_2', 'physical-1', 'coder')
  binding.acceptPromptExecution('ses_binding_2', 'prompt-1', 'physical-1', 'coder', leasedModel)
  const began = binding.beginProviderAttempt('ses_binding_2', 'physical-1', 'prompt-1')
  assert.equal(began.ok, true)
  const allowed = binding.validateObservedProvider('ses_binding_2', 'coder', leasedModel)
  assert.equal(allowed.ok, true)
  assert.equal(allowed.value, true)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_external_acceptance_immediately_binds_effective_agent', async () => {
  const session = 'ses_binding_external_acceptance'
  binding.drop(session)
  const leasedModel = await modelFromLease(session, 'physical-external', 'coder')

  binding.acceptExternalExecution(session, 'physical-external', 'coder', leasedModel)

  assert.equal(binding.tryAgent(session), 'coder')
  const allowed = binding.validateObservedProvider(session, 'coder', leasedModel)
  assert.equal(allowed.ok, true, allowed.error)
  assert.equal(allowed.value, true)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_provider_drift_is_rejected_after_prompt_binding', () => {
  binding.drop('ses_binding_3')
  binding.acceptPromptExecution('ses_binding_3', 'prompt-1', 'physical-1', 'coder', model)
  binding.beginProviderAttempt('ses_binding_3', 'physical-1', 'prompt-1')
  const stale = binding.validateObservedProvider('ses_binding_3', 'inspector', model)
  assert.equal(stale.ok, false)
  assert.match(stale.error, /provider agent drift/)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_stale_physical_terminal_cannot_strip_the_lease_before_chat_params_validation', async () => {
  const session = 'ses_binding_stale_terminal'
  binding.drop(session)

  await modelFromLease(session, 'physical-old', 'coder')
  const currentModel = await modelFromLease(session, 'physical-current', 'coder')

  routing.releasePhysical(session, 'physical-old')
  binding.acceptPromptExecution(session, 'prompt-current', 'physical-current', 'coder', currentModel)

  const observed = binding.validateObservedProvider(session, 'coder', currentModel)
  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.value, true)

  binding.drop(session)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_managed_prompt_preserves_agent_but_does_not_acquire_model', () => {
  binding.drop('ses_binding_4')
  binding.bindChild('ses_parent_4', 'ses_binding_4', 'coder')
  const prepared = binding.prepareManaged('ses_binding_4', 'coder', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'coder')
  assert.equal(prepared.value.modelProvided, false)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_child_enqueue_uses_binding_agent_and_model_free_options', () => {
  const created = binding.bindChild('ses_parent', 'ses_child', 'coder')
  assert.equal(created.ok, true)
  const prepared = binding.prepareManaged('ses_child', 'coder', false, null)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'coder')
  assert.equal(prepared.value.modelProvided, false)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_private_bookkeeper_child_stays_outside_managed_execution_binding', () => {
  const child = 'ses_binding_bookkeeper_child'
  binding.drop(child)

  const created = binding.bindChild('ses_binding_bookkeeper_parent', child, 'bookkeeper')
  assert.equal(created.ok, true, created.error)
  assert.equal(binding.tryAgent(child), '')
  assert.equal(binding.isUnboundHostAuxiliaryChild(child), true)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_terminal_listener_refcounts_do_not_share_disposal', () => {
  const observed = runListenerRefcountScenario()
  assert.equal(observed.afterOneDisposeFatal, true)
  assert.equal(observed.afterAllDisposeFatal, false)
  assert.deepEqual(observed.sends, [])
})
