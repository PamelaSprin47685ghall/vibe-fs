// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: capability-enforcement.
//
// ENF-001 / ENF-003 / ENF-004: the AttemptExecutionProfile is the single origin
// of a request's role, tools and request kind — everything derivable is derived
// from the authority, the request kind is carried not inferred, and the tier
// never reaches the system prompt or the tool set.

import assert from 'node:assert/strict'
import test from 'node:test'
import { plan } from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'

// ── PROMPT-008: everything derivable is derived ────────────────────────────

test('WHAT[ENF-001] PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority', () => {
  // The caller supplies only the canonical role, tier and request kind. The
  // owner surface constructs the profile through AttemptPlanner.plan.
  const planned = plan({ role: 'coder', tier: 'fast', kind: 'work-main' })

  assert.equal(planned.ok, true, planned.error)
  assert.equal(planned.canonicalRole, 'coder')
  assert.equal(planned.systemPromptId, 'coder', 'AGENT-001: derived from the role alone')
  assert.deepEqual(planned.toolCapabilities, [
    'BashHoneypot',
    'Edit',
    'Fetch',
    'Fission',
    'Glob',
    'Grep',
    'Inspect',
    'Move',
    'Read',
    'Remove',
    'Write',
  ])
})

test('WHAT[ENF-004] AGENT_010_the_tier_does_not_reach_the_system_prompt_or_the_tool_set', () => {
  const fast = plan({ role: 'coder', tier: 'fast', kind: 'work-main' })
  const deep = plan({ role: 'coder', tier: 'deep', kind: 'work-main' })

  assert.equal(fast.ok, true, fast.error)
  assert.equal(deep.ok, true, deep.error)
  assert.equal(fast.systemPromptId, deep.systemPromptId)
  assert.deepEqual(fast.toolCapabilities, deep.toolCapabilities)
})

test('WHAT[ENF-003] PROMPT_008_the_request_kind_is_carried_not_inferred', () => {
  for (const kind of ['work-main', 'blogger-main', 'blogger-squash', 'interaction-repair']) {
    const planned = plan({ role: 'coder', tier: 'fast', kind })
    assert.equal(planned.ok, true, planned.error)
    assert.equal(planned.requestKind, kind)
  }
})
