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
  participant: 'manager',
  role: 'manager',
  selectedTier: 'deep',
  persona: 'Lead',
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

const inheritedSeed = (owner, child = 'coder') => {
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

test('WHAT[INTERACTION-AUTHORITY-005] IA_005_every_continuation_kind_is_parseable_and_not_root', () => {
  const kinds = [
    'InteractionRepair',
    'JoinGuard',
    'ManagerGuard',
    'BusyAgentNudge',
    'HumanMessage',
    'ManagedDelegationAssignment',
    'ProviderRetryAttempt',
    'DegenerationGuard',
    'FissionHandoff',
  ]

  for (const kind of kinds) {
    assert.deepEqual(authority.originForContinuation(kind), { kind: 'Continuation', label: kind })
    assert.deepEqual(authority.tryParseContinuationKind(kind), { kind })
  }
  assert.equal(authority.tryParseContinuationKind('HumanRoot'), null)
})
