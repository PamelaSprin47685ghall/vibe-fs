// ARCH-016 Gate D — same session must keep system prompt bytes stable across fallback/T1/review/reanchor/Strength.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import { AgentTier, Role } from '../../../dist/Kernel/Roles.js'
import { systemPromptIdFor } from '../../../dist/Domain/PromptAuthority.js'
import { SystemPromptIdModule_value as promptIdValue } from '../../../dist/Kernel/Identity.js'
import * as PersonaCatalog from '../../../dist/Domain/PersonaCatalog.js'
import {
  attemptPlanner as planner,
  cursor,
  promptResources,
  requestKind,
  resultOf,
  sessionId,
  unwrapOption,
} from '../support/domain.mjs'

test('PROMPT_STABILITY_gate_d_is_wired_in_verify_contract', () => {
  const verify = readFileSync(new URL('../../../docs/proof/verify.md', import.meta.url), 'utf8')
  assert.match(verify, /prompt-stability\.test\.mjs/)
  assert.match(verify, /Gate D/)
  assert.match(verify, /system prompt 字节相同/)
})

test('PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes', () => {
  const managerBytes = promptResources.load().ManagerSystemPrompt
  assert.ok(managerBytes.length > 0)

  const managerPromptId = promptIdValue(systemPromptIdFor(Role.Manager))
  assert.equal(managerPromptId, 'manager')
  assert.doesNotMatch(managerPromptId, /fast|deep/i)

  const authority = planner.authority({
    role: 'Manager',
    selected: 'fast-manager',
    peer: 'deep-manager',
  })

  const profiles = [0, 1, 2, 3].map((offset) =>
    planner.plan({
      authorityProfile: authority,
      cursor: cursor.atOffset(offset),
      kind: requestKind.workMain,
    }),
  )

  assert.deepEqual(
    profiles.map((profile) => profile.systemPromptId),
    ['manager', 'manager', 'manager', 'manager'],
  )
  assert.deepEqual(
    profiles.map((profile) => profile.effectiveAgent),
    ['fast-manager', 'fast-manager', 'deep-manager', 'deep-manager'],
  )
  assert.deepEqual(
    profiles.map((profile) => profile.toolCapabilities),
    profiles.map(() => profiles[0].toolCapabilities),
  )

  const replica = planner.plan({
    authorityProfile: authority,
    cursor: cursor.atOffset(2),
    kind: requestKind.of('StrengthReplica'),
  })
  assert.equal(replica.systemPromptId, 'manager')
  assert.equal(replica.effectiveAgent, 'deep-manager')

  assert.equal(promptResources.load().ManagerSystemPrompt, managerBytes)

  PersonaCatalog.SessionPersona_clearAllForTests()
  const owner = sessionId('ses_gate_d')
  const bound = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, 'Coordinator'))
  assert.equal(bound.ok, true)
  assert.equal(unwrapOption(PersonaCatalog.SessionPersona_tryGet(owner)), 'Coordinator')
  assert.notEqual(
    PersonaCatalog.PersonaCatalog_persona(Role.Manager, AgentTier.Deep),
    PersonaCatalog.PersonaCatalog_persona(Role.Manager, AgentTier.Fast),
  )
  const replay = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, 'Coordinator'))
  assert.equal(replay.ok, true)
  assert.equal(unwrapOption(PersonaCatalog.SessionPersona_tryGet(owner)), 'Coordinator')

  const coderBytes = promptResources.load().CoderSystemPrompt
  assert.equal(promptIdValue(systemPromptIdFor(Role.Coder)), 'coder')
  assert.doesNotMatch(coderBytes, /strength|replica|prefetch/i)

  const coderAuthority = planner.authority({
    role: 'Coder',
    selected: 'fast-coder',
    peer: 'deep-coder',
  })
  const coderProfiles = [0, 2].map((offset) =>
    planner.plan({
      authorityProfile: coderAuthority,
      cursor: cursor.atOffset(offset),
      kind: requestKind.workMain,
    }),
  )
  assert.equal(coderProfiles[0].systemPromptId, coderProfiles[1].systemPromptId)
  assert.notEqual(coderProfiles[0].effectiveAgent, coderProfiles[1].effectiveAgent)
})
