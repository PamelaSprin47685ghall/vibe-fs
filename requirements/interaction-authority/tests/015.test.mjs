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

test('WHAT[INTERACTION-AUTHORITY-015] matching external user ingress continues without replacing the active authority run', async () => {
  await withJournal('external-while-active', async (handle) => {
    const active = await acceptOwner(handle, 'ses-external-while-active')
    const before = dispatch.projectionObservation(handle, 'ses-external-while-active')

    const ingress = await dispatch.acceptManagedExternal(
      handle,
      'ses-external-while-active',
      'msg-external-while-active',
      'manager',
    )
    const after = dispatch.projectionObservation(handle, 'ses-external-while-active')

    assert.equal(ingress.ok, true)
    assert.equal(ingress.origin, 'HumanMessage')
    assert.equal(ingress.participant, 'manager')
    assert.equal(ingress.role, 'manager')
    assert.deepEqual(after.activeLogicalRun, active)
    assert.equal(after.activeLogicalRun.logicalRun, before.activeLogicalRun.logicalRun)
    assert.equal(after.runtimeStartCount, before.runtimeStartCount)
    assert.deepEqual(after.pendingClaims, before.pendingClaims)
  })
})
