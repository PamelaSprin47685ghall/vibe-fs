import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const planner = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const xwire = await import("../../../dist/Context/Prefix/XWireSurface.js");
const { budget } = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const requestKind = prefix.requestKind
const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })
const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => ({
  probeId: id,
  basedOnEpoch: 0,
  candidate: snapshotAt(cutoff),
})

test('WHAT[context-compression-002] successful retry tool steps keep the committed prefix despite new coverage', () => {
  const projection = {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'opening' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'first result' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'second result' }] },
    ],
  }
  const failed = budget.recordFailure(budget.initial)
  const input = {
    journal: true,
    sessionId: 'retry-session',
    acceptedRetry: true,
    physicalUser: 'retry-user',
    acceptedPhysicalUser: 'retry-user',
    prefixEpoch: 0,
    failures: failed.failures,
    currentProjection: projection,
    committedSnapshot: null,
    coverableCutoff: 1,
    requestStartCutoff: 2,
    coveredDigest: xwire.coveredPrefixDigest(projection, 1),
    frozenRecordPrefixRef: 'frozen-first',
    frozenRecordPrefixDigest: 'digest-first',
    frozenRecordPrefixBody: 'first frozen record',
    memoryPreamble: 'prior responsibility',
    outcome: 'tool-calls',
  }
  const first = xwire.transform(input)
  assert.equal(first.probe.candidate.cutoff, 1)
  assert.equal(first.promoted, true)

  const succeeded = budget.recordSuccess(failed)
  const nextInput = {
    ...input,
    failures: succeeded.failures,
    prefixEpoch: 1,
    committedSnapshot: first.probe.candidate,
    coverableCutoff: 2,
    coveredDigest: xwire.coveredPrefixDigest(projection, 2),
    frozenRecordPrefixRef: 'frozen-later',
    frozenRecordPrefixDigest: 'digest-later',
    outcome: null,
  }
  const next = xwire.transform(nextInput)
  assert.equal(next.probe, null, 'a retained retry row is not a new failure')
  assert.deepEqual(next.output, first.output, 'new coverage must not replace the sealed prefix')

  const failedAgain = budget.recordFailure(succeeded)
  const recovery = xwire.transform({ ...nextInput, failures: failedAgain.failures })
  assert.equal(recovery.probe.candidate.cutoff, 2, 'a new failure may select the newer coverage')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");

const entryFrame = (n) => blog.frame({
  kind: 'Entry',
  digest: `sha-entry-${n}`,
  ref: `blob-entry-${n}`,
  coveredFrom: n - 1,
  coveredThrough: n,
})
const squashFrame = (n) => blog.frame({
  kind: 'Squash',
  digest: `sha-entry-${n}`,
  ref: `blob-squash-${n}`,
  coveredFrom: 0,
  coveredThrough: 2,
})
const commitEntry = (state, { epoch = 0, from, to, cutoffFrom, cutoffTo, digest = `digest-${cutoffTo}`, n = 1 }) =>
  blog.applyEntry(
    {
      epoch,
      previous: from,
      next: to,
      previousCutoff: cutoffFrom,
      nextCutoff: cutoffTo,
      digest,
      frame: entryFrame(n),
    },
    state,
  )
function threeEntries() {
  let state = blog.empty

  for (let i = 1; i <= 3; i += 1) {
    const result = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i })
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }

  return state
}

