// tests-mjs/Journal/envelope.test.mjs — PERSIST-001/002/005/008.
//
// The NDJSON line and the fold that reads it back. Everything here is pure: no
// file is touched, so a failure names the codec or the fold and nothing else.
// The on-disk behaviour those two enable lives in `boot.test.mjs`.
//
// Why full-text assertions rather than field probes: mjs has no compile-time
// rename protection, so `env.LocalSeq` becoming `env.Seq` would read `undefined`
// and compare equal to `undefined`. Comparing the whole serialized line makes a
// renamed field a diff instead of a silent pass.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  envelope,
  fact,
  fold,
  idValue,
  journal,
  kWayMerge,
  mapCount,
  offsetAt,
  offsetMinutesOf,
  payloadOf,
  sessionId,
  stream,
} from '../domain.mjs'

const SESSION = sessionId('ses_a')
const CLOSED = fact('CompanionBloggerClosed', { SessionId: SESSION })

const env = (overrides = {}) => envelope({ stream: stream.session(SESSION), fact: CLOSED, ...overrides })

/** Envelope identity as plain text, so a field rename cannot read `undefined`. */
const readEnvelope = (value) => ({
  runtime: idValue.runtime(value.RuntimeId),
  seq: Number(idValue.localSeq(value.LocalSeq)),
  event: idValue.event(value.EventId),
  stream: caseOf(value.Stream),
  fact: caseOf(payloadOf(value.Fact)),
})

// ── PERSIST-001: the envelope is one self-contained line ─────────────────────

test('PERSIST_001_an_envelope_serializes_to_exactly_one_line', () => {
  const line = journal.serialize(env({ seq: 7 }))

  // NDJSON's whole contract. A line containing a newline would make append touch
  // more than the tail and make recovery's line split invent two envelopes.
  assert.equal(line.includes('\n'), false)
  assert.equal(line.includes('\r'), false)

  // Every field a fold needs is inline: no envelope references another line.
  assert.deepEqual(Object.keys(JSON.parse(line)).sort(), [
    'EventId',
    'Fact',
    'LocalSeq',
    'ObservedAt',
    'RuntimeId',
    'Stream',
  ])
})

test('PERSIST_001_serialization_is_deterministic_for_one_envelope', () => {
  // Two folds of one journal must produce one projection, so the bytes cannot
  // depend on iteration order or a clock read at serialize time.
  const value = env({ seq: 3, observedAt: '2026-02-03T04:05:06Z' })
  assert.equal(journal.serialize(value), journal.serialize(value))
  assert.equal(journal.serialize(value), journal.serialize(env({ seq: 3, observedAt: '2026-02-03T04:05:06Z' })))
})

test('PERSIST_001_an_absent_provider_run_is_omitted_rather_than_written_null', () => {
  // HOST-010: facts belonging to no provider run pass None. A `"ProviderRun":null`
  // would make "observed during no run" and "observed during an unnamed run" the
  // same line.
  const withoutRun = journal.serialize(env({ seq: 1 }))
  assert.equal(withoutRun.includes('ProviderRun'), false)

  const withRun = journal.serialize(env({ seq: 1, run: 'run_9' }))
  assert.equal(withRun.includes('"ProviderRun":["ProviderRunIdentity","run_9"]'), true)
})

test('PERSIST_001_an_envelope_survives_a_round_trip_unchanged', () => {
  const original = env({ seq: 4, observedAt: '2026-03-04T05:06:07Z', run: 'run_1' })
  const line = journal.serialize(original)
  const decoded = journal.deserialize(line)

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(readEnvelope(decoded.value), readEnvelope(original))

  // Re-serializing must reproduce the same bytes, or replay would drift from the
  // file it replayed. This is the assertion that caught `serialize` rendering the
  // READER's local offset: the decoder attaches it, so on a `TZ=Asia/Shanghai`
  // host the round trip previously produced `+08:00` for the same instant.
  assert.equal(journal.serialize(decoded.value), line)
})

