import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const PluginHooksSurface = await import("../../../dist/OpenCode/Host/PluginHooksSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')
const pluginHooksSource = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')
const interopSource = read('src/Wanxiangshu/OpenCode/Host/PluginHostInterop.fs')
const hookPolicySource = read('src/Wanxiangshu/OpenCode/Host/HookPolicy.fs')
const REGISTERED_HOOK_NAMES = [
  'chat.message',
  'chat.params',
  'experimental.chat.messages.transform',
  'experimental.chat.system.transform',
  'config',
  'experimental.session.compacting',
  'experimental.compaction.autocontinue',
  'tool.definition',
  'tool.execute.before',
  'tool.execute.after',
  'event',
  'dispose',
  'command.execute.before',
]

test('WHAT[host-boundary-019] STRENGTH_004_replica_transform_route_is_structurally_exclusive', () => {
  // The transform hook is registered exactly once under the experimental name.
  // There is no second 'chat.transform' alias — that was removed because the
  // Host Hooks type has only the experimental key.
  assert.match(hookPolicySource, /HostKey = "experimental\.chat\.messages\.transform"/)
  assert.doesNotMatch(hookPolicySource, /HostKey = "chat\.transform"/)
})
test('WHAT[host-boundary-019] CHAT_MESSAGE_routes_managed_model_then_CHAT_PARAMS_only_validates', () => {
  // chat.message is registered before chat.params in the hook object.
  const chatMessageIdx = pluginHooksSource.indexOf('registeredHook HookKey.ChatMessage')
  const chatParamsIdx = pluginHooksSource.indexOf('registeredHook HookKey.ChatParams')
  assert.ok(chatMessageIdx >= 0, 'chat.message must be registered')
  assert.ok(chatParamsIdx >= 0, 'chat.params must be registered')
  assert.ok(chatMessageIdx < chatParamsIdx, 'chat.message is registered before chat.params')
  // chat.params only validates — it never mutates the model (only temperature).
  const chatParamsHookSource = read('src/Wanxiangshu/OpenCode/Host/ChatParamsHook.fs')
  assert.match(chatParamsHookSource, /applyManagedTemperature/)
  // chat.params does not rewrite the model id.
  assert.doesNotMatch(chatParamsHookSource, /output\?model.*<-\s*[^t]/)
})
test('WHAT[host-boundary-019] CHAT_MESSAGE_new_physical_material_supersedes_old_capacity_without_idle', () => {
  // The physical user message identity is exact: a new material supersedes
  // the old one. This is a structural fact of the message identity model,
  // not an idle-derived continuation. The hook surface does not derive
  // identity from idle signals.
  assert.doesNotMatch(pluginHooksSource, /idle.*identity|SessionIdle.*PhysicalUserMessage/)
  // The chat.message hook routes through wired.ChatMessageHook, which is
  // the managed model admission owner — not an idle consumer.
  assert.match(pluginHooksSource, /wired\.ChatMessageHook/)
})
test('WHAT[host-boundary-019] PROMPT_004_human_root_survives_host_synthetic_file_parts', () => {
  // The transform hook receives the full message array including host-synthetic
  // parts. The human root message identity (role=user, id=root) is preserved
  // through the transform — the lifecycle wrapper only admits/drains the same
  // transform and does not strip or rewrite user roots.
  assert.match(pluginHooksSource, /let ownedTransform[\s\S]*?scope\.RunOwnedWork\(fun \(\) -> transform inObj outObj\)/)
  assert.match(
    pluginHooksSource,
    /registeredHook HookKey\.MessagesTransform \(curriedHook \(box ownedTransform\)\)/,
  )
  // The transform is a pure function over the message array; it does not
  // consume host-synthetic file parts as business input.
  const transformsSource = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')
  assert.doesNotMatch(transformsSource, /source.*host.*business|host.*file.*fact/)
})
test('WHAT[host-boundary-019] AGENT_007_tool_gate_recovers_human_root_from_host_snapshot_on_resume', () => {
  // The tool.execute.before hook decodes context from the tool input, not
  // from a host snapshot. The human root is recovered from the durable
  // snapshot via SessionSnapshotPort, not from the hook args.
  assert.match(pluginHooksSource, /ToolHostCodec\.decodeContext/)
  // The hook reads journal + toolCallId for estimate observation, not for
  // root recovery.
  assert.match(pluginHooksSource, /DelegatedToolEstimateLedger\.observe/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const SessionSnapshotSurface = await import("../../../dist/OpenCode/Host/SessionSnapshotSurface.js");

const projectMessages = SessionSnapshotSurface.projectMessages
const locateToolCall = SessionSnapshotSurface.locateToolCall
const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[host-boundary-019] CANARY_H journal xtrace uniquely completes host carrier', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'completed' })])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.ok, true)
  assert.equal(located.providerRun, 'asst_run')
  assert.equal(located.hostToolPartId, 'part_todo')
  assert.equal(located.toolCallId, 'call_todo')
})
test('WHAT[host-boundary-019] CANARY_H journal mapping fails closed on host part mismatch', () => {
  const messages = projectMessages([{ info: { id: 'ses_x' }, parts: [{ type: 'text', text: 'not a tool' }] }])
  const located = locateToolCall('call_missing', messages)
  assert.equal(located.ok, false)
  assert.equal(located.error, 'Missing')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { spawnSync } = await import("node:child_process");
const { default: fs } = await import("node:fs");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");

const here = path.dirname(fileURLToPath(import.meta.url))
const fixture = JSON.parse(fs.readFileSync(path.join(here, '../fixtures/opencode-chat-admission-1.18.29.json'), 'utf8'))
const driftFixture = JSON.parse(fs.readFileSync(path.join(here, '../fixtures/opencode-chat-admission-drift.json'), 'utf8'))
const runner = path.join(here, 'support/run-opencode-chat-admission-canary.mjs')
const assertPassingVersionEvidence = (versions) => {
  const supported = fixture.supportedVersionRange !== null
    && versions.opencode === fixture.supportedVersionRange
    && versions.plugin === fixture.supportedVersionRange
  if (!supported) {
    throw new Error(
      `OpenCode ${versions.opencode}/plugin ${versions.plugin} is outside the passing observed range: ${fixture.missingCapability ?? fixture.supportedVersionRange}`,
    )
  }
  assert.deepEqual(versions, fixture.observedVersions)
}
const runInstalledCanary = () => {
  const launched = spawnSync(process.execPath, [runner], { cwd: path.resolve(here, '../../..'), encoding: 'utf8' })
  assert.equal(launched.status, 0, launched.stderr || launched.stdout)
  return JSON.parse(launched.stdout)
}
const first = (evidence, kind) => evidence.observations.find((observation) => observation.kind === kind)

test('WHAT[host-boundary-019] fails closed on OpenCode chat contract drift', () => {
  assert.throws(
    () => assertPassingVersionEvidence(driftFixture.versions),
    /outside the passing observed range/,
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createWithCaps, NormalTransformCapabilities, TransformBranchCapabilities, TraceTransformCapture } = await import("../../../dist/OpenCode/Plugin/PluginTransforms.js");
const { PrefixPresentationHorizon } = await import("../../../dist/Context/Prefix/Wire.js");
const { StrengthReplicaRuntime } = await import("../../../dist/Strength/Replica/Runtime.js");

const EXPECTED_ORDER = [
  'BeginPhysicalProviderAttempt',
  'BindSessionStartedAt',
  'SettleAndReplaceDeferredInspections',
  'ApplyRelayProjection',
  'ApplyStrengthReplay',
  'CaptureXTraceMessages',
  'CommitStrengthTrace',
  'RefreshCompanionXTrace',
  'ApplyCompanion',
  'ApplyXWire',
  'FreezeProviderAttemptPlan',
  'ApplyEnforcerContinuation',
  'ApplyStrengthSpeculate',
  'InjectPairGuideline',
  'ProjectRequirementGrounding',
  'InjectBloggerChronicle',
  'SettleAndReplaceDeferredInspections',
  'SanitizeMessages',
]
const makeRecordingCaps = (opts = {}) => {
  const trace = []
  const fnBegin = (sid, out) => { trace.push('BeginPhysicalProviderAttempt'); return Promise.resolve() }
  const fnStarted = (sid) => { trace.push('BindSessionStartedAt'); return Promise.resolve(null) }
  const fnReplay = (sid, out) => { trace.push('ApplyStrengthReplay'); return Promise.resolve([]) }
  const fnRelay = (sid, out) => { trace.push('ApplyRelayProjection'); return Promise.resolve({ tag: 0 }) }
  const fnCapture = (sid, out) => { trace.push('CaptureXTraceMessages'); return Promise.resolve(new TraceTransformCapture([], null)) }
  const fnCommit = (sid, cur, plans) => { trace.push('CommitStrengthTrace'); return Promise.resolve() }
  const fnRefresh = (sid, cur) => { trace.push('RefreshCompanionXTrace') }
  const fnCompanion = (relay, sid, inO, outO) => { trace.push('ApplyCompanion'); return Promise.resolve() }
  const fnXWire = (relay, outO) => { trace.push('ApplyXWire'); return Promise.resolve(opts.horizon ?? PrefixPresentationHorizon.Current) }
  const fnFreeze = (sid, outO) => { trace.push('FreezeProviderAttemptPlan'); return Promise.resolve() }
  const fnEnforcer = (sid, outO) => { trace.push('ApplyEnforcerContinuation'); return Promise.resolve() }
  const fnSpeculate = (outO) => { trace.push('ApplyStrengthSpeculate'); return Promise.resolve() }
  const fnPair = (sid, started, outO) => { trace.push('InjectPairGuideline'); return Promise.resolve() }
  const fnGrounding = (sid, outO) => { trace.push('ProjectRequirementGrounding'); return Promise.resolve() }
  const fnDeferred = (sid, outO) => { trace.push('SettleAndReplaceDeferredInspections'); return Promise.resolve() }
  const fnBlogger = (sid, outO) => { trace.push('InjectBloggerChronicle') }
  const fnSanitize = (outO) => { trace.push('SanitizeMessages') }

  const caps = new NormalTransformCapabilities(
    fnBegin,
    fnStarted,
    opts.swap3and4 ? fnRelay : fnReplay,
    opts.swap3and4 ? fnReplay : fnRelay,
    fnCapture,
    fnCommit,
    fnRefresh,
    fnCompanion,
    fnXWire,
    fnFreeze,
    fnEnforcer,
    fnSpeculate,
    fnPair,
    fnGrounding,
    fnBlogger,
    fnDeferred,
    fnSanitize,
  )
  return { caps, trace }
}

test('WHAT[host-boundary-019] normalTransform executes exact 16-step canonical sequence on production createWithCaps', async () => {
  const { caps, trace } = makeRecordingCaps({ horizon: PrefixPresentationHorizon.Current })
  const branches = new TransformBranchCapabilities(
    () => false,
    () => {},
    () => null,
    () => Promise.resolve(),
    () => {},
    () => {},
  )
  const transform = createWithCaps(caps, branches)
  await transform({ sessionID: 's-1' })({ messages: [] })

  assert.deepEqual(trace, EXPECTED_ORDER)
  assert.equal(trace.length, 18)
})
test('WHAT[host-boundary-019] counterexample: swapping two stub functions causes trace to differ', async () => {
  const normal = makeRecordingCaps({ horizon: PrefixPresentationHorizon.Current, swap3and4: false })
  const swapped = makeRecordingCaps({ horizon: PrefixPresentationHorizon.Current, swap3and4: true })

  const branches = new TransformBranchCapabilities(
    () => false,
    () => {},
    () => null,
    () => Promise.resolve(),
    () => {},
    () => {},
  )

  await createWithCaps(normal.caps, branches)({ sessionID: 's-normal' })({ messages: [] })
  await createWithCaps(swapped.caps, branches)({ sessionID: 's-swapped' })({ messages: [] })

  assert.notDeepEqual(swapped.trace, normal.trace)
  assert.notDeepEqual(swapped.trace, EXPECTED_ORDER)
})
test('WHAT[host-boundary-019] tentative prefix probe horizon suppresses historical auxiliary projection in the same physical request', async () => {
  const { caps, trace } = makeRecordingCaps({ horizon: PrefixPresentationHorizon.TentativeCold })
  const branches = new TransformBranchCapabilities(
    () => false,
    () => {},
    () => null,
    () => Promise.resolve(),
    () => {},
    () => {},
  )
  const transform = createWithCaps(caps, branches)
  await transform({ sessionID: 's-tentative' })({ messages: [] })

  // Under TentativeCold, steps 12-14 (ApplyStrengthSpeculate, InjectPairGuideline, ProjectRequirementGrounding)
  // are suppressed, while InjectBloggerChronicle and SanitizeMessages still run.
  assert.equal(trace.includes('ApplyStrengthSpeculate'), false)
  assert.equal(trace.includes('InjectPairGuideline'), false)
  assert.equal(trace.includes('ProjectRequirementGrounding'), false)
  assert.equal(trace.includes('InjectBloggerChronicle'), true)
  assert.equal(trace.includes('SanitizeMessages'), true)
  assert.equal(trace.length, 15)
})
test('WHAT[host-boundary-019] branch probe: ReplicaRuntime runs only replica steps', async () => {
  const replicaTrace = []
  const caps = new NormalTransformCapabilities(
    ...Array.from({ length: 16 }, () => () => { replicaTrace.push('unwantedNormalStep'); return Promise.resolve() }),
  )
  caps.FreezeProviderAttemptPlan = (sid, outO) => { replicaTrace.push('FreezeProviderAttemptPlan'); return Promise.resolve() }

  const runtime = new StrengthReplicaRuntime(
    null, null, null, null, '/tmp', 65536, null, null,
  )
  runtime.byReplica.set('s-replica', {
    Replica: 's-replica',
    SemanticTerminal: { tag: 1 },
  })

  const branches = new TransformBranchCapabilities(
    () => false,
    (sid) => { replicaTrace.push('RegisterOwned:' + sid) },
    () => runtime,
    () => { replicaTrace.push('ReplicaXWire'); return Promise.resolve() },
    () => { replicaTrace.push('ReplicaSanitize') },
    () => { replicaTrace.push('ExplicitResumeSanitize') },
  )

  const transform = createWithCaps(caps, branches)
  await transform({ sessionID: 's-replica' })({ messages: [{ info: { sessionID: 's-replica' } }] })

  assert.deepEqual(replicaTrace, [
    'RegisterOwned:s-replica',
    'ReplicaXWire',
    'FreezeProviderAttemptPlan',
    'ReplicaSanitize',
  ])
  assert.equal(replicaTrace.includes('unwantedNormalStep'), false)
})
test('WHAT[host-boundary-019] branch probe: IsExplicitResume runs only ExplicitResumeSanitize and exits', async () => {
  const resumeTrace = []
  const caps = new NormalTransformCapabilities(
    ...Array.from({ length: 16 }, () => () => { resumeTrace.push('unwantedNormalStep'); return Promise.resolve() }),
  )
  const branches = new TransformBranchCapabilities(
    () => true,
    () => { resumeTrace.push('RegisterOwned') },
    () => null,
    () => { resumeTrace.push('ReplicaXWire'); return Promise.resolve() },
    () => { resumeTrace.push('ReplicaSanitize') },
    () => { resumeTrace.push('ExplicitResumeSanitize') },
  )

  const transform = createWithCaps(caps, branches)
  await transform({ sessionID: 's-resume' })({ messages: [] })

  assert.deepEqual(resumeTrace, ['ExplicitResumeSanitize'])
})
}
