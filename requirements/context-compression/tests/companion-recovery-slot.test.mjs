import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import * as slot from '../../../dist/Context/Companion/CompressionSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname
const PROD = join(ROOT, 'src/Wanxiangshu')

const walk = (dir, acc = []) => {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)
    const stat = statSync(path)
    if (stat.isDirectory()) walk(path, acc)
    else if (path.endsWith('.fs')) acc.push(path)
  }
  return acc
}

const production = walk(PROD)
const source = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[CONTEXT-COMPRESSION-006] CTX_006_opportunity_is_failure_plus_primed_not_material', () => {
  assert.equal(slot.recoveryOpportunity(slot.afterFailureAdvance, 1), 'RecoveryAttempt')
  assert.equal(slot.recoveryOpportunity(slot.afterFailureAdvance, 3), 'RecoveryAttempt')
  assert.equal(slot.recoveryOpportunity(slot.afterFailureAdvance, 0), 'OrdinaryAttempt')
  assert.equal(slot.recoveryOpportunity(slot.beginSequence, 1), 'OrdinaryAttempt')

  // Material is a later proof. It changes whether recovery can act, not whether
  // this physical attempt owns the one-shot recovery opportunity.
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 1, false), false)
  assert.equal(slot.recoveryOpportunity(slot.afterFailureAdvance, 1), 'RecoveryAttempt')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_primed_blogger_main_with_frames_dispatches_squash_first', () => {
  assert.equal(slot.nextBloggerRequest('blogger-main', 'RecoveryAttempt', true), 'blogger-squash')
  assert.equal(slot.nextBloggerRequest('blogger-main', 'RecoveryAttempt', false), 'blogger-main')
  assert.equal(slot.nextBloggerRequest('blogger-main', 'OrdinaryAttempt', true), 'blogger-main')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_failed_squash_always_leaves_the_slot_before_main', () => {
  assert.equal(slot.nextBloggerRequest('blogger-squash', 'OrdinaryAttempt', true), 'blogger-main')
  assert.equal(slot.nextBloggerRequest('blogger-squash', 'RecoveryAttempt', true), 'blogger-main')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_blogger_dispatch_distinguishes_missing_projection_from_no_active_run', () => {
  assert.equal(slot.nextBloggerRequest('missing', 'RecoveryAttempt', true), 'MissingProjection')
  assert.equal(slot.nextBloggerRequest('work-main', 'RecoveryAttempt', true), 'NoActiveBloggerRun')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_future_X_material_waiter_is_deleted_from_production', () => {
  const forbidden = /StartRecoveryOpportunity|OfferRecoveryMaterial|recoveryWaiter|ReArmRecovery/
  const hits = production
    .filter((file) => forbidden.test(readFileSync(file, 'utf8')))
    .map((file) => file.slice(ROOT.length))

  assert.deepEqual(hits, [], `parallel recovery waiter/re-arm state remains: ${hits.join(', ')}`)

  const coordinator = source('src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs')
  assert.doesNotMatch(coordinator, /tryStartSquash|OfferRecoveryMaterial/)

  const workflow = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs')
  assert.match(workflow, /FallbackLedger\.recordConfirmedFailure/)
  assert.match(workflow, /RecoverySlot\.nextBloggerRequest/)
  assert.match(workflow, /replaceFailedBloggerRequest/)
})

test('WHAT[CONTEXT-COMPRESSION-022] CTX_022_all_production_main_rebuilds_share_BloggerMainContext', () => {
  const coordinator = source('src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs')
  const transform = source('src/Wanxiangshu/Context/Companion/Transform.fs')
  const enforcer = source('src/Wanxiangshu/Enforcer/Continuation.fs')
  const recovery = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs')

  assert.match(coordinator, /onMainContext/)
  assert.doesNotMatch(coordinator, /ProviderSemanticProjection|BloggerMainContext\.fromProjection/)
  assert.match(transform, /XTraceMaterialization\.currentProjection/)
  assert.match(transform, /BloggerMainContext\.hasMaterial/)
  assert.match(transform, /BloggerMainContext\.fromProjection/)
  assert.doesNotMatch(
    transform,
    /ProviderWireCapture\.decodeMessageView/,
    'normal Blogger materialization must never derive coverage from request-local provider presentation',
  )
  assert.match(enforcer, /BloggerMainContext\.fromJournal/)
  assert.match(recovery, /BloggerMainContext\.fromJournal/)

  for (const [name, text] of [
    ['Coordinator', coordinator],
    ['Transform', transform],
    ['Enforcer', enforcer],
    ['Recovery', recovery],
  ]) {
    assert.doesNotMatch(text, /BloggerDelta\.nextChunk/, `${name} must not own a second next-main formula`)
  }
})

