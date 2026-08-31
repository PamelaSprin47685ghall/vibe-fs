// Split from tests/unit/journal/envelope.test.mjs (cutover Wave 2a); owner: dispatch-protocol.
//
// DISPATCH-PROTOCOL-002: RuntimeStarted advances one workspace watermark;
// claims retain the watermark observed at registration for audit only.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

const inheritedIdentitySeed = (session) => ({
  kind: 'InheritedFromOwner',
  ownerSession: `${session}-owner`,
  ownerLogicalRun: `run-${session}-owner`,
  ownerAuthorityRoot: `root-${session}-owner`,
  participantIdentity: {
    selectedAgent: 'fast-coder',
    peerAgent: 'deep-coder',
    canonicalRole: 'coder',
    selectedTier: 'fast',
    persona: 'Coordinator',
    personaCatalogVersion: 1,
    origin: 'InheritedFromOwner',
  },
})

const claim = (session, key, seq) => ({
  kind: 'claim',
  seq,
  runtime: 'rt-claims',
  session,
  promptKey: key,
  continuationKind: 'ManagerGuard',
  logicalRun: `run-${seq}`,
  authorityRoot: `root-${seq}`,
  effectiveAgent: 'fast-coder',
  identitySeed: inheritedIdentitySeed(session),
  payloadDigest: `pd-${seq}`,
})

const started = (seq, runtime) => ({ kind: 'runtime-start', seq, runtime })
const findClaim = (claims, key) => claims.find((value) => value.promptKey === key)

test('WHAT[DISPATCH-PROTOCOL-002] PROMPT_011_RuntimeStarted_advances_a_workspace_watermark_not_every_session', () => {
  const folded = dispatch.foldRuntimeStartWatermark([
    claim('ses_a', 'pk_a', 1),
    claim('ses_b', 'pk_b', 2),
    started(3, 'rt-1'),
    started(4, 'rt-2'),
    claim('ses_a', 'pk_late', 5),
    started(6, 'rt-3'),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const projections = folded.value
  assert.equal(projections.runtimeStartCount, 3)

  const earlyA = findClaim(projections.claims, 'pk_a')
  const earlyB = findClaim(projections.claims, 'pk_b')
  const lateA = findClaim(projections.claims, 'pk_late')

  assert.equal(earlyA.claimedAtRuntimeStartCount, 0)
  assert.equal(earlyB.claimedAtRuntimeStartCount, 0)
  assert.equal(lateA.claimedAtRuntimeStartCount, 2)
  assert.deepEqual(dispatch.runtimeStartPolicy(), {
    claimStamp: 'workspace-runtime-start-count',
    advancesWorkspaceWatermark: true,
    restartRecoveryAuthority: false,
  })
})
