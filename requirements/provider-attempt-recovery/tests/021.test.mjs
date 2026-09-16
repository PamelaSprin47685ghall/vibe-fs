// requirements/provider-attempt-recovery/tests/lwr-retry-before-condemn.test.mjs
//
// PAR-021 / EMR-017: one confirmed provider failure keeps its physical target
// for the LWR-replaced retry; only the failure of the attempt that already WAS
// that LWR retry condemns the provider, and the dispatch after that rotates.
//
// The settlement fact is the durable `ProviderRetryAttempt` acceptance of the
// exact physical request — never a failure ordinal — and the retry binding it
// writes is single-consumption. The recommended scheduler template is the real
// policy owner for provider capacity, so the scheduling tests drive it as the
// production bootstrap does (its default export plus the attached
// `markProviderFailed` / `hasTheoreticalCapacity` capabilities).

import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const ROOT = fileURLToPath(new URL('../../../', import.meta.url))
const FIRST_CODER_TARGET = { model: 'cursor/cursor-grok-4.6-xhigh', reasoning: 'xhigh' }
const SECOND_CODER_TARGET = { model: 'neuralwatt/glm-5.2-flex', reasoning: 'high' }

// ── scheduling: the failed target is kept for the LWR retry ────────────────

const admit = async (runtime, sessionId, physicalUserMessageId) => {
  const acquisition = await routing.acquireExecutionAdmission(
    runtime,
    sessionId,
    physicalUserMessageId,
    'coder',
    'alice',
    '',
  )
  assert.equal(acquisition.kind, 'Acquired', `admission for ${physicalUserMessageId} must acquire`)
  const target = routing.executionAdmissionTarget(runtime, acquisition.lease)

  const settlement = routing.commitExecutionAdmission(runtime, acquisition.lease, {
    sessionId,
    physicalUserMessageId,
    role: 'coder',
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

/** The real recommended template, wired the way model-routing bootstrap wires it. */
const loadedTemplate = async () => {
  const template = await import('../../../resources/wanxiangshu.mjs')
  const scheduler = template.default
  scheduler.markProviderFailed = template.markProviderFailed
  scheduler.hasTheoreticalCapacity = template.hasTheoreticalCapacity
  return { template, runtime: routing.createRuntime(scheduler) }
}

/**
 * A confirmed failure of an attempt that still carried the original context:
 * the coder pool's first candidate is saturated, so the attempt lands on the
 * second candidate; its execution is released before the settlement (the
 * witness outlives the lease) and the failed provider stays unpoisoned.
 */
const firstFailureKeptTarget = async () => {
  const { template, runtime } = await loadedTemplate()
  const holders = []

  for (let index = 0; index < 4; index += 1) {
    holders.push(await admit(runtime, `holder-${index}`, `msg-holder-${index}`))
    assert.deepEqual(holders[index], FIRST_CODER_TARGET)
  }

  const failedTarget = await admit(runtime, 'ses-lwr', 'msg-first')
  assert.deepEqual(failedTarget, SECOND_CODER_TARGET, 'the saturated first candidate pushes the attempt to the second')

  routing.endProviderStep(runtime, 'ses-lwr', 'msg-first', 'run-first')
  release(runtime, 'ses-lwr', 'msg-first')

  for (let index = 0; index < 4; index += 1) {
    release(runtime, `holder-${index}`, `msg-holder-${index}`)
  }

  return { template, runtime, failedTarget }
}

test('WHAT[PAR-021] first_failure_keeps_the_original_target_for_the_lwr_retry', async () => {
  const { template, runtime, failedTarget } = await firstFailureKeptTarget()

  try {
    // cursor is free again: without a binding the fresh admission takes the
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
    assert.equal(template.providerCapacity('neuralwatt'), 4, 'the first failure condemns nothing')
  } finally {
    template.clearFailedProviders()
  }
})

test('WHAT[PAR-021] a_successful_lwr_retry_switches_nothing', async () => {
  const { template, runtime, failedTarget } = await firstFailureKeptTarget()

  try {
    routing.retainFailedTargetForRetry(runtime, 'ses-lwr', 'run-first')
    const retryTarget = await admit(runtime, 'ses-lwr', 'msg-lwr-retry')
    assert.deepEqual(retryTarget, failedTarget)

    // The retry completes: no settlement ever runs for a success.
    const next = await admit(runtime, 'ses-lwr', 'msg-next')
    assert.deepEqual(next, failedTarget, 'a successful LWR retry keeps the route')
    assert.equal(template.providerCapacity('neuralwatt'), 4, 'success never condemns a provider')
  } finally {
    template.clearFailedProviders()
  }
})

test('WHAT[PAR-021] only_the_failed_lwr_retry_condemns_the_provider_and_rotates', async () => {
  const { template, runtime, failedTarget } = await firstFailureKeptTarget()

  try {
    routing.retainFailedTargetForRetry(runtime, 'ses-lwr', 'run-first')
    assert.deepEqual(await admit(runtime, 'ses-lwr', 'msg-lwr-retry'), failedTarget)

    // This time the LWR retry itself fails: it was dispatched by recovery, so
    // its own confirmed failure condemns the provider.
    routing.endProviderStep(runtime, 'ses-lwr', 'msg-lwr-retry', 'run-lwr')
    const condemned = routing.condemnFailedTarget(runtime, 'run-lwr')
    assert.deepEqual(condemned, failedTarget, 'the condemning witness is the failed LWR retry target')
    assert.equal(template.providerCapacity('neuralwatt'), 0, 'a failed LWR retry condemns its provider')
    assert.equal(
      routing.condemnFailedTarget(runtime, 'run-lwr'),
      null,
      'one witness condemns at most once',
    )

    const rotated = await admit(runtime, 'ses-lwr', 'msg-after-condemn')
    assert.deepEqual(rotated, FIRST_CODER_TARGET, 'the dispatch after the condemnation rotates')
  } finally {
    template.clearFailedProviders()
  }
})

// ── the durable fact: an accepted ProviderRetryAttempt continuation ────────

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
    persona: 'Coder',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
})

const profileFor = (session, physical) => {
  const built = authority.createAuthorityRoot(hash, 'rt-lwr-fact', session, 'HumanRoot', physical, rootSelection('coder'))
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

const admittedWithPhysical = (physicalMessageId) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async () => dispatch.admittedWithPhysicalMessage(physicalMessageId),
})

