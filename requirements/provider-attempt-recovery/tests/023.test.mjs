// PAR-023: a physically accepted recovery retry that never reached
// `ProviderStarted` is an explicit obligation. The Host session idle sweeps
// exactly the `Accepted ∧ ¬ProviderStarted` executions of that session and
// disposes of each one through the typed resume capability; without that
// capability the exclusion is visible, never silent.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import * as recovery from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

const boot = async (portOutcome) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), `par-023-${portOutcome}-`))
  const handle = await recovery.bootRecoveryHost(directory, portOutcome)
  return { directory, handle }
}

const close = ({ directory, handle }) => {
  recovery.disposeRecoveryHost(handle)
  fs.rmSync(directory, { recursive: true, force: true })
}

test('WHAT[PAR-023] an accepted-and-never-executed retry is disposed by the idle sweep', async () => {
  const booted = await boot('accept')

  try {
    await recovery.seedAccepted(booted.handle, 'ses-obligation', 'msg-retry')

    const observed = await recovery.signalSessionQuiesced(booted.handle, 'ses-obligation')

    assert.equal(observed.calls, 1, 'the exact accepted material reaches the typed resume capability')
    assert.deepEqual(observed.manuals, [], 'a disposed obligation leaves no manual intervention')
  } finally {
    close(booted)
  }
})

test('WHAT[PAR-023] without the capability the obligation stays visible, never silent', async () => {
  const booted = await boot('absent')

  try {
    await recovery.seedAccepted(booted.handle, 'ses-obligation', 'msg-retry')

    const observed = await recovery.signalSessionQuiesced(booted.handle, 'ses-obligation')

    assert.equal(observed.calls, 0)
    assert.equal(observed.manuals.length, 1, 'an unresumable accepted execution must surface as manual intervention')
    assert.equal(observed.manuals[0].reason, 'NoAuthorizedProviderDisposition')
    assert.equal(observed.manuals[0].lifecycle, 'Accepted')
  } finally {
    close(booted)
  }
})

test('WHAT[PAR-023] the idle sweep never re-judges an execution the provider already owns', async () => {
  const booted = await boot('accept')

  try {
    await recovery.seedAccepted(booted.handle, 'ses-started', 'msg-retry')
    await recovery.seedProviderStarted(booted.handle, 'ses-started', 'msg-retry', 'msg-provider-run')

    const observed = await recovery.signalSessionQuiesced(booted.handle, 'ses-started')

    assert.equal(observed.calls, 0, 'provider-started executions belong to provider recovery, not to this obligation')
    assert.deepEqual(observed.manuals, [])
  } finally {
    close(booted)
  }
})

test('WHAT[PAR-023] the sweep is scoped to the idling session only', async () => {
  const booted = await boot('accept')

  try {
    await recovery.seedAccepted(booted.handle, 'ses-idle', 'msg-other')
    await recovery.seedAccepted(booted.handle, 'ses-other', 'msg-retry')

    const observed = await recovery.signalSessionQuiesced(booted.handle, 'ses-idle')

    assert.equal(observed.calls, 1, 'only the idling session obligation is disposed')

    const other = await recovery.signalSessionQuiesced(booted.handle, 'ses-other')
    assert.equal(other.calls, 2, 'the other session is disposed by its own idle, not by this one')
  } finally {
    close(booted)
  }
})
