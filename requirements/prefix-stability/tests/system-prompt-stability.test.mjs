// PREFIX-STABILITY-002 / PROMPT-019 — system-prompt identity is role-owned,
// while attempt tier/cursor metadata selects the effective agent and capabilities.
// The registered Authority, Planner, Strength and Delegation surfaces are the
// only boundaries this proof needs; no Fable Role/Identity representation crosses.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/Surface.js'
import * as planner from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import * as delegation from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as strength from '../../../dist/Strength/Surface.js'
import { installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

installDefaultResources()

const OWNER = 'ses_owner_prompt'
const roles = ['Manager', 'Coder', 'Inspector', 'Reviewer', 'Browser', 'Inquiry', 'Distiller', 'Blogger']
const profile = (role, tier = 'Fast', kind = 'WorkMain') => planner.plan({ role, tier, kind })

// ── PROMPT-019: resources are distinct from their role identity ───────────

test('WHAT[PREFIX-STABILITY-002] PROMPT_019_each_canonical_role_has_one_stable_prompt_identity', () => {
  const ids = new Set()
  for (const role of roles) {
    const id = authority.systemPromptIdForRole(role)
    const resource = strength.systemPromptForRole(role)
    const fast = profile(role, 'Fast')
    const deep = profile(role, 'Deep')

    assert.equal(typeof id, 'string')
    assert.ok(id.length > 0)
    assert.equal(fast.ok, true)
    assert.equal(deep.ok, true)
    assert.equal(fast.systemPromptId, id)
    assert.equal(deep.systemPromptId, id)
    assert.equal(typeof resource, 'string')
    assert.ok(resource.length > 0, `${role} resource must not be empty`)
    assert.equal(resource.includes(`system_prompt_id = "${id}"`), false, 'resource bytes must not smuggle the identity field')
    ids.add(id)
  }
  assert.equal(ids.size, roles.length, 'canonical roles must not alias prompt identities')
})

test('WHAT[PREFIX-STABILITY-002] PROMPT_019_effective_agent_changes_by_tier_not_prompt_identity', () => {
  const managerFast = delegation.vocabulary('Manager', 'Fast', OWNER)
  const managerDeep = delegation.vocabulary('Manager', 'Deep', OWNER)
  const reviewerFast = delegation.vocabulary('Reviewer', 'Fast', OWNER)
  const reviewerDeep = delegation.vocabulary('Reviewer', 'Deep', OWNER)

  assert.notEqual(managerFast.agent, managerDeep.agent)
  assert.notEqual(reviewerFast.agent, reviewerDeep.agent)
  assert.equal(profile('Manager', 'Fast').systemPromptId, profile('Manager', 'Deep').systemPromptId)
  assert.equal(profile('Reviewer', 'Fast').systemPromptId, profile('Reviewer', 'Deep').systemPromptId)
})

test('WHAT[PREFIX-STABILITY-002] PROMPT_019_role_identity_does_not_inherit_attempt_cursor_or_replica_metadata', () => {
  const manager = profile('Manager', 'Fast')
  const reviewer = profile('Reviewer', 'Fast')

  // Authority owns prompt IDs; the derived attempt profile owns request kind and
  // capabilities. Neither surface accepts a cursor or a replica id as an identity
  // input, so changing those lifecycle facts cannot change the role identity.
  assert.equal(manager.systemPromptId, authority.systemPromptIdForRole('Manager'))
  assert.equal(reviewer.systemPromptId, authority.systemPromptIdForRole('Reviewer'))
  assert.equal('cursor' in manager, false)
  assert.equal('replicaId' in manager, false)
  assert.match(manager.requestKind, /^work-?main$/i)
  assert.match(reviewer.requestKind, /^work-?main$/i)
})

test('WHAT[PREFIX-STABILITY-002] PROMPT_019_role_and_tier_capabilities_remain_explicit', () => {
  const managerFast = profile('Manager', 'Fast')
  const managerDeep = profile('Manager', 'Deep')
  const inspectorFast = profile('Inspector', 'Fast')

  assert.ok(managerFast.toolCapabilities.length > 0)
  assert.ok(managerDeep.toolCapabilities.length > 0)
  assert.ok(inspectorFast.toolCapabilities.length > 0)
  assert.deepEqual(managerFast.toolCapabilities, managerDeep.toolCapabilities)
  assert.notDeepEqual(managerFast.toolCapabilities, inspectorFast.toolCapabilities)
})
