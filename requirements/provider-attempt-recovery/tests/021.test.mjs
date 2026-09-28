import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const FIRST_TARGET = { model: 'first/model', reasoning: 'high' }
const SECOND_TARGET = { model: 'second/model', reasoning: 'none' }

const admit = async (runtime, sessionId, physicalUserMessageId) => {
  const acquisition = await routing.acquireExecutionAdmission(
    runtime,
    sessionId,
    physicalUserMessageId,
    'engineer',
    'alice',
    '',
  )
  assert.equal(acquisition.kind, 'Acquired', `admission for ${physicalUserMessageId} must acquire`)
  const target = routing.executionAdmissionTarget(runtime, acquisition.lease)

  const settlement = routing.commitExecutionAdmission(runtime, acquisition.lease, {
    sessionId,
    physicalUserMessageId,
    role: 'engineer',
    participant: 'alice',
    target,
  })
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  return target
}

const release = (runtime, sessionId, physicalUserMessageId) => {
  const outcome = routing.releasePhysicalExecution(runtime, sessionId, physicalUserMessageId)
  assert.ok(['Applied', 'AlreadyApplied'].includes(outcome.kind), `release of ${physicalUserMessageId} must apply`)
}

const loadedTemplate = async () => {
  const failed = new Set()
  const providerOf = target => target.model.split('/')[0]
  const scheduler = (_role, running, previous) => {
    const available = target => !failed.has(providerOf(target)) && !running.some(active => active.model === target.model)
    if (previous && available(previous)) return previous
    return [FIRST_TARGET, SECOND_TARGET].find(available) ?? null
  }
  scheduler.markProviderFailed = provider => { failed.add(provider) }
  scheduler.hasTheoreticalCapacity = () => failed.size < 2
  const template = {
    clearFailedProviders: () => failed.clear(),
    providerCapacity: provider => failed.has(provider) ? 0 : 1,
  }
  return { template, runtime: routing.createRuntime(scheduler) }
}

const firstFailureKeptTarget = async () => {
  const { template, runtime } = await loadedTemplate()
  const holders = []

  for (let index = 0; index < 1; index += 1) {
    holders.push(await admit(runtime, `holder-${index}`, `msg-holder-${index}`))
    assert.deepEqual(holders[index], FIRST_TARGET)
  }

  const failedTarget = await admit(runtime, 'ses-lwr', 'msg-first')
  assert.deepEqual(failedTarget, SECOND_TARGET, 'the occupied first candidate pushes the attempt to the second')

  routing.endProviderStep(runtime, 'ses-lwr', 'msg-first', 'run-first')
  release(runtime, 'ses-lwr', 'msg-first')

  for (let index = 0; index < 1; index += 1) {
    release(runtime, `holder-${index}`, `msg-holder-${index}`)
  }

  return { template, runtime, failedTarget }
}

const hash = (value) => `H(${value})`

const rootSelection = (participant) => ({
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant,
    role: participant,
    selectedTier: 'deep',
    persona: 'Engineer',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
})

const profileFor = (session, physical) => {
  const built = authority.createAuthorityRoot(hash, 'rt-lwr-fact', session, 'HumanRoot', physical, rootSelection('engineer'))
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

const admittedWithPhysical = (physicalMessageId) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async () => dispatch.admittedWithPhysicalMessage(physicalMessageId),
})

test('WHAT[provider-attempt-recovery-021] explicit retain settlement preserves the exact failed target for one fresh admission', async () => {
  const { template, runtime, failedTarget } = await firstFailureKeptTarget()

  try {
    // The first target is free again: without a binding fresh admission takes the
    // pool's first candidate, so a retained second candidate proves the binding.
    const retained = routing.retainFailedTargetForRetry(runtime, 'ses-lwr', 'run-first')
    assert.deepEqual(retained, failedTarget, 'the settlement resolves the exact failed target')
    assert.equal(
      routing.retainFailedTargetForRetry(runtime, 'ses-lwr', 'run-first'),
      null,
      'the failed witness is single consumption',
    )

    const retryTarget = await admit(runtime, 'ses-lwr', 'msg-lwr-retry')
    assert.deepEqual(retryTarget, failedTarget, 'the recovery retry returns to the original target')
    assert.equal(template.providerCapacity('second'), 1, 'retaining a target condemns nothing')
  } finally {
    template.clearFailedProviders()
  }
})

test('WHAT[provider-attempt-recovery-021] retry preference is consumed once and does not pin future admissions', async () => {
  const { template, runtime, failedTarget } = await firstFailureKeptTarget()

  try {
    routing.retainFailedTargetForRetry(runtime, 'ses-lwr', 'run-first')
    const retryTarget = await admit(runtime, 'ses-lwr', 'msg-lwr-retry')
    assert.deepEqual(retryTarget, failedTarget)

    release(runtime, 'ses-lwr', 'msg-lwr-retry')
    const next = await admit(runtime, 'ses-lwr', 'msg-next')
    assert.deepEqual(next, FIRST_TARGET, 'the one-use preference has been consumed')
    assert.equal(template.providerCapacity('second'), 1)
  } finally {
    template.clearFailedProviders()
  }
})

