// Split from tests/unit/invariants/prompt-stability.test.mjs (cutover Wave 2a); owner: prefix-stability
//
// System-prompt-byte half of ARCH-016 Gate D: one session keeps its system
// prompt bytes, prompt ids and catalog bytes stable across fallback peer
// switch / T1 review / reanchor. The persona-binding half of the same
// scenario lives in participant-identity/tests/persona-binding.test.mjs.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import { Role } from '../../../dist/Kernel/Roles.js'
import { systemPromptIdFor } from '../../../dist/Domain/PromptAuthority.js'
import { SystemPromptIdModule_value as promptIdValue } from '../../../dist/Kernel/Identity.js'
import {
  attemptPlanner as planner,
  cursor,
  projectionAlgebra,
  projectionIntent,
  projectionSnapshot,
  promptOrigin,
  promptResources,
  providerProjection,
  requestKind,
  reviewChallenge,
  rootKind,
  toList,
} from '../../verification-system/tests/support/domain.mjs'

const roleCatalogBytes = (catalog) => ({
  ManagerSystemPrompt: catalog.ManagerSystemPrompt,
  CoderSystemPrompt: catalog.CoderSystemPrompt,
  DevopsSystemPrompt: catalog.DevopsSystemPrompt,
  InspectorSystemPrompt: catalog.InspectorSystemPrompt,
  ReviewerSystemPrompt: catalog.ReviewerSystemPrompt,
  BrowserSystemPrompt: catalog.BrowserSystemPrompt,
  InquirySystemPrompt: catalog.InquirySystemPrompt,
  OrchestratorSystemPrompt: catalog.OrchestratorSystemPrompt,
  DistillerSystemPrompt: catalog.DistillerSystemPrompt,
  BloggerSystemPrompt: catalog.BloggerSystemPrompt,
})

const assertSystemFrozen = ({ catalog, managerPromptId, reviewerPromptId }) => {
  assert.deepEqual(roleCatalogBytes(promptResources.load()), catalog)
  assert.equal(promptIdValue(systemPromptIdFor(Role.Manager)), managerPromptId)
  assert.equal(promptIdValue(systemPromptIdFor(Role.Reviewer)), reviewerPromptId)
}

test('PROMPT_STABILITY_gate_d_is_wired_in_verify_contract', () => {
  const what = readFileSync(new URL('../WHAT.md', import.meta.url), 'utf8')
  assert.match(what, /system-prompt-stability\.test\.mjs/)
  assert.match(what, /byte-identical/)
  assert.match(what, /Peer Fallback/)
})

