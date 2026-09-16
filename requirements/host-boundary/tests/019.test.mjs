// requirements/host-boundary/tests/ordered-transform.test.mjs — WHAT[HOST-BOUNDARY-019]
//
// OrderedTransformProof: verifies the 16-step static semantic score of
// PluginTransforms.normalTransform alongside the StrengthReplica and
// ExplicitResumeSuppression branch isolation paths without illegal deep dist imports.

import assert from 'node:assert/strict'
import test from 'node:test'
import { createWithCaps, NormalTransformCapabilities, TransformBranchCapabilities, TraceTransformCapture } from '../../../dist/OpenCode/Plugin/PluginTransforms.js'
import { PrefixPresentationHorizon } from '../../../dist/Context/Prefix/Wire.js'
import { StrengthReplicaRuntime } from '../../../dist/Strength/Replica/Runtime.js'

const EXPECTED_ORDER = [
  'BeginPhysicalProviderAttempt',
  'BindSessionStartedAt',
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
    fnSanitize,
  )
  return { caps, trace }
}

test('WHAT[HOST-BOUNDARY-019] normalTransform executes exact 16-step canonical sequence on production createWithCaps', async () => {
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
  assert.equal(trace.length, 16)
})

test('WHAT[HOST-BOUNDARY-019] counterexample: swapping two stub functions causes trace to differ', async () => {
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

test('WHAT[HOST-BOUNDARY-019] tentative prefix probe horizon suppresses historical auxiliary projection in the same physical request', async () => {
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
  assert.equal(trace.length, 13)
})

test('WHAT[HOST-BOUNDARY-019] branch probe: ReplicaRuntime runs only replica steps', async () => {
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

test('WHAT[HOST-BOUNDARY-019] branch probe: IsExplicitResume runs only ExplicitResumeSanitize and exits', async () => {
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