test('PERSIST_001_serialized_bytes_do_not_depend_on_the_writers_utc_offset', () => {
  // One instant, three offsets. The line is the durable artifact, so two hosts in
  // different timezones must produce identical bytes for one fact — otherwise a
  // byte comparison of two replicas reports a difference that is not one.
  const instant = '2026-03-04T05:06:07Z'
  const atUtc = env({ seq: 1, observedAt: instant })
  const shifted = (minutes) => ({ ...atUtc, ObservedAt: offsetAt(instant, minutes) })

  const shanghai = shifted(480)
  const newYork = shifted(-300)

  // The inputs really are distinct values, or this test would prove nothing.
  assert.deepEqual([offsetMinutesOf(atUtc.ObservedAt), offsetMinutesOf(shanghai.ObservedAt), offsetMinutesOf(newYork.ObservedAt)], [0, 480, -300])
  assert.equal(shanghai.ObservedAt.getTime(), atUtc.ObservedAt.getTime(), 'the three must be the same instant')

  const line = journal.serialize(atUtc)
  assert.equal(journal.serialize(shanghai), line)
  assert.equal(journal.serialize(newYork), line)

  // Pinned to offset zero, not merely consistent: a shared local offset would
  // also be self-consistent while still being unreadable elsewhere.
  assert.equal(JSON.parse(line).ObservedAt, '2026-03-04T05:06:07.000+00:00')
})

test('PERSIST_001_ordering_is_by_local_seq_inside_a_runtime_and_by_time_across', () => {
  const a1 = env({ runtime: 'rt_a', seq: 1, observedAt: '2026-01-01T00:00:09Z' })
  const a2 = env({ runtime: 'rt_a', seq: 2, observedAt: '2026-01-01T00:00:00Z' })

  // Inside one runtime LocalSeq wins outright — a clock that went backwards must
  // not reorder facts the writer appended in order.
  assert.equal(journal.compareSortKey(a1, a2) < 0, true)
  assert.equal(journal.compareSortKey(a2, a1) > 0, true)
  assert.equal(journal.compareSortKey(a1, a1), 0)

  // Across runtimes, observation time decides.
  const b1 = env({ runtime: 'rt_b', seq: 1, observedAt: '2026-01-01T00:00:05Z' })
  assert.equal(journal.compareSortKey(a1, b1) > 0, true)
  assert.equal(journal.compareSortKey(b1, a1) < 0, true)
})

test('PERSIST_001_same_instant_across_runtimes_breaks_the_tie_by_runtime_id', () => {
  // Two runtimes can observe in the same millisecond. Without a total order the
  // fold would depend on which file the reader opened first, so two restarts
  // could disagree about the same history.
  const at = '2026-01-01T00:00:00Z'
  const a = env({ runtime: 'rt_a', seq: 1, observedAt: at })
  const b = env({ runtime: 'rt_b', seq: 1, observedAt: at })

  assert.equal(journal.compareSortKey(a, b) < 0, true)
  assert.equal(journal.compareSortKey(b, a) > 0, true)
})

test('PERSIST_001_k_way_merge_is_a_total_order_regardless_of_input_order', () => {
  const at = (s) => `2026-01-01T00:00:0${s}Z`
  const streamA = [
    env({ runtime: 'rt_a', seq: 1, observedAt: at(1) }),
    env({ runtime: 'rt_a', seq: 2, observedAt: at(4) }),
  ]
  const streamB = [
    env({ runtime: 'rt_b', seq: 1, observedAt: at(2) }),
    env({ runtime: 'rt_b', seq: 2, observedAt: at(3) }),
  ]

  const label = (merged) => merged.map((e) => `${idValue.runtime(e.RuntimeId)}#${idValue.localSeq(e.LocalSeq)}`)
  const expected = ['rt_a#1', 'rt_b#1', 'rt_b#2', 'rt_a#2']

  assert.deepEqual(label(kWayMerge([streamA, streamB])), expected)
  assert.deepEqual(label(kWayMerge([streamB, streamA])), expected, 'merge must not depend on stream argument order')
  assert.deepEqual(label(kWayMerge([[], streamA, [], streamB])), expected, 'empty streams must not shift the order')
})

