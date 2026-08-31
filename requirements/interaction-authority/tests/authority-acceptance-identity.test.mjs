import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as obligationJournal from '../../../dist/Persistence/Journal/ObligationJournalSurface.js'

const participantIdentity = {
  selectedAgent: 'fast-manager',
  peerAgent: 'deep-manager',
  canonicalRole: 'manager',
  selectedTier: 'fast',
  persona: 'Coordinator',
  personaCatalogVersion: 1,
  origin: 'ResolvedAtRoot',
}

const rootSelection = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity,
}

const hostPort = (sendPrompt) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: sendPrompt,
})

const withJournal = async (label, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-authority-acceptance-${label}-`))
  const opened = await journal.JournalSurface_bootWithWriterId(
    directory,
    `writer-${label}`,
    `runtime-${label}`,
    4242,
    '2026-08-30T00:00:00Z',
  )
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  try {
    await action(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

const acceptOwner = async (handle, session = 'ses-owner') => {
  const accepted = await dispatch.acceptHumanRootSelection(
    handle,
    session,
    `msg-${session}`,
    rootSelection,
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)
  return accepted.profile
}

const inheritedSeed = (owner, child = 'fast-coder') => {
  const issued = authority.issueInheritedIdentitySeed(child, owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  return issued.value
}

const completeManagerLife = async (handle, session) => {
  const lifeId = `life-${session}`
  const opened = await obligationJournal.appendManagerLifecycle(handle, session, 'LifeOpened', {
    sessionId: session,
    lifeId,
    openingCursorSequence: 0,
    openingTextDigest: 'digest-opening',
    openingTextRef: 'blob-opening',
    openingUserMessageId: `msg-${session}`,
  })
  assert.equal(opened.ok, true, opened.ok ? '' : opened.error)
  const completed = await obligationJournal.appendManagerLifecycle(handle, session, 'LifeCompleted', {
    sessionId: session,
    lifeId,
    requestId: `finality-${session}`,
    terminalRef: `terminal-${session}`,
    terminalDigest: `digest-terminal-${session}`,
  })
  assert.equal(completed.ok, true, completed.ok ? '' : completed.error)
}

test('WHAT[INTERACTION-AUTHORITY-006] HumanRoot missing identity seed is rejected without authority', async () => {
  await withJournal('human-missing', async (handle) => {
    const result = await dispatch.acceptHumanRootSelection(handle, 'ses-human-missing', 'msg-human-missing', null)

    assert.equal(result.ok, false)
    assert.equal(result.error.kind, 'IdentityRejected')
    assert.match(result.error.reason, /explicit root-selection identity seed/i)
    assert.equal(dispatch.projectionObservation(handle, 'ses-human-missing').activeLogicalRun, null)
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] HumanRoot persists identity before provider work and returns the exact profile', async () => {
  await withJournal('human-explicit', async (handle) => {
    const result = await dispatch.acceptHumanRootSelection(handle, 'ses-human-explicit', 'msg-human-explicit', rootSelection)
    const projection = dispatch.projectionObservation(handle, 'ses-human-explicit')

    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.deepEqual(result.profile.identitySeed, rootSelection)
    assert.deepEqual(projection.activeLogicalRun, result.profile)
  })
})

test('WHAT[INTERACTION-AUTHORITY-015] matching external user ingress continues without replacing the active authority run', async () => {
  await withJournal('external-while-active', async (handle) => {
    const active = await acceptOwner(handle, 'ses-external-while-active')
    const before = dispatch.projectionObservation(handle, 'ses-external-while-active')

    const ingress = await dispatch.acceptManagedExternal(
      handle,
      'ses-external-while-active',
      'msg-external-while-active',
      'fast-manager',
    )
    const after = dispatch.projectionObservation(handle, 'ses-external-while-active')

    assert.equal(ingress.ok, true)
    assert.equal(ingress.origin, 'HumanMessage')
    assert.equal(ingress.effectiveAgent, 'fast-manager')
    assert.deepEqual(after.activeLogicalRun, active)
    assert.equal(after.activeLogicalRun.logicalRun, before.activeLogicalRun.logicalRun)
    assert.equal(after.runtimeStartCount, before.runtimeStartCount)
    assert.deepEqual(after.pendingClaims, before.pendingClaims)
  })
})

test('WHAT[INTERACTION-AUTHORITY-005] AgentOwnerRoot rejects RootSelection before Host send', async () => {
  await withJournal('owner-root-selection', async (handle) => {
    let providerSends = 0
    const result = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => {
        providerSends += 1
        return dispatch.admittedWithReceipt('accepted-should-not-send')
      }),
      handle,
      'ses-owner-root-selection',
      'must not send',
      rootSelection,
    )

    assert.equal(result.ok, false)
    assert.match(result.error, /identity seed rejected.*ExpectedInheritedFromOwner/i)
    assert.equal(providerSends, 0)
    assert.equal(dispatch.projectionObservation(handle, 'ses-owner-root-selection').activeLogicalRun, null)
  })
})

test('WHAT[INTERACTION-AUTHORITY-005] inherited identity is durable in PluginPromptClaimed before Host send', async () => {
  await withJournal('claim-before-send', async (handle) => {
    const owner = await acceptOwner(handle)
    const seed = inheritedSeed(owner)
    const observations = []

    const result = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => {
        const child = dispatch.projectionObservation(handle, 'ses-claim-before-send')
        observations.push({
          providerSends: 1,
          pendingClaims: child.pendingClaims.length,
          activeAuthority: child.activeLogicalRun,
          seed: child.pendingClaims[0]?.identitySeed,
        })
        return dispatch.admittedWithReceipt('accepted-claim-before-send')
      }),
      handle,
      'ses-claim-before-send',
      'claim before provider work',
      seed,
    )

    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.deepEqual(observations, [{
      providerSends: 1,
      pendingClaims: 1,
      activeAuthority: null,
      seed,
    }])
  })
})

test('WHAT[INTERACTION-AUTHORITY-005] stale owner witness is rejected before Host send', async () => {
  await withJournal('stale-owner', async (handle) => {
    const owner = await acceptOwner(handle)
    const seed = inheritedSeed(owner)
    const staleSeed = { ...seed, ownerLogicalRun: `${seed.ownerLogicalRun}-stale` }
    let providerSends = 0

    const result = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => {
        providerSends += 1
        return dispatch.admittedWithReceipt('accepted-should-not-send')
      }),
      handle,
      'ses-stale-child',
      'stale owner must not send',
      staleSeed,
    )

    assert.equal(result.ok, false)
    assert.match(result.error, /OwnerLogicalRunIdMismatch/)
    assert.equal(providerSends, 0)
    assert.equal(dispatch.projectionObservation(handle, 'ses-stale-child').activeLogicalRun, null)
  })
})

test('WHAT[INTERACTION-AUTHORITY-005] owner superseded after claim rejects physical acceptance without child authority', async () => {
  await withJournal('owner-race', async (handle) => {
    const owner = await acceptOwner(handle, 'ses-race-owner')
    const seed = inheritedSeed(owner)
    let providerSends = 0

    const result = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => {
        providerSends += 1
        const claimed = dispatch.projectionObservation(handle, 'ses-race-child')
        assert.equal(claimed.pendingClaims.length, 1)
        assert.deepEqual(claimed.pendingClaims[0].identitySeed, seed)

        await completeManagerLife(handle, 'ses-race-owner')

        const superseded = await dispatch.acceptHumanRootSelection(
          handle,
          'ses-race-owner',
          'msg-ses-race-owner-superseded',
          rootSelection,
        )
        assert.equal(superseded.ok, true, superseded.ok ? '' : superseded.error)
        return dispatch.admittedWithPhysicalMessage('msg-race-child')
      }),
      handle,
      'ses-race-child',
      'owner changes before physical acceptance',
      seed,
    )

    const child = dispatch.projectionObservation(handle, 'ses-race-child')
    assert.equal(result.ok, false)
    assert.match(result.error, /OwnerLogicalRunIdMismatch|OwnerAuthorityRootUserMessageIdMismatch/)
    assert.equal(providerSends, 1)
    assert.equal(child.activeLogicalRun, null)
    assert.equal(child.pendingClaims.length, 1)
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] physical receipt installs exact AgentOwnerRoot authority after its claim', async () => {
  await withJournal('physical-acceptance', async (handle) => {
    const owner = await acceptOwner(handle)
    const seed = inheritedSeed(owner, 'deep-coder')
    const order = []

    const result = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => {
        const beforeReceipt = dispatch.projectionObservation(handle, 'ses-physical-child')
        order.push({
          phase: 'HostSend',
          pendingClaims: beforeReceipt.pendingClaims.length,
          authorityAccepted: Number(beforeReceipt.activeLogicalRun !== null),
        })
        return dispatch.admittedWithPhysicalMessage('msg-physical-child')
      }),
      handle,
      'ses-physical-child',
      'physical authority root',
      seed,
    )

    const afterReceipt = dispatch.projectionObservation(handle, 'ses-physical-child')
    order.push({
      phase: 'Returned',
      pendingClaims: afterReceipt.pendingClaims.length,
      authorityAccepted: Number(afterReceipt.activeLogicalRun !== null),
    })

    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.deepEqual(order, [
      { phase: 'HostSend', pendingClaims: 1, authorityAccepted: 0 },
      { phase: 'Returned', pendingClaims: 0, authorityAccepted: 1 },
    ])
    assert.equal(afterReceipt.activeLogicalRun.authorityRoot, 'msg-physical-child')
    assert.deepEqual(afterReceipt.activeLogicalRun.identitySeed, seed)
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] rejected or unknown physical send outcome leaves no authority', async () => {
  await withJournal('unaccepted-send', async (handle) => {
    const owner = await acceptOwner(handle)
    const rejectedSeed = inheritedSeed(owner, 'fast-coder')
    const unknownSeed = inheritedSeed(owner, 'deep-coder')

    const rejected = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => dispatch.fatal('provider rejected')),
      handle,
      'ses-rejected-child',
      'rejected child',
      rejectedSeed,
    )
    const unknown = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => dispatch.acceptanceUnknown('physical result unavailable')),
      handle,
      'ses-unknown-child',
      'unknown child',
      unknownSeed,
    )

    assert.equal(rejected.ok, false)
    assert.match(rejected.error, /provider rejected/)
    assert.equal(unknown.ok, false)
    assert.match(unknown.error, /acceptance unknown/i)
    assert.equal(dispatch.projectionObservation(handle, 'ses-rejected-child').activeLogicalRun, null)
    assert.equal(dispatch.projectionObservation(handle, 'ses-rejected-child').pendingClaims.length, 0)
    assert.equal(dispatch.projectionObservation(handle, 'ses-unknown-child').activeLogicalRun, null)
    assert.equal(dispatch.projectionObservation(handle, 'ses-unknown-child').pendingClaims.length, 1)
  })
})
