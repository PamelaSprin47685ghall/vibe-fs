import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as persona from '../../../dist/Participant/Persona/Surface.js'
import * as capability from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as store from '../../../dist/Persistence/EventStore/Surface.js'
import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withJournal } from './support/authority.mjs'

const historical = readFileSync(new URL('./fixtures/authority-root-v1-inspector.json', import.meta.url), 'utf8').trim()

test('WHAT[interaction-authority-021] historical payload survives actual EventStore reopen without role rewriting', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-historical-authority-'))
  let handle = store.create(directory, 'history-writer')
  const event = { id: 'historical-authority', stream: 'authority-history', type: 'JournalEnvelope', parents: [], payload: { Fact: JSON.parse(historical) }, payloadRefs: [] }
  try {
    const appended = await store.append(handle, [event])
    assert.equal(appended.ok, true, JSON.stringify(appended))
    const before = store.read(handle, event.id)
    assert.deepEqual(before, event)
    store.dispose(handle)
    handle = store.create(directory, 'history-reader')
    assert.deepEqual(store.read(handle, event.id), before)
    assert.equal(JSON.stringify(store.read(handle, event.id).payload.Fact), historical)
  } finally {
    store.dispose(handle)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[interaction-authority-021] legacy authority is rejected at actual new ingress and current Engineer remains admissible', async () => {
  await withJournal('no-legacy-upgrade', async (handle) => {
    for (const legacy of ['coder', 'inspector', 'browser', 'inquiry', 'distiller']) {
      assert.equal(persona.resolveParticipantIdentityAtRoot(legacy).ok, false)
      assert.equal(capability.isAllowed(legacy, 'Write'), false)
      assert.equal(capability.isAllowed(legacy, 'Fission'), false)
      const accepted = await dispatch.acceptManagedExternal(handle, `new-${legacy}`, `root-${legacy}`, legacy)
      assert.equal(accepted.ok, false)
      assert.equal(dispatch.projectionObservation(handle, `new-${legacy}`).activeLogicalRun, null)
    }
    const current = await dispatch.acceptManagedExternal(handle, 'new-engineer', 'root-engineer', 'engineer')
    assert.equal(current.ok, true, JSON.stringify(current))
    assert.equal(capability.isAllowed('Engineer', 'Write'), true)
    assert.equal(capability.isAllowed('Engineer', 'Fission'), true)
  })
})

test('WHAT[interaction-authority-021] legacy Inspector fact remains decodable for history without becoming Engineer', { todo: 'GAP-124 actual historical fact decoder rejects legacy SelectedAgent' }, () => {
  const decoded = factCodec.decode(historical)
  assert.equal(decoded.ok, true, decoded.error)
  assert.equal(decoded.payload.IdentitySeed.participantIdentity.participant, 'inspector')
  assert.equal(decoded.payload.IdentitySeed.participantIdentity.role, 'inspector')
})