// ── PERSIST-005: a pre-0.5.0 journal is refused, never guessed ───────────────

test('PERSIST_005_legacy_fallback_counters_and_model_ids_are_fatal', () => {
  // Two groups, both fatal. The old Fallback projection stored counters and model
  // ids; deriving a modulo-4 cursor from them would be inventing history, and
  // VERIFY-006 lists a journal carrying model ids as a No-Go.
  const markers = [
    'FailuresOnCurrentSide',
    'IsDead',
    'TotalFailures',
    'BaseModelID',
    'BaseProviderID',
    'EffectiveModelID',
    'EffectiveProviderID',
  ]

  for (const marker of markers) {
    const line = JSON.stringify({ LocalSeq: 1, [marker]: 3 })
    assert.equal(journal.containsLegacyFallbackFields(line), true, `${marker} must be recognised as pre-0.5.0`)

    const decoded = journal.deserialize(line)
    assert.equal(decoded.ok, false)
    assert.equal(decoded.error, journal.pre050MigrationMessage)
  }
})

test('PERSIST_005_replaced_fact_names_produce_the_migration_message_not_a_codec_error', () => {
  // Without these names the decoder would fail with an opaque union error, so the
  // operator would read "cannot parse line 3" instead of "this journal predates
  // 0.5.0" — the difference between archiving the file and debugging the codec.
  const retired = [
    'PluginPromptAccepted',
    'HumanPromptAccepted',
    'GuardPromptAccepted',
    'InteractionRepairClaimed',
    'ReviewConfirmedIdle',
    'AgentLinked',
    'AgentForked',
    'AgentUnlinked',
    'OrchestratorCandidateRegistered',
    'OrchestratorRebased',
    'OrchestratorPublishClaimed',
    'DurableEffectRequested',
    'DurableEffectAccepted',
  ]

  for (const name of retired) {
    const line = JSON.stringify({ Fact: ['Agent', [name, {}]] })
    assert.equal(journal.deserialize(line).error, journal.pre050MigrationMessage, `${name} must be diagnosed by name`)
  }
})

test('PERSIST_005_the_migration_message_tells_the_operator_what_to_do', () => {
  // A refusal that does not say what to do gets worked around. This one names the
  // action, so archiving the file is the obvious response.
  assert.equal(
    journal.pre050MigrationMessage,
    'Wanxiangshu 0.5.0 does not support pre-0.5.0 runtime journals.\n' +
      'Archive or remove the old Wanxiangshu runtime journal before starting.',
  )
})

test('PERSIST_005_a_current_fact_is_not_mistaken_for_a_legacy_one', () => {
  // The markers are substring matches, so a current name that contains a retired
  // one would be a false positive. `HandleLinked` replaced `AgentLinked` and must
  // stay readable.
  const line = journal.serialize(env({ seq: 1 }))
  assert.equal(journal.containsLegacyFallbackFields(line), false)
  assert.equal(journal.deserialize(line).ok, true)

  for (const current of ['HandleLinked', 'HandleCompleted', 'HandleRetired', 'CandidateReady', 'PublishClaimed']) {
    assert.equal(
      journal.containsLegacyFallbackFields(JSON.stringify({ Fact: ['Agent', [current, {}]] })),
      false,
      `${current} is a current fact and must not trip the pre-0.5.0 check`,
    )
  }
})

test('PERSIST_005_malformed_json_is_an_error_value_not_an_exception', () => {
  // The caller is a boot loop that must report and stop, so a throw here would
  // turn "refuse to start" into a stack trace.
  for (const bad of ['', '{', '{"unclosed": ', 'null', '[]', 'not json at all']) {
    const decoded = journal.deserialize(bad)
    assert.equal(decoded.ok, false, `${JSON.stringify(bad)} must not decode`)
    assert.equal(typeof decoded.error, 'string')
  }
})

