import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const source = async (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[EMR-005] EMR_005_runtime_contains_no_product_lane_or_max_sessions_policy', async () => {
  const routing = await source('src/Wanxiangshu/OpenCode/Host/ModelRouting.fs')
  assert.doesNotMatch(routing, /ExecutionLane|ModelLaneConfig|max_sessions|firstFree|first-free/)
})

test('WHAT[EMR-010] EMR_010_borrowing_complexity_is_owned_only_by_the_capacity_decorator', async () => {
  const capacity = await source('src/Wanxiangshu/OpenCode/Host/ModelCapacity.fs')
  const routing = await source('src/Wanxiangshu/OpenCode/Host/ModelRouting.fs')
  const sessions = await source('src/Wanxiangshu/OpenCode/Host/Sessions.fs')
  const binding = await source('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
  const transform = await source('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')
  const host = await source('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const scheduler = await source('resources/wanxiangshu.mjs')

  assert.match(capacity, /type CapacityLedger<'target>/)
  assert.match(capacity, /type BorrowingCapacity<'target>/)
  assert.match(capacity, /ancestorDistance/)
  assert.match(capacity, /CapacityTokenState/)
  assert.match(routing, /BorrowingCapacity<ModelRoutingTarget>/)
  assert.doesNotMatch(routing, /let ancestorDistance|type private CapacityTokenState|type private CapacityStepDemand/)

  for (const main of [sessions, binding, transform, host]) {
    assert.doesNotMatch(main, /ancestorDistance|CapacityTokenState|CapacityStepDemand|ownedTokenByExecution/)
  }
  assert.doesNotMatch(scheduler, /borrow|recall|lineage|parentSession|childSession/i)
})

test('WHAT[EMR-008] EMR_008_host_inventory_no_longer_exposes_model_binding_authority', async () => {
  const managed = await source('src/Wanxiangshu/OpenCode/Host/ManagedAgentConfig.fs')
  const port = await source('src/Wanxiangshu/OpenCode/Host/OpenCodePort.fs')
  const wiring = await source('src/Wanxiangshu/OpenCode/Plugin/PluginSessionWiring.fs')
  const strengthScope = await source('src/Wanxiangshu/Strength/OpenCode/PluginScope.fs')

  assert.doesNotMatch(managed, /tryBoundModel|tryOpencodeModel|DuplicatePairModel|liveInventory|Model:\s*string/)
  assert.doesNotMatch(port, /ManagedAgentConfig\.tryBoundModel/)
  assert.doesNotMatch(wiring, /ManagedAgentInventory|tryOpencodeModel|promptModelFor/)
  assert.doesNotMatch(strengthScope, /ManagedAgentInventory|RecordManagedAgentInventory/)
})

test('WHAT[EMR-009] EMR_009_chat_message_is_the_single_managed_execution_admission_owner', async () => {
  const host = await source('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const routing = await source('src/Wanxiangshu/OpenCode/Host/ModelRouting.fs')
  const binding = await source('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
  const sessions = await source('src/Wanxiangshu/OpenCode/Host/Sessions.fs')
  const params = await source('src/Wanxiangshu/OpenCode/Host/ChatParamsHook.fs')

  assert.match(host, /decoded\.PhysicalUserMessageId/)
  assert.match(host, /ModelRouting\.routeChatExecution/)
  assert.doesNotMatch(host, /ModelRoutingAcquisition|ChatExecutionAdmission\.(NoRoute|Rejected|ExternalManaged|PluginManaged)/)
  assert.match(routing, /let routeChatExecution/)
  assert.match(routing, /acquireManagedExecution/)
  assert.match(host, /SessionExecutionBinding\.acceptRoutedExecution/)
  assert.doesNotMatch(host, /acceptPromptExecution|acceptExternalExecution/)
  assert.match(host, /fun \(claim: PromptAuthority\.PromptClaim\) ->[\s\S]{0,100}runtime\.DispatchAccepted\(claim\.SessionId, claim\)/)
  assert.match(binding, /let acceptRoutedExecution/)
  assert.doesNotMatch(binding, /PromptDispatcher|DispatchAccepted/)
  assert.doesNotMatch(sessions, /ModelRouting\.acquireManagedExecution/, 'fork/send enqueue must never wait for model capacity')
  assert.match(host, /message\?model\s*<-\s*box routed/)
  assert.match(params, /validateObservedProvider/)
  assert.doesNotMatch(params, /observeUserFacing\s/)
})

test('WHAT[EMR-007] EMR_007_exact_terminal_identity_releases_capacity_not_coarse_idle_or_business_completion', async () => {
  const host = await source('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const codec = await source('src/Wanxiangshu/OpenCode/Codec/HostEventCodec.fs')
  const ordinary = await source('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')

  assert.match(codec, /tryDecodePhysicalExecutionEnd/)
  assert.match(codec, /isMessageUpdated = not \(isNull raw\) && eventTypeOf raw = "message\.updated"/)
  assert.match(codec, /info\?parentID/)
  assert.match(host, /onPhysicalExecutionEnd =\s*\(fun .* ->[\s\S]*ModelRouting\.releasePhysicalExecution/)
  assert.doesNotMatch(host, /SessionIdle sessionId[\s\S]{0,260}ModelRouting\.releaseExecution sessionId/)
  assert.doesNotMatch(host, /AttemptAborted sessionId[\s\S]{0,260}ModelRouting\.releaseExecution sessionId/)
  assert.doesNotMatch(ordinary, /ModelRouting\.(releaseExecution|releaseSession)/,
    'application completion/finality must not own physical capacity release')
})

test('WHAT[EMR-007] EMR_007_chat_message_closes_the_old_idle_window_before_model_admission', async () => {
  const host = await source('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const chatHook = host.slice(host.indexOf('let chatMessageHook ='), host.indexOf('let cancelSignals'))
  const barrier = host.slice(host.indexOf('let observePhysicalAdmission'), host.indexOf('let chatMessageHook ='))
  const ordinaryRouting = host.slice(
    host.indexOf('let continueOrdinaryChatMessage'),
    host.indexOf('let chatMessageHook ='),
  )
  const revoke = barrier.indexOf('Quiescence.ObservePhysicalUserMessage')
  const invoke = chatHook.indexOf('observePhysicalAdmission output sessionId physicalId')
  const ordinaryCall = chatHook.indexOf('continueOrdinaryChatMessage decoded input output')
  const classify = ordinaryRouting.indexOf('ModelRouting.chatExecutionAdmission')
  const acquire = ordinaryRouting.indexOf('ModelRouting.routeChatExecution')

  assert.notEqual(revoke, -1, 'chat.message must close the preceding idle-send window')
  assert.notEqual(invoke, -1, 'chat.message must invoke the named physical admission barrier')
  assert.notEqual(ordinaryCall, -1, 'ordinary routing must cross the named boundary after physical admission')
  assert.notEqual(classify, -1)
  assert.notEqual(acquire, -1)
  assert.ok(invoke < ordinaryCall, 'physical ingress barrier must run before ordinary routing begins')
  assert.ok(classify < acquire, 'routing classification must precede managed model acquisition')
})

test('WHAT[EMR-008] SPEC_INV_fast_and_deep_physical_model_equality_is_not_an_eligibility_gate', async () => {
  const policy = await source('src/Wanxiangshu/Strength/Policy.fs')
  const speculate = await source('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')

  assert.doesNotMatch(policy, /ModelBindingsDistinct|model-bindings-not-distinct/)
  assert.doesNotMatch(speculate, /ManagedAgentInventory|modelsDistinct|fastBinding/)
})
