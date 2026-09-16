import assert from 'node:assert/strict'
import test from 'node:test'
import * as admission from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import { runManagedAdmissionScenario } from './support/admission-scenario-helper.mjs'
import * as bootstrap from './support/bootstrap-helper.mjs'

test('WHAT[CHATEXEC-003] managed admission has one fixed success order', async () => {
  const result = await runManagedAdmissionScenario('happy-path')
  assert.equal(result.outcome, 'Settled')
})

test('WHAT[CHATEXEC-003] append failure performs zero downstream effects', async () => {
  const result = await runManagedAdmissionScenario('append-failure')
  assert.equal(result.outcome, 'Refused')
  assert.equal(result.acquiredCapacity, false)
})

test('WHAT[CHATEXEC-003] acquisition failure crosses no later boundary', async () => {
  const result = await runManagedAdmissionScenario('acquire-failure')
  assert.equal(result.outcome, 'Refused')
  assert.equal(result.boundProvider, false)
})

test('WHAT[CHATEXEC-003] superseded demand is a typed nonfatal short-circuit', async () => {
  const result = await runManagedAdmissionScenario('superseded')
  assert.equal(result.outcome, 'ShortCircuit')
})

test('WHAT[CHATEXEC-003] already-started replay performs no duplicate admission effect', async () => {
  const result = await runManagedAdmissionScenario('already-started-replay')
  assert.equal(result.duplicateEffects, 0)
})

test('WHAT[CHATEXEC-003] managed path calls one admission transaction', async () => {
  const calls = await bootstrap.countAdmissionCalls('managed')
  assert.equal(calls, 1)
})

test('WHAT[CHATEXEC-003] only Settled crosses the managed provider boundary', async () => {
  const crossed = await bootstrap.canCrossProvider('Refused')
  assert.equal(crossed, false)
})

test('WHAT[CHATEXEC-003] acceptance uncertainty, acquire, bind, and Host projection failures stop before provider', async () => {
  for (const failure of ['acceptance-uncertain', 'acquire-failed', 'bind-failed', 'projection-failed']) {
    const crossed = await bootstrap.canCrossProvider(failure)
    assert.equal(crossed, false)
  }
})

test('WHAT[CHATEXEC-003] unmanaged and HostInternal preserve the physical continuation without admission', async () => {
  const handled = await bootstrap.handleUnmanaged('HostInternal')
  assert.equal(handled.bypassedAdmission, true)
})

test('WHAT[CHATEXEC-003] Reject remains typed at the Host hook boundary', async () => {
  const res = await bootstrap.hookRejectResponse('TypedRejection')
  assert.equal(res.ok, false)
  assert.equal(res.isTyped, true)
})

test('WHAT[CHATEXEC-003] bootstrap contains no fragmented admission owner', () => {
  assert.equal(bootstrap.hasFragmentedAdmissionOwner(), false)
})
