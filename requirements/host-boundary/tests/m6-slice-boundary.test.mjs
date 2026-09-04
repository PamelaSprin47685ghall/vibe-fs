import assert from 'node:assert/strict'
import test from 'node:test'
import { readOwnerProjectInventoryV1 } from '../../../scripts/checks/owner-projects.mjs'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import { assertEffectIsInjected, assertFatalBoundary, assertOptionalObservationNoninterference, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const locality = (inventory, id) => {
  const matches = inventory.localities.filter((candidate) => candidate.id === id)
  assert.equal(matches.length, 1, `${id} must resolve to one production locality`)
  return matches[0]
}

const sourcePaths = (entry) => entry.sources.map(({ implementationPath }) => implementationPath)

test('WHAT[HOST-BOUNDARY-027] Host message loop and envelope slices reject the old wide signal closure', () => {
  assertPureContract()
  assertEffectIsInjected('host')
})

test('WHAT[HOST-BOUNDARY-027] production inventory closes Host codec audiences without the wide signal adapter', () => {
  const inventory = readOwnerProjectInventoryV1()
  const envelope = locality(inventory, 'host-event-envelope')
  const message = locality(inventory, 'host-message-codec')
  const loop = locality(inventory, 'loop-event-codec')

  assert.equal(envelope.kind, 'contract')
  assert.equal(message.kind, 'contract')
  assert.equal(loop.kind, 'contract')
  assert.deepEqual(sourcePaths(envelope), ['src/Wanxiangshu/OpenCode/Codec/HostEventEnvelope.fs'])
  assert.deepEqual(sourcePaths(message), ['src/Wanxiangshu/OpenCode/Codec/HostMessageCodec.fs'])
  assert.deepEqual(sourcePaths(loop), ['src/Wanxiangshu/OpenCode/Codec/LoopEventCodec.fs'])
  assert.deepEqual(loop.references, ['foundation-identity', 'host-event-envelope'])

  for (const id of ['host-session-runtime', 'authority-runtime-surface', 'opencode-codec-providerprojectionsurface']) {
    const consumer = locality(inventory, id)
    assert.ok(consumer.references.includes('host-message-codec'), `${id} must consume the message codec contract`)
    assert.ok(!consumer.references.includes('host-signal-adapter'), `${id} must not consume the wide signal adapter`)
  }

  const loopRuntime = locality(inventory, 'execution-session-loopdetector')
  assert.ok(loopRuntime.references.includes('loop-event-codec'))
  assert.ok(!loopRuntime.references.includes('host-signal-adapter'))
  assert.ok(!loopRuntime.references.includes('host-diagnostics-runtime'))

  const signalAdapter = locality(inventory, 'host-signal-adapter')
  assert.ok(signalAdapter.references.includes('host-event-envelope'))
  assert.ok(signalAdapter.references.includes('loop-event-codec'))
  assert.ok(!sourcePaths(signalAdapter).includes('src/Wanxiangshu/OpenCode/Codec/HostMessageCodec.fs'))
  assert.ok(!sourcePaths(signalAdapter).includes('src/Wanxiangshu/OpenCode/Codec/LoopEventCodec.fs'))

  const visibility = locality(inventory, 'opencode-host-messagevisibility')
  assert.ok(visibility.references.includes('host-event-envelope'))
  assert.ok(!visibility.references.includes('host-signal-adapter'))
  assert.ok(locality(inventory, 'execution-delegation-hostturnobservedsurface').references.includes('host-event-envelope'))
})

test('WHAT[HOST-BOUNDARY-027] Host envelope projection is shared and never mutates the raw payload', () => {
  const payload = { type: 'message.updated', properties: { sessionID: 'session-1', info: { sessionID: 'session-2' } } }
  const input = { directory: '/must-not-cross', payload }

  assert.equal(HostSignalSurface.unwrapEnvelope(input), payload)
  assert.equal(Object.hasOwn(payload, 'directory'), false)
  assert.equal(HostSignalSurface.envelopeEventType(input), 'message.updated')
  assert.equal(HostSignalSurface.envelopeSessionId(input), 'session-1')
  assert.equal(HostSignalSurface.envelopeMessageSessionId(input), 'session-1')
  assert.equal(
    HostSignalSurface.envelopeMessageSessionId({ payload: { type: 'message.updated', properties: { info: { sessionID: 'session-2' } } } }),
    'session-2',
  )
})

test('WHAT[HOST-BOUNDARY-030] Host envelope rejects adjacent malformed event and session carriers without throwing', () => {
  for (const value of ['', ' ', 7, true, {}, [], new String('session-1')]) {
    const raw = { type: value, properties: { sessionID: value, sessionId: value, info: { sessionID: value } }, sessionID: value, sessionId: value }
    assert.doesNotThrow(() => HostSignalSurface.envelopeEventType(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeSessionId(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeMessageSessionId(raw))
    assert.equal(HostSignalSurface.envelopeEventType(raw), '')
    assert.equal(HostSignalSurface.envelopeSessionId(raw), null)
    assert.equal(HostSignalSurface.envelopeMessageSessionId(raw), null)
  }

  for (const raw of [null, 7, true, 'session-1', [], {}]) {
    assert.doesNotThrow(() => HostSignalSurface.unwrapEnvelope(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeEventType(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeSessionId(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeMessageSessionId(raw))
  }
})

test('WHAT[HOST-BOUNDARY-028] typed subscription and diagnostic injection preserve one failure owner', async () => {
  const inventory = readOwnerProjectInventoryV1()
  const adapter = locality(inventory, 'host-signal-adapter')
  const composition = locality(inventory, 'opencode-host-hostsignalbootstrap')

  assert.ok(!adapter.references.includes('host-diagnostics-runtime'))
  assert.ok(!adapter.references.includes('foundation-temporal'))
  assert.ok(composition.references.includes('host-signal-adapter'))
  assert.ok(composition.references.includes('host-diagnostics-runtime'))
  await assertOptionalObservationNoninterference()
  assertEffectIsInjected('console')
})

test('WHAT[HOST-BOUNDARY-029] fatal vocabulary stays pure and physical execution is composition-only', () => {
  assertPureContract('capability-type-only')
  assertFatalBoundary('host-boundary')
})

test('WHAT[HOST-BOUNDARY-031] RootWorkspace runtime is private and every observer consumes only the typed contract', () => {
  const inventory = readOwnerProjectInventoryV1()
  const contract = locality(inventory, 'host-root-workspace-contract')
  const runtime = locality(inventory, 'host-root-workspace-runtime')

  assert.equal(contract.kind, 'contract')
  assert.deepEqual(sourcePaths(contract), ['src/Wanxiangshu/OpenCode/Host/RootWorkspace.fs'])
  assert.deepEqual(contract.references, [])
  assert.equal(runtime.kind, 'runtime')
  assert.deepEqual(sourcePaths(runtime), ['src/Wanxiangshu/OpenCode/Host/RootWorkspaceRuntime.fs'])
  assert.deepEqual(runtime.references, ['host-root-workspace-contract'])

  const runtimeConsumers = inventory.localities
    .filter(({ references }) => references.includes('host-root-workspace-runtime'))
    .map(({ id }) => id)
    .sort()
  assert.deepEqual(runtimeConsumers, ['opencode-host-hostsignalbootstrap', 'opencode-host-sharedstatesurface'])

  assert.ok(locality(inventory, 'interaction-dispatch-opencode-ingresscodec').references.includes('host-root-workspace-contract'))

  for (const id of [
    'execution-delegation-hostturnobservedsurface',
    'git-integrationgate',
    'interaction-repair-interactionrepair',
    'opencode-host-pluginruntimescope',
    'participant-provider-attempt-fallback-ledger',
  ])
    assert.ok(!locality(inventory, id).references.includes('host-root-workspace-runtime'), `${id} must not acquire the process-local runtime`)
})