test('WHAT[provider-attempt-recovery-021] explicit condemnation consumes an exact witness once and rotates to a healthy target', async () => {
  const { template, runtime, failedTarget } = await firstFailureKeptTarget()

  try {
    routing.retainFailedTargetForRetry(runtime, 'ses-lwr', 'run-first')
    assert.deepEqual(await admit(runtime, 'ses-lwr', 'msg-lwr-retry'), failedTarget)

    // The test selects condemnation explicitly; workflow selection is separate.
    routing.endProviderStep(runtime, 'ses-lwr', 'msg-lwr-retry', 'run-lwr')
    const condemned = routing.condemnFailedTarget(runtime, 'run-lwr')
    assert.deepEqual(condemned, failedTarget, 'the condemning witness is the failed LWR retry target')
    assert.equal(template.providerCapacity('second'), 0)
    assert.equal(
      routing.condemnFailedTarget(runtime, 'run-lwr'),
      null,
      'one witness condemns at most once',
    )

    const rotated = await admit(runtime, 'ses-lwr', 'msg-after-condemn')
    assert.deepEqual(rotated, FIRST_TARGET, 'the dispatch after the condemnation rotates')
  } finally {
    template.clearFailedProviders()
  }
})

test('WHAT[provider-attempt-recovery-021] the_settlement_fact_is_the_durable_provider_retry_attempt_acceptance', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-lwr-fact-'))

  const created = await journal.JournalSurface_bootWithWriterId(
    directory,
    'writer-lwr-fact',
    'rt-lwr-fact',
    1,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  try {
    const handle = created.journal
    const accepted = await failureOwner.acceptHumanRoot(handle, 'ses_lwr_fact', 'msg_root', 'engineer')
    assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)


    // One physical message drives several provider steps: the fact binds the
    // continuation acceptance AND the exact run that established the request's
    // durable ProviderStarted. A run that did not establish it is a later step.
    assert.equal(
      failureOwner.wasLwrRetryAttempt(handle, 'ses_lwr_fact', 'msg_root', 'run-any'),
      false,
      'an authority root is not a recovery dispatch',
    )
    assert.equal(failureOwner.wasLwrRetryAttempt(handle, 'ses_lwr_fact', 'msg_absent', 'run-any'), false)

    const sent = await dispatch.sendContinuation(
      admittedWithPhysical('msg_lwr_retry'),
      handle,
      'ses_lwr_fact',
      'continue after the confirmed provider failure',
      'ProviderRetryAttempt',
      profileFor('ses_lwr_fact', 'msg_lwr_retry'),
      'Await',
    )
    assert.equal(sent.ok, true, sent.ok ? '' : sent.error)

    assert.equal(
      failureOwner.wasLwrRetryAttempt(handle, 'ses_lwr_fact', 'msg_root', 'run-any'),
      false,
      'the root request stays an ordinary attempt',
    )

    const established = await failureOwner.establishProviderRun(handle, 'ses_lwr_fact', 'msg_lwr_retry', 'pr-run-1')
    assert.equal(established.ok, true, 'the retry request establishes its durable provider run')

    assert.equal(
      failureOwner.wasLwrRetryAttempt(handle, 'ses_lwr_fact', 'msg_lwr_retry', 'pr-run-1'),
      true,
      'the accepted retry continuation established by that run is the durable LWR-retry fact',
    )
    assert.equal(
      failureOwner.wasLwrRetryAttempt(handle, 'ses_lwr_fact', 'msg_lwr_retry', 'pr-run-2'),
      false,
      'a later step of the retry episode is not the LWR retry itself',
    )
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[provider-attempt-recovery-021] the_lwr_retry_payload_replaces_the_covered_prefix_with_the_work_record', () => {
  const projection = {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'opening request' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'first answer' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'second answer' }] },
    ],
  }

  const retryInput = {
    journal: true,
    sessionId: 'ses_lwr_payload',
    acceptedRetry: true,
    failures: 1,
    prefixEpoch: 0,
    physicalUser: 'msg_lwr_retry',
    acceptedPhysicalUser: 'msg_lwr_retry',
    snapshotPort: true,
    currentProjection: projection,
    committedSnapshot: null,
    coverableCutoff: 1,
    coveredDigest: xwire.coveredPrefixDigest(projection, 1),
    requestStartCutoff: 2,
    frozenRecordPrefixRef: 'blob/frozen',
    frozenRecordPrefixDigest: 'sha256:frozen',
    frozenRecordPrefixBody: 'Chronicle\nwork record of the failed attempt',
    memoryPreamble: 'same-session memory',
    outcome: null,
  }

  const retry = xwire.transform(retryInput)
  assert.equal(retry.ok, true, retry.ok ? '' : retry.error)
  assert.equal(retry.consumed, true)
  assert.equal(retry.changed, true, 'the LWR retry replaces the covered prefix')
  assert.equal(retry.probe.candidate.cutoff, 1)

  const head = retry.output.messages[0]
  assert.equal(head.role, 'user')
  assert.ok(head.parts[0].text.includes('same-session memory'))
  assert.ok(
    head.parts[0].text.includes('work record of the failed attempt'),
    'the LWR body is the retry request context',
  )
  assert.equal(retry.output.messages.length, 3, 'the covered prefix is dropped and the tail stays')
  assert.equal(retry.output.messages[1].parts[0].text, 'first answer')

  const ordinary = xwire.transform({ ...retryInput, acceptedRetry: false })
  assert.equal(ordinary.noop, true, 'an ordinary request never replaces the prefix')

  const beforeAnyFailure = xwire.transform({ ...retryInput, failures: 0 })
  assert.equal(beforeAnyFailure.changed, false, 'no confirmed failure means no LWR replacement')
})

test.todo('WHAT[provider-attempt-recovery-021] actual ordinary and sync-delegate recovery combine exact durable LWR evidence and target settlement after licensing and before send (GAP-139)')