test('PROMPT_STABILITY_fallback_peer_switch_keeps_system_prompt_bytes', () => {
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

test('PROMPT_STABILITY_t1_review_reanchor_keep_system_prompt_bytes', async () => {
  const { managerNarrative } = await import('../../verification-system/tests/support/glory.mjs')

  const catalogBefore = roleCatalogBytes(promptResources.load())
  assert.ok(catalogBefore.ManagerSystemPrompt.length > 0)
  assert.ok(catalogBefore.ReviewerSystemPrompt.length > 0)
  const managerPromptId = promptIdValue(systemPromptIdFor(Role.Manager))
  const reviewerPromptId = promptIdValue(systemPromptIdFor(Role.Reviewer))
  assert.equal(managerPromptId, 'manager')
  assert.equal(reviewerPromptId, 'reviewer')

  const managerAuthority = planner.authority({
    role: 'Manager',
    selected: 'fast-manager',
    peer: 'deep-manager',
  })
  const profileBefore = planner.plan({
    authorityProfile: managerAuthority,
    cursor: cursor.atOffset(0),
    kind: requestKind.workMain,
  })
  assert.equal(profileBefore.systemPromptId, managerPromptId)

  // T1 — entrustment rides conversation tool result only (TODO-015 / GLORY-075).
  const t1Conversation = managerNarrative.wrapT1AcceptedResult('checkpoint body')
  assert.match(t1Conversation, /The account has been accepted/)
  assert.match(t1Conversation, /The Manager who will carry it is you/)
  assert.ok(t1Conversation.includes('checkpoint body'))
  assert.notEqual(t1Conversation, catalogBefore.ManagerSystemPrompt)
  assert.doesNotMatch(catalogBefore.ManagerSystemPrompt, /The account has been accepted/)
  assert.doesNotMatch(catalogBefore.ManagerSystemPrompt, /checkpoint body/)
  assertSystemFrozen({
    catalog: catalogBefore,
    managerPromptId,
    reviewerPromptId,
  })

  // review — AppendReviewChallenge injects conversation bytes; system catalog untouched.
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }]
  const wire = providerProjection.decodeMessageView(toList(raw)).Messages
  const reviewSnapshot = projectionSnapshot.of({
    currentProjection: providerProjection.toSemantic(providerProjection.decodeMessageView(toList(raw))),
    blogFrames: [],
    transportMessages: [],
  })
  const reviewIntent = projectionIntent.appendReviewChallenge({ TextVersion: reviewChallenge.textVersion })
  const reviewPlan = projectionAlgebra.plan([reviewIntent])
  assert.equal(reviewPlan.ok, true)
  assert.deepEqual(reviewPlan.intents, ['AppendReviewChallenge'])
  const reviewView = projectionAlgebra.renderMessagesWithIntents(reviewSnapshot, wire, [reviewIntent])
  const reviewTexts = reviewView.flatMap((m) => m.parts.map((p) => p.text)).filter(Boolean)
  assert.equal(
    reviewTexts.some((t) => t === reviewChallenge.prompt || t.includes(reviewChallenge.text)),
    true,
    'review must append challenge conversation bytes',
  )
  assert.equal(
    reviewTexts.includes(catalogBefore.ManagerSystemPrompt),
    false,
    'review must not swap Manager system catalog into conversation',
  )
  assert.equal(
    reviewTexts.includes(catalogBefore.ReviewerSystemPrompt),
    false,
    'review must not swap Reviewer system catalog into conversation',
  )
  const reviewProfile = planner.plan({
    authorityProfile: planner.authority({
      role: 'Reviewer',
      selected: 'fast-reviewer',
      peer: 'deep-reviewer',
    }),
    cursor: cursor.atOffset(0),
    kind: requestKind.workMain,
    origin: promptOrigin.continuation('ReviewConfirmation'),
  })
  assert.equal(reviewProfile.systemPromptId, reviewerPromptId)
  assertSystemFrozen({
    catalog: catalogBefore,
    managerPromptId,
    reviewerPromptId,
  })

  // reanchor — Host compaction intent is a wire no-op; catalog / prompt id stay.
  const reanchorRaw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'before' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'after' }] },
  ]
  const reanchorWire = providerProjection.decodeMessageView(toList(reanchorRaw)).Messages
  const reanchorSnapshot = projectionSnapshot.of({
    currentProjection: providerProjection.toSemantic(providerProjection.decodeMessageView(toList(reanchorRaw))),
    blogFrames: [],
    transportMessages: [],
    hostReanchor: projectionSnapshot.hostReanchor(),
  })
  const reanchorIntent = projectionIntent.reanchorAfterCompaction
  const reanchorPlan = projectionAlgebra.plan([reanchorIntent])
  assert.equal(reanchorPlan.ok, true)
  assert.deepEqual(reanchorPlan.intents, ['ReanchorAfterCompaction'])
  const reanchorView = projectionAlgebra.renderMessagesWithIntents(reanchorSnapshot, reanchorWire, [reanchorIntent])
  assert.deepEqual(
    reanchorView.map((m) => m.parts[0]?.text),
    ['before', 'after'],
    'reanchor must not rewrite conversation wire bytes',
  )
  const profileAfter = planner.plan({
    authorityProfile: managerAuthority,
    cursor: cursor.atOffset(0),
    kind: requestKind.workMain,
    origin: promptOrigin.authorityRoot(rootKind.human),
  })
  assert.equal(profileAfter.systemPromptId, profileBefore.systemPromptId)
  assertSystemFrozen({
    catalog: catalogBefore,
    managerPromptId,
    reviewerPromptId,
  })
})
