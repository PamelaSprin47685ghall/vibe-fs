// tests/unit/Context/host-compaction-policy.test.mjs — HOST-006, both layers.
//
// The prevention layer requires settings off; the containment layer reanchors any
// compaction that appears regardless.
//
// Two judgements here are the ones that go wrong quietly:
//
//   The startup probe must NOT assert "no pseudo-run ever". That would judge a user's
//   legitimate /compact as a Host contract violation. It asserts the far narrower
//   claim that the first turn of the first managed session is clean — a turn
//   necessarily far below any threshold, so a compaction there cannot be legitimate.
//
//   nextReanchor returns at most one run. A reanchor retires the prefix and zeroes
//   coverage; doing it twice changes nothing the second time, so emitting several
//   would produce facts whose only effect is to advance the epoch counter.

import assert from 'node:assert/strict'
import test from 'node:test'
import { hostCompaction as policy } from '../domain.mjs'

// ── prevention: the settings that must be off ──────────────────────────────

test('HOST_006_prevention_layer_names_every_setting_that_must_be_off', () => {
  // The whole set is asserted, not each item's presence. A missing entry is a path by
  // which the Host can still rewrite context on its own, and dropping one turns no
  // test red unless the set itself is the assertion.
  assert.deepEqual(policy.settingPaths, ['compaction.auto', 'compaction.prune', 'compaction.autocontinue'])

  for (const setting of policy.settings) {
    assert.equal(setting.required, false, `${setting.path} must be required off`)
    assert.ok(setting.reason.length > 0, `${setting.path} must state why it matters`)
  }
})

test('COMPANION_009_prune_is_listed_and_says_why_containment_cannot_save_it', () => {
  // `prune` was not in the frozen clause; X0 found it while reading the Host source.
  // It bypasses the transform boundary and deletes persisted rows, and containment
  // cannot repair that: a deleted row is not a voided index, it is absent.
  const prune = policy.settings.find((s) => s.path === 'compaction.prune')

  assert.equal(prune.clause, 'COMPANION-009')
  assert.match(prune.reason, /cannot be reanchored/)
})

test('HOST_006_autocontinue_is_answered_false_rather_than_left_to_the_default', () => {
  // `auto = false` already makes the replay branch unreachable, so this is belt and
  // braces. Answering explicitly matters because the hook is the only vetoable
  // synthetic-turn injection point; staying silent relies on an upstream default.
  assert.equal(policy.autoContinueEnabled, false)
})

test('HOST_006_a_setting_that_cannot_be_written_fails_startup_with_its_reason', () => {
  const verdict = policy.judgeFirstTurn({ unavailable: 'compaction.auto', session: 'ses_x', pseudoRuns: 0 })

  assert.equal(verdict.name, 'SettingUnavailable')
  assert.match(verdict.message, /HostContractUnsupported/)
  assert.match(verdict.message, /compaction\.auto/)

  // The reason travels with the message, because an operator reading
  // "compaction.auto could not be disabled" needs to know what breaks.
  assert.match(verdict.message, /overflow\.ts:28/)
})

// ── the startup probe's boundary ───────────────────────────────────────────

test('HOST_006_startup_probe_passes_when_settings_are_off_and_the_first_turn_is_clean', () => {
  const verdict = policy.judgeFirstTurn({ unavailable: undefined, session: 'ses_x', pseudoRuns: 0 })

  assert.equal(verdict.name, 'Satisfied')
})

test('HOST_006_a_compaction_on_the_first_turn_means_a_second_implementation', () => {
  // A first turn is necessarily far below any threshold, so an automatic compaction
  // there cannot be legitimate.
  const verdict = policy.judgeFirstTurn({ unavailable: undefined, session: 'ses_probe', pseudoRuns: 1 })

  assert.equal(verdict.name, 'CompactedDespiteSettings')
  assert.match(verdict.message, /ses_probe/)
  assert.match(verdict.message, /first turn/)

  // Why refuse startup when containment exists: an unpreventable automatic compaction
  // grinds the mechanism into uselessness — reanchoring every few rounds means probe
  // coverage never accumulates, while everything looks normal from outside.
  assert.match(verdict.message, /no visible symptom/)
})

test('HOST_006_the_setting_check_takes_precedence_over_the_turn_observation', () => {
  // When both fail, report the unavailable setting: that is the root cause, and the
  // pseudo-run is its consequence. The other order sends an operator looking for a
  // second implementation that does not exist.
  const verdict = policy.judgeFirstTurn({ unavailable: 'compaction.auto', session: 'ses_x', pseudoRuns: 3 })

  assert.equal(verdict.name, 'SettingUnavailable')
})

// ── containment: recognition and deduplication ─────────────────────────────

test('HOST_006_containment_keys_on_the_folded_predicate_not_raw_fields', () => {
  // The three raw fields (agent / mode / summary) are already folded into
  // `IsCompaction` at the snapshot boundary. Only the folded answer is accepted here:
  // re-deriving it would be a second definition of the observation the entire
  // containment layer keys on.
  assert.equal(policy.isContainableCompaction(true), true)
  assert.equal(policy.isContainableCompaction(false), false)
})

test('CTX_005_containment_does_not_discriminate_by_source', () => {
  // A user's /compact and an unexpected Host compaction get identical handling, so
  // there is no "which kind" parameter and no branch for it. This asserts the shape of
  // the signature: a single-argument predicate with no source input.
  assert.equal(policy.isContainableCompaction.length, 1)
})

test('HOST_006_the_newest_unhandled_compaction_is_the_one_to_reanchor', () => {
  // The newest, because it is the one whose numbering the current transcript reflects.
  assert.equal(policy.nextReanchor(['msg_c1', 'msg_c2', 'msg_c3']), 'msg_c3')
})

test('HOST_006_at_most_one_reanchor_is_emitted_per_observation', () => {
  const single = policy.nextReanchor(['msg_c1', 'msg_c2', 'msg_c3', 'msg_c4'])

  assert.equal(typeof single, 'string')
  assert.equal(single, 'msg_c4')
})

test('HOST_006_an_already_reanchored_compaction_is_not_reanchored_again', () => {
  // Two observations of one pseudo-run must produce one retirement. This is the first
  // of two guards; the second is PrefixEpochProjection's epoch check (see
  // prefix-epoch.test.mjs).
  assert.equal(policy.nextReanchor(['msg_c1'], ['msg_c1']), undefined)
  assert.equal(policy.nextReanchor(['msg_c1', 'msg_c2'], ['msg_c2']), 'msg_c1')
  assert.equal(policy.nextReanchor(['msg_c1', 'msg_c2'], ['msg_c1', 'msg_c2']), undefined)
})

test('HOST_006_no_observed_compaction_means_nothing_to_do', () => {
  assert.equal(policy.nextReanchor([]), undefined)
  assert.equal(policy.nextReanchor([], ['msg_c1']), undefined)
})

test('HOST_006_a_new_compaction_after_a_handled_one_is_still_caught', () => {
  // A session that was already reanchored once, then genuinely compacted again. It has
  // to be recognised, or a second manual /compact would silently disable probes for
  // the rest of the session's life.
  assert.equal(policy.nextReanchor(['msg_c1', 'msg_c2'], ['msg_c1']), 'msg_c2')
})