test('WHAT[PAR-017] PAR_017_blogger_retry_abandons_then_materializes_then_binds_new_prompt', () => {
  const workflow = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs')

  assert.match(
    workflow,
    /let private replaceFailedBloggerRequest[\s\S]*abandonContinuationContext[\s\S]*sendStagedBloggerContinuation/,
  )
  assert.match(
    workflow,
    /sendStagedBloggerContinuation[\s\S]*taskResult[\s\S]*materializeContinuationContext[\s\S]*sendContinuation[\s\S]*bindContinuationContext/,
  )
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_all_materialization_owners_share_admission_and_nonoverwrite_flight', () => {
  const coordinator = source('src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs')
  const recovery = source('src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs')
  const scope = source('src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs')
  const allProduction = production.map((file) => readFileSync(file, 'utf8')).join('\n')

  assert.match(coordinator, /let private materializeRequest/)
  assert.match(coordinator, /let private withMaterialization[\s\S]*AcquireMaterialization/)
  assert.match(
    coordinator,
    /materializeContinuationContext[\s\S]*withMaterialization[\s\S]*foreignFlightReason[\s\S]*materializeRequest/,
  )
  assert.match(
    coordinator,
    /claimMaterializedContinuation[\s\S]*claimCurrentRequest[\s\S]*abandonRequest/,
    'a post-materialize claim conflict must close the durable request before returning error',
  )
  assert.match(
    coordinator,
    /failBeforeFlightClaim[\s\S]*abandonRequest[\s\S]*StartFailed/,
    'normal start claim conflict must abandon its materialized request without clearing a foreign flight',
  )
  assert.match(coordinator, /bindContinuationContext[\s\S]*withMaterialization/)
  assert.match(coordinator, /abandonContinuationContext[\s\S]*withMaterialization/)
  assert.match(
    coordinator,
    /startWithJournal[\s\S]*withMaterialization[\s\S]*scope\.HasFlight[\s\S]*materializeThenSend/,
  )
  assert.match(recovery, /AcquireMaterialization/)
  assert.match(scope, /BloggerFlightClaim\.Conflict/)
  assert.match(scope, /BloggerFlightRelease\.Conflict/)
  assert.doesNotMatch(allProduction, /\bSetCurrentRequest\b|\bClearCurrentRequest\b/)
})

const recoveryWaitSource = () => {
  const workflow = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs')
  return workflow.slice(
    workflow.indexOf('let private hasOpenBloggerRequest'),
    workflow.indexOf('let private currentFallback'),
  )
}

test('WHAT[CONTEXT-COMPRESSION-023] recovery_wait_has_no_clock_or_process_local_correctness_state', () => {
  const wait = recoveryWaitSource()

  assert.match(wait, /AgentJournal\.snapshotWithRevision/)
  assert.match(wait, /AgentJournal\.awaitChangeFromOrCancel/)
  assert.match(wait, /host\.Cancellation/)
  assert.doesNotMatch(wait, /HasFlight|HasPendingOffer|TimeSpan|ITimerPort|\.Delay\b|deadline|timeout|sleep/i)
})

test('WHAT[PAR-018] recovery_continuation_waits_only_on_durable_open_producer_events', () => {
  const wait = recoveryWaitSource()

  assert.match(wait, /BloggerCycleProjection\.tryOpenByBlogger/)
  assert.match(wait, /sessionHasFreshCoverage/)
  assert.match(wait, /RecoveryMaterialState\.AwaitCommittedFact/)
  assert.match(wait, /AgentJournal\.awaitChangeFromOrCancel/)
  assert.doesNotMatch(wait, /HasFlight|HasPendingOffer|TimeSpan|ITimerPort|\.Delay\b|deadline|timeout|sleep/i)
})
