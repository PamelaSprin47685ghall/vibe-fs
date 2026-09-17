import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const turns = await import("../../../dist/Interaction/Repair/CompletedTurnSurface.js");

const text = (value) => ({ type: 'text', text: value })
const reasoning = (value) => ({ type: 'reasoning', text: value })
const toolCall = (callID, tool, args) => ({ type: 'tool-call', callID, tool, args })
const toolResult = (callID, result) => ({ type: 'tool-result', callID, result })
const activity = (kind) => ({ type: kind })
const classify = (completed, finish, errorName, parts = []) => turns.classifyOutcome(completed, finish, errorName, parts)

test('WHAT[PAR-008] RECON_formal_content_gate_is_shared_with_terminal_validity', () => {
  assert.equal(turns.formalContentUnusable(null), true)
  assert.equal(turns.formalContentUnusable([]), true)
  assert.equal(turns.formalContentUnusable([reasoning('only thoughts')]), true)
  assert.equal(turns.formalContentUnusable([text('   ')]), true)
  assert.equal(turns.formalContentUnusable([text('<tool_call>read</tool_call>')]), true)
  assert.equal(turns.formalContentUnusable([text('a real answer')]), false)
  assert.equal(turns.formalContentUnusable([text('a real answer'), reasoning('and thinking')]), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const intent = await import("../../../dist/OpenCode/Host/ChatAdmission/IntentSurface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");

const hash = (value) => `H(${value})`
const personas = {
  engineer: 'Engineer',
  coder: 'Coder',
  manager: 'Lead',
  reviewer: 'Auditor',
  inspector: 'Investigator',
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
const inheritedSelection = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    hash,
    'rt_owner',
    'ses_owner',
    'HumanRoot',
    `owner_${physical}`,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}
const rootFor = (agent = 'engineer', physical = 'msg_u1', kind = 'HumanRoot') => {
  const seed = kind === 'AgentOwnerRoot' ? inheritedSelection(agent, physical) : rootSelection(agent)
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', kind, physical, seed)
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

test('WHAT[INTERACTION-AUTHORITY-008] IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root', () => {
  const root = rootFor('engineer', 'msg_u1', 'AgentOwnerRoot')
  let state = register(root)

  const claimed = authority.claimContinuation('pk_claimed', 'ses_a', 'ManagerGuard', root, 'pd-c')
  state = authority.registerClaim(claimed, state)
  const accepted = authority.claimContinuation('pk_accepted', 'ses_a', 'BusyAgentNudge', root, 'pd-a')
  state = authority.registerClaim(accepted, state)
  state = authority.acceptClaim('pk_accepted', 'msg_accepted', state)

  assert.deepEqual(
    {
      accepted: authority.resolveKnownOrigin('msg_accepted', '', false, state),
      claimed: authority.resolveKnownOrigin('msg_unseen', 'pk_claimed', false, state),
      compaction: authority.resolveKnownOrigin('msg_unseen', '', true, state),
      registeredRoot: authority.resolveKnownOrigin('msg_unseen', 'pk_unknown', false, state),
      nothing: authority.resolveKnownOrigin('msg_unseen', '', false, state),
    },
    {
      accepted: 'Continuation',
      claimed: 'Continuation',
      compaction: 'HostInternal',
      registeredRoot: 'AuthorityRoot',
      nothing: 'UnknownOrigin',
    },
  )
})
test('WHAT[INTERACTION-AUTHORITY-008] IA_008_accepted_continuation_outranks_compaction', () => {
  const root = rootFor()
  let state = register(root)
  state = authority.registerClaim(
    authority.claimContinuation('pk_both', 'ses_a', 'ManagerGuard', root, 'pd-b'),
    state,
  )
  state = authority.acceptClaim('pk_both', 'msg_both', state)
  assert.equal(authority.resolveKnownOrigin('msg_both', '', true, state), 'Continuation')
})
}
