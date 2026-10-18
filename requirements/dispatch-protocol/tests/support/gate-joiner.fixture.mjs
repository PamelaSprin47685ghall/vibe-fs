import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const [product, directory, scenario, receiptPath] = process.argv.slice(2)
const load = relative => import(pathToFileURL(path.join(product, 'dist', relative)).href)
const journal = await load('Persistence/Journal/Surface.js')
const dispatch = await load('Interaction/Dispatch/DispatchSurface.js')
const result = { scenario, pid: process.pid, sends: 0, registrations: [0, 0], notifications: [0, 0] }
const leases = [[], []]
const sentinel = new Error('joiner lease owner refused')
let release
let ready
let later = false
const hostReached = new Promise(resolve => { ready = resolve })
const port = {
  SubscribeTerminal: () => ({ Dispose() {} }),
  SubscribeFutureTerminal: () => ({ Dispose() {} }),
  SendPrompt: () => {
    result.sends++
    if (later) return Promise.resolve(dispatch.admittedWithReceipt('later-transport-receipt'))
    return new Promise(resolve => { release = resolve; ready() })
  },
}
const opened = await journal.JournalSurface_bootWithWriterId(directory, 'gate-joiner-writer', 'gate-joiner-runtime', process.pid, '2026-01-01T00:00:00Z')
assert.equal(opened.ok, true)
let timer

try {
  const session = 'gate-joiner-session'
  assert.equal((await dispatch.acceptHumanRoot(opened.journal, session, 'gate-joiner-root', 'engineer')).ok, true)
  const call = index => dispatch.sendGateContinuationWithAcceptance(
    port, opened.journal, directory, session, 'one exact gate occasion', 'ProviderRetryAttempt',
    'GateJoiner', 'gate-joiner-terminal',
    () => { result.notifications[index]++ },
    lease => {
      result.registrations[index]++
      leases[index].push(lease)
      if (scenario === 'attach-throws' && index === 1) throw sentinel
    })
  const settle = pending => pending.then(value => ({ value }), error => ({ error }))
  const first = settle(call(0))
  const second = settle(call(1))
  await hostReached
  assert.equal(result.sends, 1)
  const claim = dispatch.projectionObservation(opened.journal, session).pendingClaims[0].promptKey
  const acceptPhysical = async () => {
    const accepted = await dispatch.acceptManagedPromptClaim(opened.journal, session, 'gate-joiner-physical', claim, 'engineer')
    assert.equal(accepted.ok, true, accepted.error)
  }
  if (scenario === 'physical-before-receipt') await acceptPhysical()
  release(scenario === 'send-fails' ? dispatch.retryable('transport refused')
    : scenario === 'unknown' ? dispatch.acceptanceUnknown('physical outcome unknown')
    : dispatch.admittedWithReceipt('gate-joiner-receipt'))
  const outcomes = await Promise.race([
    Promise.all([first, second]),
    new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('joined gate send did not settle')), 8000) }),
  ])
  clearTimeout(timer)
  if (scenario === 'attach-throws') {
    assert.equal(outcomes[0].value.kind, 'Sent')
    assert.equal(outcomes[1].error, sentinel)
    result.originalJoinerErrorPreserved = true
  } else if (scenario === 'send-fails' || scenario === 'unknown') {
    assert.ok(outcomes.every(item => item.value.kind === 'Failed'))
    assert.deepEqual(result.registrations, [1, 0])
    later = true
    const repeated = await call(0)
    assert.equal(repeated.kind, scenario === 'unknown' ? 'AlreadyAdmitted' : 'Sent')
    assert.equal(result.sends, scenario === 'unknown' ? 1 : 2)
  } else {
    assert.ok(outcomes.every(item => item.value.kind === 'Sent'))
    assert.deepEqual(outcomes[0].value, outcomes[1].value)
    assert.deepEqual(result.registrations, scenario === 'physical-before-receipt' ? [1, 0] : [1, 1])
    if (scenario !== 'physical-before-receipt') {
      for (const lease of leases[0]) dispatch.disposePhysicalAcceptanceObserver(lease)
      await acceptPhysical()
    }
    assert.deepEqual(result.notifications, scenario === 'physical-before-receipt' ? [1, 1] : [0, 1])
  }
  result.accepted = true
} catch (error) {
  result.accepted = false
  result.error = { message: error.message, stack: error.stack }
  process.exitCode = 1
} finally {
  clearTimeout(timer)
  for (const owned of leases) for (const lease of owned) dispatch.disposePhysicalAcceptanceObserver(lease)
  journal.JournalSurface_dispose(opened.journal)
  result.closed = true
  fs.writeFileSync(receiptPath, JSON.stringify(result, null, 2) + '\n')
}
