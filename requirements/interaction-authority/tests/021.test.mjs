import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as PersonaSurface from '../../../dist/Participant/Persona/Surface.js'
import * as CapabilitySurface from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as EventStoreSurface from '../../../dist/Persistence/EventStore/Surface.js'

test('WHAT[INTERACTION-AUTHORITY-021] historical inspector records are isolated and do not upgrade to engineer authority', async () => {
  // 1. Office permissions boundary: Engineer has Write and Fission; legacy Inspector/Coder have neither
  assert.equal(CapabilitySurface.isAllowed('Engineer', 'Write'), true, 'Engineer must have Write permission')
  assert.equal(CapabilitySurface.isAllowed('Engineer', 'Fission'), true, 'Engineer must have Fission permission')

  assert.equal(CapabilitySurface.isAllowed('Inspector', 'Write'), false, 'Legacy Inspector must not have Write permission')
  assert.equal(CapabilitySurface.isAllowed('Inspector', 'Fission'), false, 'Legacy Inspector must not have Fission permission')
  assert.equal(CapabilitySurface.isAllowed('Coder', 'Write'), false, 'Legacy Coder must not have Write permission')
  assert.equal(CapabilitySurface.isAllowed('Coder', 'Fission'), false, 'Legacy Coder must not have Fission permission')

  // 2. Active identity resolution fail-closed: legacy role names return LegacyParticipantName error and NEVER upgrade to Engineer
  const legacyInspectorResolve = PersonaSurface.resolveParticipantIdentityAtRoot('inspector')
  assert.equal(legacyInspectorResolve.ok, false)
  assert.equal(legacyInspectorResolve.error, 'LegacyParticipantName', 'Resolving legacy inspector at root must fail-closed with LegacyParticipantName')
  assert.equal(legacyInspectorResolve.identity, null)

  const legacyCoderResolve = PersonaSurface.resolveParticipantIdentityAtRoot('coder')
  assert.equal(legacyCoderResolve.ok, false)
  assert.equal(legacyCoderResolve.error, 'LegacyParticipantName', 'Resolving legacy coder at root must fail-closed with LegacyParticipantName')
  assert.equal(legacyCoderResolve.identity, null)

  // 3. Historical event logs: historical events are preserved and decodable without rewriting EventStore
  const directory = await mkdtemp(join(tmpdir(), 'wxs-ia-historical-'))
  const handle = EventStoreSurface.create(directory, 'writer-ia-021')
  try {
    const historicalEvent = {
      id: 'evt-hist-1',
      stream: 'session/ses_hist_021',
      type: 'JobRequested',
      parents: [],
      payload: { id: 'evt-hist-1', role: 'Inspector', charge: 'historical read-only investigation' },
      payloadRefs: [],
    }
    const appendRes = await EventStoreSurface.append(handle, [historicalEvent])
    assert.equal(appendRes.ok, true, 'Historical event must be safely appended without rewriting')
  } finally {
    EventStoreSurface.dispose(handle)
  }
})
