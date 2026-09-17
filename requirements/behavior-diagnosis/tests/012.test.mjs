import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");
const observation = await import("../../../dist/Enforcer/ObservationSurface.js");

const cycle = ({ run = 'msg-e1', tipRuleId = 'enforcement-tip-1', fieldNameAtCommit = 'field-tip-1', toolCalls = [], evidenceRef } = {}) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blogger',
  run,
  toolCallIds: toolCalls,
  textRef: 'blob-e1',
  textDigest: 'sha-e1',
  tipRuleId,
  fieldNameAtCommit,
  evidenceRef,
  observedPrefixEpoch: 0,
})
const apply = (state, value, n = 1) => {
  const enforcement = observation.applyEnforcementCycle(state.enforcement, cycle(value))
  assert.equal(enforcement.ok, true, enforcement.ok ? '' : enforcement.error)
  const committed = observation.applyBlogEntry(
    {
      frameEpoch: 0,
      previousIngestedThroughSequence: n - 1,
      nextIngestedThroughSequence: n,
      previousCoverableTurnCutoffExclusive: n - 1,
      nextCoverableTurnCutoffExclusive: n,
      nextCoveredPrefixDigest: `d-${n}`,
    },
    observation.blogFrame({
      kind: 'Entry',
      digest: `sha-e${n}`,
      ref: `blob-e${n}`,
      coveredFrom: n - 1,
      coveredThrough: n,
    }),
    state.blog,
  )
  assert.equal(committed.ok, true, committed.ok ? '' : committed.error)
  return { enforcement: enforcement.value, blog: committed.value }
}