test('WHAT[context-compression-002] HOST_006_reanchor_zeroes_prefix_coverage_and_keeps_record_coverage', () => {
  let state = threeEntries()
  const framesBefore = blog.frameKinds(state)
  const ingestBefore = blog.coverage(state).ingestedThroughSequence
  assert.equal(blog.hasCoverage(state), true)
  assert.equal(ingestBefore, 3)

  const reanchored = blog.applyReanchor(state)

  // B records work that really happened. What compaction voided is the mapping
  // from B to X turn indices, not the work log — so frames survive untouched.
  assert.deepEqual(blog.frameKinds(reanchored), framesBefore)
  assert.equal(blog.frameCount(reanchored), blog.frameCount(state))

  // PrefixCoverage returns to the origin: Host numbering those positions referred
  // to no longer exists. RecordCoverage (IngestedThrough) is an XTrace cursor and
  // stays put — clearing it would re-feed already-compressed X into Y (COMPANION-008).
  assert.deepEqual(blog.coverage(reanchored), {
    ingestedThroughSequence: ingestBefore,
    cutoff: 0,
    digest: '',
    coverableFrames: 0,
  })
  assert.equal(blog.hasCoverage(reanchored), false)
  assert.deepEqual(blog.coverableFrameKinds(reanchored), [], 'no probe may be built until prefix coverage rebuilds')
})
test('WHAT[context-compression-002] HOST_006_reanchor_does_not_advance_the_frame_epoch', () => {
  // No frame changed, so a squash already written against the current epoch is
  // still valid after a reanchor. Advancing here would reject it.
  const state = threeEntries()
  const before = blog.frameEpochOf(state)
  const reanchored = blog.applyReanchor(state)

  assert.equal(blog.frameEpochOf(reanchored), before)

  const squash = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: squashFrame(1) }, reanchored)
  assert.equal(squash.ok, true, squash.ok ? '' : squash.error)
})
test('WHAT[context-compression-002] HOST_006_prefix_coverage_rebuilds_after_a_reanchor_without_rewinding_ingest', () => {
  // Probe capability recovers on its own once a new complete-turn boundary is
  // crossed in the NEW Host numbering. RecordCoverage continues from where Y
  // already was — the next entry must advance the XTrace sequence, not restart.
  const reanchored = blog.applyReanchor(threeEntries())
  assert.equal(blog.coverage(reanchored).ingestedThroughSequence, 3)

  const rebuilt = commitEntry(reanchored, { from: 3, to: 4, cutoffFrom: 0, cutoffTo: 1, digest: 'new-d1' })
  assert.equal(rebuilt.ok, true, rebuilt.ok ? '' : rebuilt.error)
  assert.equal(blog.hasCoverage(rebuilt.value), true)

  // The pre-reanchor frames are still there, now joined by the new entry.
  assert.deepEqual(blog.frameKinds(rebuilt.value), ['Entry', 'Entry', 'Entry', 'Entry'])

  // And all four become coverable at once, including the three written under the
  // voided numbering. That is deliberate, not an oversight: those frames describe
  // work that really happened, and the cutoff is a claim about X's CURRENT prefix,
  // not about which turns B's text discusses. A FrozenRecordPrefix richer than the
  // cutoff is extra context; one poorer than it would be information loss.
  assert.deepEqual(blog.coverage(rebuilt.value), {
    ingestedThroughSequence: 4,
    cutoff: 1,
    digest: 'new-d1',
    coverableFrames: 4,
  })
})
test('WHAT[context-compression-002] HOST_006_reanchor_is_idempotent_on_the_frame_projection', () => {
  // The prefix projection makes a replay stale via its epoch check; here the
  // operation itself must be safe to apply twice, because the two projections
  // move under one fact.
  const once = blog.applyReanchor(threeEntries())
  const twice = blog.applyReanchor(once)

  assert.deepEqual(blog.coverage(twice), blog.coverage(once))
  assert.deepEqual(blog.frameKinds(twice), blog.frameKinds(once))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const policy = await import("../../../dist/Host/Contract/CompactionPolicySurface.js");

const settings = policy.requiredSettings()
const nextReanchor = (observed, reanchored) => {
  const handled = new Set(reanchored)
  return policy.nextReanchor(observed, (runId) => handled.has(runId))
}

test('WHAT[context-compression-002] HOST_006_prevention_layer_names_every_setting_that_must_be_off', () => {
  // The whole set is asserted, not each item's presence. A missing entry is a path by
  // which the Host can still rewrite context on its own, and dropping one turns no
  // test red unless the set itself is the assertion.
  assert.deepEqual(settings.map((setting) => setting.path), ['compaction.auto', 'compaction.prune', 'compaction.autocontinue'])

  for (const setting of settings) {
    assert.equal(setting.required, false, `${setting.path} must be required off`)
    assert.ok(setting.reason.length > 0, `${setting.path} must state why it matters`)
  }
})
test('WHAT[context-compression-002] COMPANION_009_prune_is_listed_and_says_why_containment_cannot_save_it', () => {
  // `prune` was not in the frozen clause; X0 found it while reading the Host source.
  // It bypasses the transform boundary and deletes persisted rows, and containment
  // cannot repair that: a deleted row is not a voided index, it is absent.
  const prune = settings.find((s) => s.path === 'compaction.prune')

  assert.equal(prune.clause, 'COMPANION-009')
  assert.match(prune.reason, /cannot be reanchored/)
})
test('WHAT[context-compression-002] HOST_006_autocontinue_is_answered_false_rather_than_left_to_the_default', () => {
  // `auto = false` already makes the replay branch unreachable, so this is belt and
  // braces. Answering explicitly matters because the hook is the only vetoable
  // synthetic-turn injection point; staying silent relies on an upstream default.
  assert.equal(policy.autoContinueEnabled(), false)
})
test('WHAT[context-compression-002] HOST_006_a_setting_that_cannot_be_written_fails_startup_with_its_reason', () => {
  const verdict = policy.judgeFirstTurnWithUnavailable('compaction.auto', 'ses_x', 0)

  assert.equal(verdict.kind, 'SettingUnavailable')
  assert.match(verdict.message, /HostContractUnsupported/)
  assert.match(verdict.message, /compaction\.auto/)

  // The reason travels with the message, because an operator reading
  // "compaction.auto could not be disabled" needs to know what breaks.
  assert.match(verdict.message, /overflow\.ts:28/)
})
test('WHAT[context-compression-002] HOST_006_startup_probe_passes_when_settings_are_off_and_the_first_turn_is_clean', () => {
  const verdict = policy.judgeFirstTurn('ses_x', 0)

  assert.equal(verdict.kind, 'Satisfied')
})
test('WHAT[context-compression-002] HOST_006_a_compaction_on_the_first_turn_means_a_second_implementation', () => {
  // A first turn is necessarily far below any threshold, so an automatic compaction
  // there cannot be legitimate.
  const verdict = policy.judgeFirstTurn('ses_probe', 1)

  assert.equal(verdict.kind, 'CompactedDespiteSettings')
  assert.match(verdict.message, /ses_probe/)
  assert.match(verdict.message, /first turn/)

  // Why refuse startup when containment exists: an unpreventable automatic compaction
  // grinds the mechanism into uselessness — reanchoring every few rounds means probe
  // coverage never accumulates, while everything looks normal from outside.
  assert.match(verdict.message, /no visible symptom/)
})
test('WHAT[context-compression-002] HOST_006_the_setting_check_takes_precedence_over_the_turn_observation', () => {
  // When both fail, report the unavailable setting: that is the root cause, and the
  // pseudo-run is its consequence. The other order sends an operator looking for a
  // second implementation that does not exist.
  const verdict = policy.judgeFirstTurnWithUnavailable('compaction.auto', 'ses_x', 3)

  assert.equal(verdict.kind, 'SettingUnavailable')
})
test('WHAT[context-compression-002] HOST_006_containment_keys_on_the_folded_predicate_not_raw_fields', () => {
  // The three raw fields (agent / mode / summary) are already folded into
  // `IsCompaction` at the snapshot boundary. Only the folded answer is accepted here:
  // re-deriving it would be a second definition of the observation the entire
  // containment layer keys on.
  assert.equal(policy.isContainableCompaction(true), true)
  assert.equal(policy.isContainableCompaction(false), false)
})
test('WHAT[context-compression-002] HOST_006_the_newest_unhandled_compaction_is_the_one_to_reanchor', () => {
  // The newest, because it is the one whose numbering the current transcript reflects.
  assert.equal(nextReanchor(['msg_c1', 'msg_c2', 'msg_c3'], []), 'msg_c3')
})
test('WHAT[context-compression-002] HOST_006_at_most_one_reanchor_is_emitted_per_observation', () => {
  const single = nextReanchor(['msg_c1', 'msg_c2', 'msg_c3', 'msg_c4'], [])

  assert.equal(typeof single, 'string')
  assert.equal(single, 'msg_c4')
})
test('WHAT[context-compression-002] HOST_006_an_already_reanchored_compaction_is_not_reanchored_again', () => {
  // Two observations of one pseudo-run must produce one retirement. This is the first
  // of two guards; the second is PrefixEpochProjection's epoch check (see
  // prefix-epoch.test.mjs).
  assert.equal(nextReanchor(['msg_c1'], ['msg_c1']), null)
  assert.equal(nextReanchor(['msg_c1', 'msg_c2'], ['msg_c2']), 'msg_c1')
  assert.equal(nextReanchor(['msg_c1', 'msg_c2'], ['msg_c1', 'msg_c2']), null)
})
test('WHAT[context-compression-002] HOST_006_no_observed_compaction_means_nothing_to_do', () => {
  assert.equal(nextReanchor([], []), null)
  assert.equal(nextReanchor([], ['msg_c1']), null)
})
test('WHAT[context-compression-002] HOST_006_a_new_compaction_after_a_handled_one_is_still_caught', () => {
  // A session that was already reanchored once, then genuinely compacted again. It has
  // to be recognised, or a second manual /compact would silently disable probes for
  // the rest of the session's life.
  assert.equal(nextReanchor(['msg_c1', 'msg_c2'], ['msg_c1']), 'msg_c2')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const selection = prefix
const agreeing = (digest) => () => digest
const committedAt = (cutoff, { digest = `prefix-${cutoff}`, frozen = `frozen-${cutoff}`, seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: frozen,
    cutoff,
    prefixDigest: digest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

test('WHAT[prefix-stability-002] PREFIX_STABILITY_prefix_behavior_is_exported_only_by_PrefixSurface', () => {
  for (const removed of ['select', 'snapshot', 'empty', 'prefixEmpty', 'prefixSnapshot', 'prefixProbe', 'applyRebase', 'retainTodoWriteRounds', 'requestKind', 'requestKindLabels', 'requestKindLabel', 'requestKindMayCarryProbe']) {
    assert.equal(typeof compression[removed], 'undefined', `${removed} must not remain on CompressionSurface`)
  }
  assert.equal(prefix.requestKind.mayCarryProbe(prefix.requestKind.workMain), true)
  assert.equal(prefix.requestKind.mayCarryProbe(prefix.requestKind.bloggerMain), false)
  assert.equal(prefix.requestKindLabel(prefix.requestKind.workMain), 'work-main')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const requestKind = prefix.requestKind
const budget = failureOwner.budget

test('WHAT[context-compression-002] retry dispatch reacts only to confirmed failure material', () => {
  // Without squash material the next request stays the ordinary main: squash
  // is never pre-selected before a confirmed failure produces material.
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
})
}