// ── PERSIST-002: two append outcomes, and replay agrees with them ────────────

test('PERSIST_002_a_committed_envelope_replays_into_the_same_projection', () => {
  // "No partial write" is only meaningful if the written line folds to what the
  // in-memory projection already holds. Replay goes through the NDJSON text, so
  // this covers codec and fold together.
  const envelopes = [env({ seq: 1 }), env({ seq: 2, run: 'run_1' }), env({ seq: 3 })]

  const direct = fold.apply(fold.empty, envelopes)
  const replayed = fold.replay(envelopes)

  assert.equal(direct.ok, true, direct.ok ? '' : JSON.stringify(direct.error))
  assert.equal(replayed.ok, true, replayed.ok ? '' : JSON.stringify(replayed.error))
  assert.deepEqual(Object.keys(fold.sessions(replayed.value)), ['ses_a'])
  assert.deepEqual(fold.sessions(replayed.value), fold.sessions(direct.value))
})

// ── PERSIST-008: projections are O(1) integrations, not history scans ────────

test('PERSIST_008_one_session_projection_is_reached_by_a_keyed_lookup', () => {
  const projection = fold.apply(fold.empty, [env({ seq: 1 })])
  assert.equal(projection.ok, true, projection.ok ? '' : JSON.stringify(projection.error))

  // Every bounded projection hangs off the session key. A missing one would force
  // a caller to scan, which is the shape the clause forbids.
  assert.deepEqual(Object.keys(fold.session(projection.value, 'ses_a')).sort(), [
    'Blog',
    'BloggerCycles',
    'Companion',
    'Enforcement',
    'Fallback',
    'Handles',
    'PrefixEpoch',
    'PromptAuthority',
    'ReviewGuard',
    'ReviewRequirements',
    'XTrace',
  ])

  // An unknown session is absent, not an empty scan result.
  assert.equal(fold.session(projection.value, 'ses_missing'), undefined)
})

test('PERSIST_008_projection_size_tracks_distinct_sessions_not_history_length', () => {
  // The property the clause actually wants: folding 300 facts for one session
  // leaves one entry. A projection that grew per fact would be a history scan
  // waiting to happen.
  const many = Array.from({ length: 300 }, (_, index) => env({ seq: index + 1 }))
  const projection = fold.apply(fold.empty, many)

  assert.equal(projection.ok, true, projection.ok ? '' : JSON.stringify(projection.error))
  assert.equal(mapCount(projection.value.AgentProjections.Sessions), 1)

  const other = sessionId('ses_b')
  const twoSessions = fold.apply(fold.empty, [
    ...many,
    envelope({ seq: 301, stream: stream.session(other), fact: fact('CompanionBloggerClosed', { SessionId: other }) }),
  ])

  assert.equal(twoSessions.ok, true, twoSessions.ok ? '' : JSON.stringify(twoSessions.error))
  assert.deepEqual(Object.keys(fold.sessions(twoSessions.value)).sort(), ['ses_a', 'ses_b'])
})

test('PERSIST_008_folding_is_incremental_so_one_envelope_needs_no_replay', () => {
  // `foldEnvelope` on an existing projection must equal folding the whole
  // sequence. If it did not, the runtime would have to re-read history on every
  // append to stay correct.
  const first = fold.apply(fold.empty, [env({ seq: 1 })])
  assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))

  const incremental = fold.one(first.value, env({ seq: 2 }))
  const wholesale = fold.apply(fold.empty, [env({ seq: 1 }), env({ seq: 2 })])

  assert.equal(incremental.ok, true, incremental.ok ? '' : JSON.stringify(incremental.error))
  assert.equal(wholesale.ok, true, wholesale.ok ? '' : JSON.stringify(wholesale.error))
  assert.deepEqual(fold.sessions(incremental.value), fold.sessions(wholesale.value))
})
