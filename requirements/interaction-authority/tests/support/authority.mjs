import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as identity from '../../../../dist/Participant/Persona/Surface.js'
import * as authority from '../../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as eventStore from '../../../../dist/Persistence/EventStore/Surface.js'

export const rootSelection = (participant = 'manager') => {
  const resolved = identity.resolveParticipantIdentityAtRoot(participant)
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

export const rootFor = (participant = 'engineer', physical = 'root-message', session = 'authority-session') => {
  const result = authority.createAuthorityRoot((value) => `test-hash(${value})`, 'authority-runtime', session, 'HumanRoot', physical, rootSelection(participant))
  assert.equal(result.ok, true, result.error)
  return result.value
}

export const withJournal = async (label, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-authority-${label}-`))
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

export const withSharedJournal = async (label, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-authority-shared-${label}-`))
  execFileSync('git', ['init', '--quiet', directory])
  const commonDir = await journal.JournalSurface_initializeStateRoot(directory)
  let handle
  const open = async () => {
    const result = await journal.JournalSurface_acquireSharedForWorkspace(directory, process.pid, '2026-10-14T00:00:00Z')
    assert.equal(result.ok, true, JSON.stringify(result.error))
    handle = result.journal
    return handle
  }
  const reopen = async () => {
    journal.JournalSurface_dispose(handle)
    handle = null
    return open()
  }
  try { return await action(await open(), reopen, directory, commonDir) }
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

export const appendClosureEnvelope = async (commonDir, session, closure) => {
  const writer = `closure-${randomUUID()}`
  const store = eventStore.create(commonDir, writer)
  try {
    const stream = `journal/session/${session}`
    const parent = eventStore.head(store, stream)
    assert.equal(typeof parent, 'string')
    const reference = eventStore.read(store, parent)
    assert.equal(reference.type, 'JournalEnvelope')
    const id = randomUUID().replaceAll('-', '')
    const event = {
      id, stream, type: 'JournalEnvelope', parents: [parent], payloadRefs: [],
      payload: {
        ...reference.payload,
        EventId: ['EventId', id],
        RuntimeId: ['RuntimeId', writer],
        LocalSeq: ['LocalSeq', '1'],
        Fact: ['Agent', ['Prompt', ['AuthorityLogicalRunClosed', {
          SessionId: ['SessionId', session],
          LogicalRunId: ['LogicalRunId', closure.logicalRun],
          AuthorityRootUserMessageId: ['AuthorityRootUserMessageId', closure.authorityRoot],
          RootKind: closure.rootKind,
          ClosureWitness: closure.closureWitness,
        }]]],
      },
    }
    return { ...await eventStore.append(store, [event]), event }
  } finally {
    eventStore.dispose(store)
  }
}

export const hostPort = (send) => ({ SubscribeTerminal: () => ({ Dispose() {} }), SendPrompt: send })
