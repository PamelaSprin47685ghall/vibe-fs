import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as identity from '../../../../dist/Participant/Persona/Surface.js'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'

const rootSelection = () => {
  const resolved = identity.resolveParticipantIdentityAtRoot('manager')
  assert.equal(resolved.ok, true, resolved.error)
  const value = resolved.identity
  return {
    kind: 'RootSelection', ownerSession: null, ownerLogicalRun: null, ownerAuthorityRoot: null,
    participantIdentity: {
      participant: value.name, role: value.role, persona: value.persona,
      personaCatalogVersion: value.catalogVersion, origin: value.origin,
    },
  }
}

export const withJournal = async (label, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-dispatch-${label}-`))
  let handle
  let incarnation = 0
  const open = async () => {
    incarnation += 1
    const result = await journal.JournalSurface_bootWithWriterId(directory, `writer-${label}-${incarnation}`, `runtime-${label}-${incarnation}`, 4242, '2026-09-26T00:00:00Z')
    assert.equal(result.ok, true, JSON.stringify(result.error))
    handle = result.journal
    return handle
  }
  const reopen = async () => {
    journal.JournalSurface_dispose(handle)
    handle = null
    return open()
  }
  try { return await action(await open(), reopen, directory) }
  finally {
    if (handle) journal.JournalSurface_dispose(handle)
    rmSync(directory, { recursive: true, force: true })
  }
}

export const acceptOwner = async (handle, session = 'authority-owner') => {
  const result = await dispatch.acceptHumanRootSelection(handle, session, `root-${session}`, rootSelection())
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.profile
}

export const hostPort = (send) => ({ SubscribeTerminal: () => ({ Dispose() {} }), SendPrompt: send })
