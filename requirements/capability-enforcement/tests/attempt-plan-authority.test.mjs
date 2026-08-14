// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: capability-enforcement.
//
// ENF-001 / ENF-003 / ENF-004: the AttemptExecutionProfile is the single origin
// of a request's role, tools and request kind — everything derivable is derived
// from the authority, the request kind is carried not inferred, and the tier
// never reaches the system prompt or the tool set.

import assert from 'node:assert/strict'
import test from 'node:test'
import { attemptPlanner as planner, requestKind } from '../../verification-system/tests/support/domain.mjs'

// ── PROMPT-008: everything derivable is derived ────────────────────────────

test('PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority', () => {
  // The caller supplies an authority profile and a cursor. It cannot supply a role that
  // disagrees with the agent name, or a tool set that disagrees with the role, because
  // neither is a parameter.
  const plan = planner.plan({ kind: requestKind.workMain })

  assert.equal(plan.canonicalRole, 'Coder')
  assert.equal(plan.systemPromptId, 'coder', 'AGENT-001: derived from the role alone')
  assert.deepEqual(plan.toolCapabilities, ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Inspect', 'Move', 'Read', 'Remove', 'Write'])
})

test('AGENT_010_the_tier_does_not_reach_the_system_prompt_or_the_tool_set', () => {
  // `permissions(fast-coder) = permissions(deep-coder)` must be structurally true, not
  // a coincidence of two lookup tables agreeing.
  const fast = planner.plan({
    authorityProfile: planner.authority({ selected: 'fast-coder', peer: 'deep-coder', tier: 'Fast' }),
    kind: requestKind.workMain,
  })

  const deep = planner.plan({
    authorityProfile: planner.authority({ selected: 'deep-coder', peer: 'fast-coder', tier: 'Deep' }),
    kind: requestKind.workMain,
  })

  assert.equal(fast.systemPromptId, deep.systemPromptId)
  assert.deepEqual(fast.toolCapabilities, deep.toolCapabilities)
})

test('PROMPT_008_the_request_kind_is_carried_not_inferred', () => {
  for (const kind of requestKind.all) {
    const plan = planner.plan({ kind })
    assert.equal(plan.requestKind, requestKind.nameOf(kind))
  }
})