test('WHAT[PAR-021] the_settlement_fact_is_the_durable_provider_retry_attempt_acceptance', async () => {
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
    const accepted = await failureOwner.acceptHumanRoot(handle, 'ses_lwr_fact', 'msg_root', 'coder')
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

// ── the retry payload: the LWR replaces the covered prefix ─────────────────

test('WHAT[PAR-021] the_lwr_retry_payload_replaces_the_covered_prefix_with_the_work_record', () => {
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

// ── one rule for every recovery entry ──────────────────────────────────────

test('WHAT[PAR-021] ordinary_recovery_and_the_delegate_decorator_share_one_settlement', () => {
  const source = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs'),
    'utf8',
  )

  // The workflow never condemns a provider directly: the settlement is the
  // single site, and both outcomes derive from the durable LWR-retry fact.
  assert.equal(source.includes('ModelRouting.markProviderFailed'), false)
  const settlement = source.match(/\bsettleFailedAttemptTarget\b/g) ?? []
  assert.equal(settlement.length, 2, 'declared once, called from the one licensed redispatch')
  const fact = source.indexOf('failedAttemptWasLwrRetry durable turn.SessionId turn.PhysicalUserMessageId')
  const condemn = source.indexOf('ModelRouting.condemnFailedTarget')
  const retain = source.indexOf('ModelRouting.retainFailedTargetForRetry')
  assert.ok(fact !== -1 && condemn > fact && retain > fact, 'both outcomes derive from the durable fact')

  // Ordinary recovery and the SyncDelegate decorator both plug the same
  // redispatch, so neither can grow its own settlement rule.
  const plugs = source.match(/Redispatch =\s*\n?\s*redispatchAfterFailure/g) ?? []
  assert.equal(plugs.length, 2, 'the ordinary entry and the delegate entry share one redispatch')
})
