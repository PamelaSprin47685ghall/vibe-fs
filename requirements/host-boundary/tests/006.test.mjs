import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdtemp, mkdir, rm, writeFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test, after } = await import("node:test");
const binding = await import("../../../dist/OpenCode/Host/SessionBindingSurface.js");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");
const { runListenerRefcountScenario } = await import("./support/listener-refcount.mjs");

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
await routing.initialize()
after(async () => {
  if (originalHome === undefined) delete process.env.HOME
  else process.env.HOME = originalHome
  if (originalUserProfile === undefined) delete process.env.USERPROFILE
  else process.env.USERPROFILE = originalUserProfile
  await rm(home, { recursive: true, force: true })
})
const model = { providerID: 'openai', modelID: 'gpt-5' }
const modelFromLease = async (sessionId, physicalUserMessageId, role, participant, lenderSessionId) => {
  const outcome = await routing.acquireSharedExecutionAdmission(
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    lenderSessionId,
  )
  assert.equal(outcome.kind, 'Acquired')
  const target = routing.sharedExecutionAdmissionTarget(outcome.lease)
  const settlement = routing.commitSharedExecutionAdmission(outcome.lease, {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target,
  })
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  const [providerID, ...modelParts] = target.model.split('/')
  return { providerID, modelID: modelParts.join('/'), variant: target.reasoning }
}

test('WHAT[HOST-BOUNDARY-006] HOST-006_user_facing_agent_is_not_session_authority', () => {
  binding.drop('ses_binding_1')
  binding.observeUserFacingAgent('ses_binding_1', 'engineer')
  const prepared = binding.prepareUserFacing('ses_binding_1', 'engineer', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'engineer')
  assert.equal(binding.tryAgent('ses_binding_1'), 'engineer')
})
test('WHAT[HOST-BOUNDARY-006] HOST-006_accept_prompt_execution_binds_physical_prompt_and_provider_model', async () => {
  binding.drop('ses_binding_2')
  const leasedModel = await modelFromLease('ses_binding_2', 'physical-1', 'engineer', 'engineer', undefined)
  binding.acceptPromptExecution('ses_binding_2', 'prompt-1', 'physical-1', 'engineer', leasedModel)
  const began = binding.beginProviderAttempt('ses_binding_2', 'physical-1', 'prompt-1')
  assert.equal(began.ok, true)
  const allowed = binding.validateObservedProvider('ses_binding_2', 'engineer', leasedModel)
  assert.equal(allowed.ok, true)
  assert.equal(allowed.value, true)
})
test('WHAT[HOST-BOUNDARY-006] HOST-006_external_acceptance_immediately_binds_participant', async () => {
  const session = 'ses_binding_external_acceptance'
  binding.drop(session)
  const leasedModel = await modelFromLease(session, 'physical-external', 'engineer', 'engineer', undefined)

  binding.acceptExternalExecution(session, 'physical-external', 'engineer', leasedModel)

  assert.equal(binding.tryAgent(session), 'engineer')
  const allowed = binding.validateObservedProvider(session, 'engineer', leasedModel)
  assert.equal(allowed.ok, true, allowed.error)
  assert.equal(allowed.value, true)
})
test('WHAT[HOST-BOUNDARY-006] HOST-006_provider_drift_is_rejected_after_prompt_binding', () => {
  binding.drop('ses_binding_3')
  binding.acceptPromptExecution('ses_binding_3', 'prompt-1', 'physical-1', 'engineer', model)
  binding.beginProviderAttempt('ses_binding_3', 'physical-1', 'prompt-1')
  const stale = binding.validateObservedProvider('ses_binding_3', 'inspector', model)
  assert.equal(stale.ok, false)
  assert.match(stale.error, /provider agent drift/)
})
test('WHAT[HOST-BOUNDARY-006] HOST-006_stale_physical_terminal_cannot_strip_the_lease_before_chat_params_validation', async () => {
  const session = 'ses_binding_stale_terminal'
  binding.drop(session)

  await modelFromLease(session, 'physical-old', 'engineer', 'engineer', undefined)
  const currentModel = await modelFromLease(session, 'physical-current', 'engineer', 'engineer', undefined)

  routing.releasePhysical(session, 'physical-old')
  binding.acceptPromptExecution(session, 'prompt-current', 'physical-current', 'engineer', currentModel)

  const observed = binding.validateObservedProvider(session, 'engineer', currentModel)
  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.value, true)

  binding.drop(session)
})
test('WHAT[HOST-BOUNDARY-006] HOST-006_managed_prompt_preserves_agent_but_does_not_acquire_model', () => {
  binding.drop('ses_binding_4')
  binding.bindChild('ses_parent_4', 'ses_binding_4', 'engineer')
  const prepared = binding.prepareManaged('ses_binding_4', 'engineer', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'engineer')
  assert.equal(prepared.value.modelProvided, false)
})
test('WHAT[HOST-BOUNDARY-006] HOST-006_child_enqueue_uses_binding_agent_and_model_free_options', () => {
  const created = binding.bindChild('ses_parent', 'ses_child', 'engineer')
  assert.equal(created.ok, true)
  const prepared = binding.prepareManaged('ses_child', 'engineer', false, null)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'engineer')
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const SessionSnapshotSurface = await import("../../../dist/OpenCode/Host/SessionSnapshotSurface.js");

const projectMessages = SessionSnapshotSurface.projectMessages
const locateToolCall = SessionSnapshotSurface.locateToolCall
const toolPartStateAt = SessionSnapshotSurface.toolPartStateAt
const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[HOST-BOUNDARY-006] HOST-004 keeps failed session tool state consistent across Parts and ToolParts', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'error' })])
  const part = toolPartStateAt(messages, 0, 0)
  assert.equal(part.ok, true)
  assert.equal(part.state, 'failed')
})
}