test('WHAT[BD-012] ENFORCER_045_cycle_commit_appends_frame_and_advances_coverage', () => {
  const state = apply({ enforcement: observation.emptyEnforcement, blog: observation.emptyBlog }, {
    toolCalls: ['call-1'], tipRuleId: 'enforcement-a01', fieldNameAtCommit: 'primitive-obsession', evidenceRef: 'blob-evidence',
  })
  assert.equal(observation.frameCount(state.blog), 1)
  assert.equal(observation.coverage(state.blog).ingestedThroughSequence, 1)
  assert.equal(observation.coverage(state.blog).coverableTurnCutoffExclusive, 1)
})
test('WHAT[BD-012] ENFORCER_045_enforcement_half_queryable_by_provider_run', () => {
  const state = apply({ enforcement: observation.emptyEnforcement, blog: observation.emptyBlog }, {
    run: 'msg-run1', toolCalls: ['call-a', 'call-b'], tipRuleId: 'enforcement-a01', fieldNameAtCommit: 'primitive-obsession', evidenceRef: 'blob-ev1',
  })
  assert.equal(observation.enforcementRecordCount(state.enforcement), 1)
  const tip = observation.recentTips(state.enforcement)[0]
  assert.equal(tip.ruleId, 'enforcement-a01')
  assert.equal(tip.fieldName, 'primitive-obsession')
})
test('WHAT[BD-012] ENFORCER_045_no_enforcement_cycle_committed_fact_exists', () => {
  assert.throws(() => blog.serializeFact({ case: 'EnforcementCycleCommitted' }), /unknown fact/)
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");

const ROOT = new URL('../../../', import.meta.url).pathname
const prodText = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[BD-012] C0_no_EnforcementCycleCommitted_fact', () => {
  const fact = prodText('src/Wanxiangshu/Composition/Durable/Fact.fs')
  assert.equal(
    /\| EnforcementCycleCommitted\b/.test(fact),
    false,
    'EnforcementCycleCommitted must stay deleted; BlogObservationCommitted is the atomic fact',
  )
  // FactCodec may list it only as a pre-0.5.0 refuse marker (escaped JSON case name).
  const codec = prodText('src/Wanxiangshu/Persistence/Journal/FactCodec.fs')
  assert.ok(
    codec.includes('EnforcementCycleCommitted') && /pre050Markers|pre-0\.5\.0/.test(codec),
    'FactCodec must keep the legacy refuse marker for old journals',
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");

const observationFact = () => ({
  case: 'BlogObservationCommitted',
  sessionId: 'ses_a',
  bloggerSessionId: 'ses_b',
  requestId: 'req-obs',
  frameEpoch: 0,
  previousIngestedThroughSequence: 0,
  nextIngestedThroughSequence: 1,
  previousCoverableTurnCutoffExclusive: 0,
  nextCoverableTurnCutoffExclusive: 1,
  nextCoveredPrefixDigest: 'd-1',
  textRef: 'blobs/blob-obs',
  textDigest: 'sha-obs',
  run: 'run-obs',
  toolCallIds: [],
  tipRuleId: 'rule-obs',
  fieldNameAtCommit: 'field-obs',
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
})
const envelope = (fact) => ({
  runtimeId: 'rt1',
  localSeq: 4,
  observedAt: '2026-01-01T00:00:00.000+00:00',
  eventId: 'e1',
  stream: { kind: 'Session', id: 'ses_a' },
  providerRun: 'run-obs',
  fact,
})

test('WHAT[BD-012] PERSIST_005_envelope_rejects_pre_cutover_observation_tags', () => {
  const line = blog.serializeEnvelope(envelope(observationFact()))
  assert.equal(line.includes('BlogObservationCommitted'), true)
  assert.equal(line.includes('BlogEntryCommitted'), false)

  const preCutover = line.replaceAll('BlogObservationCommitted', 'BlogEntryCommitted')
  const decoded = blog.deserializeEnvelope(preCutover)
  assert.equal(decoded.ok, false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");

const observation = (overrides = {}) => ({
  case: 'BlogObservationCommitted',
  sessionId: 'ses_obs',
  bloggerSessionId: 'ses_blogger',
  requestId: 'req-obs',
  frameEpoch: 0,
  previousIngestedThroughSequence: 0,
  nextIngestedThroughSequence: 1,
  previousCoverableTurnCutoffExclusive: 0,
  nextCoverableTurnCutoffExclusive: 1,
  nextCoveredPrefixDigest: 'd-1',
  textRef: 'blobs/blob-obs',
  textDigest: 'sha-obs',
  run: 'run-obs',
  toolCallIds: [],
  tipRuleId: 'rule-obs',
  fieldNameAtCommit: 'field-obs',
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
  ...overrides,
})
const squashed = () => ({
  case: 'BlogObservationsSquashed',
  sessionId: 'ses_obs',
  bloggerSessionId: 'ses_blogger',
  requestId: 'req-squash',
  previousFrameEpoch: 0,
  nextFrameEpoch: 1,
  coveredFrameCount: 1,
  textRef: 'blobs/blob-squash',
  textDigest: 'sha-squash',
  run: 'run-squash',
})

test('WHAT[BD-012] PERSIST_005_observation_encode_writes_new_tags_only', () => {
  const committed = blog.serializeFact(observation())
  assert.equal(committed.includes('BlogObservationCommitted'), true)
  assert.equal(committed.includes('BlogEntryCommitted'), false)

  const encodedSquash = blog.serializeFact(squashed())
  assert.equal(encodedSquash.includes('BlogObservationsSquashed'), true)
  assert.equal(encodedSquash.includes('BlogSquashCommitted'), false)
})
test('WHAT[BD-012] PERSIST_005_fact_codec_rejects_pre_cutover_observation_tags', () => {
  const committed = blog.serializeFact(observation())
  const preCutoverCommitted = committed.replaceAll('BlogObservationCommitted', 'BlogEntryCommitted')
  assert.equal(preCutoverCommitted.includes('BlogEntryCommitted'), true)
  assert.equal(blog.deserializeFact(preCutoverCommitted).ok, false)

  const encodedSquash = blog.serializeFact(squashed())
  const preCutoverSquash = encodedSquash.replaceAll('BlogObservationsSquashed', 'BlogSquashCommitted')
  assert.equal(blog.deserializeFact(preCutoverSquash).ok, false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const observation = await import("../../../dist/Enforcer/ObservationSurface.js");

const entryFrame = (n) => observation.blogFrame({
  kind: 'Entry',
  digest: `sha-entry-${n}`,
  ref: `blob-entry-${n}`,
  coveredFrom: n - 1,
  coveredThrough: n,
})
const squashFrame = (n) => observation.blogFrame({
  kind: 'Squash',
  digest: `sha-squash-${n}`,
  ref: `blob-squash-${n}`,
  coveredFrom: 0,
  coveredThrough: 2,
})
const cycleRecord = (n, field) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blog',
  run: `msg_tip_${n}`,
  toolCallIds: [],
  textRef: `blob-cycle-${n}`,
  textDigest: `sha-cycle-${n}`,
  tipRuleId: field,
  fieldNameAtCommit: field,
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
})
const commitEntry = (state, { from, to, cutoffFrom, cutoffTo, digest = `digest-${cutoffTo}`, n = 1 }) =>
  observation.applyBlogEntry(
    {
      frameEpoch: 0,
      previousIngestedThroughSequence: from,
      nextIngestedThroughSequence: to,
      previousCoverableTurnCutoffExclusive: cutoffFrom,
      nextCoverableTurnCutoffExclusive: cutoffTo,
      nextCoveredPrefixDigest: digest,
    },
    entryFrame(n),
    state,
  )
const readObs = (enforcement, blog) => observation.observationsOf(enforcement, blog).map((o) => ({
  tipName: o.tipName,
  cycleId: o.cycleId,
  frameDigest: o.frameDigest,
}))

test('WHAT[BD-012] OBS_PROJ_002_zip_recent_tips_with_blog_frame_digests', () => {
  let blog = observation.emptyBlog
  let enforcement = observation.emptyEnforcement

  for (let n = 1; n <= 2; n += 1) {
    const applied = observation.applyEnforcementCycle(enforcement, cycleRecord(n, `field-${n}`))
    assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
    enforcement = applied.value

    const entry = commitEntry(blog, {
      from: n - 1,
      to: n,
      cutoffFrom: n - 1,
      cutoffTo: n,
      n,
    })
    assert.equal(entry.ok, true, entry.ok ? '' : entry.error)
    blog = entry.value
  }

  assert.deepEqual(readObs(enforcement, blog), [
    { tipName: 'field-1', cycleId: 'msg_tip_1', frameDigest: 'sha-entry-1' },
    { tipName: 'field-2', cycleId: 'msg_tip_2', frameDigest: 'sha-entry-2' },
  ])
})
}
