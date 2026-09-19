import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");

const hash = (value) => `H(${value})`
const personas = {
  engineer: 'Engineer',
  coder: 'Coder',
  manager: 'Lead',
  reviewer: 'Auditor',
  inspector: 'Investigator',
  devops: 'Operator',
}
const rootSelection = (agent) => {
  const role = agent === 'predictor' ? 'inspector' : agent
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: agent,
      role,
      selectedTier: 'deep',
      persona: personas[agent] ?? 'Unknown',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}
const rootFor = (agent = 'engineer', physical = 'msg_u1') => {
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', physical, rootSelection(agent))
  assert.equal(result.ok, true, result.error)
  return result.value
}
const profile = (value) => ({
  session: value.session,
  logicalRun: value.logicalRun,
  authorityRoot: value.authorityRoot,
  authorityKind: value.authorityKind,
  participant: value.participantIdentity.participant,
  role: value.participantIdentity.role,
})
const register = (root) => authority.registerAuthority(root, authority.empty)
const continuation = (key, root, kind = 'ManagerGuard', payload = 'payload') =>
  authority.claimContinuation(key, 'ses_a', kind, root, payload)

test('WHAT[interaction-authority-011] PROMPT_011_logical_run_id_is_stable_and_input_sensitive', () => {
  const id = (runtime, session, physical) => authority.stableLogicalRunId(hash, runtime, session, physical)
  const base = id('rt_1', 'ses_a', 'msg_u1')
  assert.equal(base, 'H(rt_1\nses_a\nmsg_u1)')
  assert.equal(id('rt_1', 'ses_a', 'msg_u1'), base)
  assert.notEqual(id('rt_2', 'ses_a', 'msg_u1'), base)
  assert.notEqual(id('rt_1', 'ses_b', 'msg_u1'), base)
  assert.notEqual(id('rt_1', 'ses_a', 'msg_u2'), base)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const binding = await import("../../../dist/OpenCode/Host/SessionBindingSurface.js");
const chatParams = await import("../../../dist/OpenCode/Host/ChatParamsSurface.js");


test('WHAT[interaction-authority-011] CHAT_PARAMS_parented_session_requires_provider_model_binding', () => {
  binding.bindChild('ses_chat_params_root', 'ses_chat_params_child', 'coder')
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  const rejected = chatParams.apply(
    { sessionID: 'ses_chat_params_child', agent: 'coder', model: { providerID: 'anthropic', modelID: 'fast-haiku' } },
    output,
  )
  assert.equal(rejected.ok, false)
  assert.match(rejected.error, /no observable provider\/model binding|no exact physical execution binding/)
  assert.equal(output.model.modelID, 'fast-haiku')
})
test('WHAT[interaction-authority-011] CHAT_PARAMS_unbound_Host_auxiliary_child_does_not_claim_managed_execution', () => {
  binding.observeHostAuxiliaryChild('ses_chat_params_title')
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  const observed = chatParams.apply(
    { sessionID: 'ses_chat_params_title', agent: 'coder', model: { providerID: 'anthropic', modelID: 'fast-haiku' } },
    output,
  )

  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.temperature, undefined)
  assert.equal(output.model.modelID, 'fast-haiku')
})
test('WHAT[interaction-authority-011] CHAT_PARAMS_acceptance_establishes_binding_without_rewriting_host_model', () => {
  binding.bindChild('ses_chat_params_root_2', 'ses_chat_params_child_2', 'coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_2',
    'pk-chat-params',
    'physical-chat-params',
    'coder',
    { providerID: 'anthropic', modelID: 'deep-opus' },
  )
  const output = { model: { providerID: 'anthropic', modelID: 'deep-opus' } }
  const observed = chatParams.apply(
    { sessionID: 'ses_chat_params_child_2', agent: 'coder', model: { providerID: 'anthropic', modelID: 'deep-opus' } },
    output,
  )
  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.modelID, 'deep-opus')
  assert.equal(observed.temperature, 1)
  assert.equal(output.model.modelID, 'deep-opus')
})
test('WHAT[interaction-authority-011] CHAT_PARAMS_uses_the_resolved_provider_model_id_not_the_mutated_user_message_model', () => {
  binding.bindChild('ses_chat_params_root_3', 'ses_chat_params_child_3', 'coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_3',
    'pk-chat-params-actual-model',
    'physical-chat-params-actual-model',
    'coder',
    { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
  )

  const output = {}
  const observed = chatParams.apply(
    {
      sessionID: 'ses_chat_params_child_3',
      agent: 'coder',
      model: { id: 'fast-haiku', providerID: 'anthropic' },
      message: {
        model: { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
      },
    },
    output,
  )

  assert.equal(observed.ok, false)
  assert.match(observed.error, /model\/reasoning drift/i)
})
test('WHAT[interaction-authority-011] CHAT_PARAMS_accepts_the_real_provider_model_shape_with_message_variant', () => {
  binding.bindChild('ses_chat_params_root_4', 'ses_chat_params_child_4', 'coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_4',
    'pk-chat-params-real-shape',
    'physical-chat-params-real-shape',
    'coder',
    { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
  )

  const inputModel = {
    id: 'deep-opus',
    providerID: 'anthropic',
    capabilities: { temperature: true },
    variants: { high: { reasoning: { effort: 'high' } }, low: {} },
    options: {},
  }
  const output = { options: { existing: 'sentinel' } }

  const observed = chatParams.apply(
    {
      sessionID: 'ses_chat_params_child_4',
      agent: 'coder',
      model: inputModel,
      message: {
        model: { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
      },
    },
    output,
  )

  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.temperature, 1)
  assert.equal(output.temperature, 1)
  assert.equal(output.options.temperature, 1)
  assert.equal(output.options.existing, 'sentinel')
  assert.equal(inputModel.variants.high.temperature, 1)
  assert.equal(inputModel.variants.low.temperature, 1)
  assert.equal(inputModel.options.temperature, 1)
})
test('WHAT[interaction-authority-011] CHAT_PARAMS_leaves_temperature_untouched_when_model_capability_disables_it', () => {
  binding.bindChild('ses_chat_params_root_5', 'ses_chat_params_child_5', 'coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_5',
    'pk-chat-params-reasoning-shape',
    'physical-chat-params-reasoning-shape',
    'coder',
    { providerID: 'openai', modelID: 'o3-mini', variant: 'high' },
  )

  const inputModel = {
    id: 'o3-mini',
    providerID: 'openai',
    capabilities: { temperature: false },
    variants: { high: {} },
  }
  const output = { options: {} }
  const observed = chatParams.apply(
    {
      sessionID: 'ses_chat_params_child_5',
      agent: 'coder',
      model: inputModel,
      message: {
        model: { providerID: 'openai', modelID: 'o3-mini', variant: 'high' },
      },
    },
    output,
  )

  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.temperature, undefined)
  assert.equal(output.temperature, undefined)
  assert.equal(output.options.temperature, undefined)
  assert.equal(inputModel.variants.high.temperature, undefined)
})
test('WHAT[interaction-authority-011] CHAT_PARAMS_agentless_root_does_not_invent_binding', () => {
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  const observed = chatParams.apply({ sessionID: 'ses_unbound_root' }, output)
  assert.equal(observed.ok, true)
  assert.equal(observed.temperature, undefined)
  assert.equal(output.model.modelID, 'fast-haiku')
})
}
