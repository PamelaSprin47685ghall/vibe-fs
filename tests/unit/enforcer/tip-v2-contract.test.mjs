// tests/unit/Enforcer/tip-v2-contract.test.mjs — 16 tip-v2 contract items (ENFORCER-020..026, 030, 070/071, 170).
//
// Facade + resource contracts only (no Host). Items that need Host schema enum
// surface also live in manager-tool-contract (integration).

import assert from 'node:assert/strict'
import test from 'node:test'
import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import {
  blobDigest,
  blobRef,
  bloggerRequestId,
  blogProjection as blog,
  companionPrompt as prompt,
  companionProjection as proj,
  bloggerToml as toml,
  enforcementProjection as enf,
  envelope,
  fact,
  fold,
  frameEpochId,
  prefixEpochId,
  sessionId,
  stream,
  providerRun,
  enforcer,
} from '../support/domain.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const rulebookRoot = join(ROOT, 'resources/enforcer')
const bloggerSystemPath = join(ROOT, 'resources/prompts/blogger-system.md')

const packageTipNames = () =>
  readdirSync(rulebookRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort()

// ── 1. catalog exactly 120 valid rules ──────────────────────────────────────

test('ENFORCER_TIP_01_catalog_has_exactly_120_valid_rules', () => {
  assert.equal(enforcer.ruleCount, 120)
  assert.equal(packageTipNames().length, 120)
  assert.equal(enforcer.fieldNames().length, 120)
  assert.equal(new Set(enforcer.fieldNames()).size, 120)
})

// ── 2 / 16. tip.enum = directory TipName set; package = runtime ─────────────

test('ENFORCER_TIP_02_and_16_tip_enum_equals_catalog_fields_and_package', () => {
  const runtimeFields = enforcer.fieldNames()
  const packageFields = packageTipNames()

  assert.deepEqual(runtimeFields, packageFields)
  assert.equal(runtimeFields.length, 120)
  assert.equal(new Set(runtimeFields).size, 120)
})

// ── 3 / 4 schema-level: required tip; no 120 numerics (integration also) ────

test('ENFORCER_TIP_03_04_facade_surface_has_tip_not_numeric_scores', () => {
  // Unit facade cannot open Host zod; pin codec + catalog contract here.
  // Integration manager-tool-contract asserts Host schema required/optional.
  const sample = enforcer.decodeCall({ text: 'x', tip: enforcer.fieldNames()[0] })
  assert.equal(sample.ok, true)
  assert.equal(typeof sample.value.tip.ruleId, 'string')
  assert.equal(sample.value.tip.fieldName in Object.fromEntries(enforcer.fieldNames().map((f) => [f, true])), true)

  // No score vector path on decode surface.
  assert.equal(sample.value.Scores, undefined)
  assert.equal(enforcer.parseScore, undefined)
})

// ── 5 / 6 / 7 codec ─────────────────────────────────────────────────────────

test('ENFORCER_TIP_05_missing_tip_fails', () => {
  const r = enforcer.decodeCall({ text: 'entry' })
  assert.equal(r.ok, false)
  assert.equal(r.error, 'missing required argument: tip')
})

test('ENFORCER_TIP_06_unknown_tip_fails', () => {
  const r = enforcer.decodeCall({ text: 'entry', tip: 'totally-unknown-field' })
  assert.equal(r.ok, false)
  assert.match(r.error, /UnknownTip totally-unknown-field/)
})

test('ENFORCER_TIP_07_valid_field_maps_rule_id_exactly', () => {
  const field = 'primitive-obsession'
  const rule = enforcer.tryFindByField(field)
  assert.ok(rule)
  const r = enforcer.decodeCall({ text: 'entry', tip: field })
  assert.equal(r.ok, true)
  assert.equal(r.value.tip.ruleId, rule.ruleId)
  assert.equal(r.value.tip.fieldName, field)
})

// ── 8 / 9 / 10 / 11 RecentTips projection ───────────────────────────────────

const cycleRecord = (n, field) => {
  const rule = enforcer.tryFindByField(field) ?? enforcer.tryFindByField(enforcer.fieldNames()[n % 120])
  return enf.cycleRecord({
    mainSessionId: 'ses-main',
    bloggerSessionId: 'ses-blog',
    run: `msg_tip_${n}`,
    toolCallIds: [`call-${n}`],
    textRef: `blob-t${n}`,
    textDigest: `sha-t${n}`,
    tipRuleId: rule.ruleId,
    fieldNameAtCommit: rule.fieldName,
  })
}

test('ENFORCER_TIP_08_each_committed_cycle_records_exactly_one_tip', () => {
  let state = enf.empty
  const field = enforcer.fieldNames()[0]
  const applied = enf.applyFromEntry(state, cycleRecord(1, field))
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  state = applied.value
  const tips = enf.recentTips(state)
  assert.equal(tips.length, 1)
  assert.equal(tips[0].fieldName, field)
  assert.equal(typeof tips[0].ruleId, 'string')
  assert.equal(tips[0].cycleId, 'msg_tip_1')

  const record = enf.tryFindByProviderRun('msg_tip_1', state)
  assert.equal(enf.tipRuleIdOf(record), tips[0].ruleId)
  assert.equal(enf.fieldNameAtCommitOf(record), field)
})

test('ENFORCER_TIP_09_replay_preserves_tip', () => {
  const field = enforcer.fieldNames()[2]
  const first = enf.applyFromEntry(enf.empty, cycleRecord(1, field))
  assert.equal(first.ok, true)
  // Second apply same run → reject (ENFORCER-154); tip already stored.
  const dup = enf.applyFromEntry(first.value, cycleRecord(1, field))
  assert.equal(dup.ok, false)

  const tips = enf.recentTips(first.value)
  assert.deepEqual(tips, [
    {
      ruleId: enforcer.tryFindByField(field).ruleId,
      fieldName: field,
      cycleId: 'msg_tip_1',
    },
  ])
})

test('ENFORCER_TIP_10_recent_tips_cap_at_8', () => {
  let state = enf.empty
  const fields = enforcer.fieldNames()
  for (let n = 1; n <= 12; n++) {
    const applied = enf.applyFromEntry(state, cycleRecord(n, fields[n % fields.length]))
    assert.equal(applied.ok, true, `n=${n}: ${applied.ok ? '' : applied.error}`)
    state = applied.value
  }
  const tips = enf.recentTips(state)
  assert.equal(tips.length, 8)
  assert.equal(enf.RecentTipLimit, 8)
  // Last eight cycles: n=5..12
  assert.equal(tips[0].cycleId, 'msg_tip_5')
  assert.equal(tips[7].cycleId, 'msg_tip_12')
})

test('ENFORCER_TIP_11_recent_tips_order_oldest_to_newest', () => {
  let state = enf.empty
  const fields = enforcer.fieldNames()
  for (let n = 1; n <= 3; n++) {
    const applied = enf.applyFromEntry(state, cycleRecord(n, fields[n]))
    assert.equal(applied.ok, true)
    state = applied.value
  }
  const tips = enf.recentTips(state)
  assert.deepEqual(
    tips.map((t) => t.cycleId),
    ['msg_tip_1', 'msg_tip_2', 'msg_tip_3'],
  )
})

// ── 12. squash co-truncates RecentTips with covered frames ──────────────────

test('ENFORCER_TIP_12_squash_co_truncates_recent_tips', () => {
  // 1:1 assumption: each Entry appends one tip. Squash of oldest K frames drops
  // oldest min(K, tips) tips on the same main session (observation co-move).
  let state = enf.empty
  const fields = enforcer.fieldNames()
  for (let n = 1; n <= 2; n++) {
    const applied = enf.applyFromEntry(state, cycleRecord(n, fields[n - 1]))
    assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
    state = applied.value
  }
  assert.equal(enf.recentTips(state).length, 2)

  // Direct projection: squash 1 of 2 tips → keep newest.
  const afterOne = enf.applySquash(1, state)
  assert.deepEqual(
    enf.recentTips(afterOne).map((t) => t.cycleId),
    ['msg_tip_2'],
  )

  // Squash count ≥ tips length clears tips.
  assert.deepEqual(enf.recentTips(enf.applySquash(2, state)), [])
  assert.deepEqual(enf.recentTips(enf.applySquash(5, state)), [])
  // count ≤ 0 is a no-op.
  assert.deepEqual(enf.recentTips(enf.applySquash(0, state)).map((t) => t.cycleId), [
    'msg_tip_1',
    'msg_tip_2',
  ])

  // Fold causal chain: Entry → Entry → Squash(1) leaves one tip + one Squash frame.
  let seq = 0
  const session = sessionId('ses-main')
  const blogger = sessionId('ses-blog')
  const env = (factValue, run) =>
    envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

  const f0 = enforcer.fieldNames()[0]
  const f1 = enforcer.fieldNames()[1]
  const rule0 = enforcer.tryFindByField(f0)
  const rule1 = enforcer.tryFindByField(f1)

  // BlogProjection.applyEntry never advances FrameEpochId — both frames must
  // be written against epoch 0 (only BlogSquashCommitted advances it).
  const entry = (n, run, rule, field) =>
    env(
      fact('BlogEntryCommitted', {
        SessionId: session,
        BloggerSessionId: blogger,
        RequestId: bloggerRequestId(`req-e${n}`),
        FrameEpochId: frameEpochId(0),
        PreviousIngestedThroughSequence: BigInt(n - 1),
        NextIngestedThroughSequence: BigInt(n),
        PreviousCoverableTurnCutoffExclusive: n - 1,
        NextCoverableTurnCutoffExclusive: n,
        NextCoveredPrefixDigest: `d-${n}`,
        TextRef: blobRef(`blob-e${n}`),
        TextDigest: blobDigest(`sha-e${n}`),
        ProviderRun: providerRun(run),
        ToolCallIds: [],
        TipRuleId: rule.ruleId,
        FieldNameAtCommit: field,
        EvidenceRef: undefined,
        ObservedPrefixEpochId: prefixEpochId(0),
      }),
      run,
    )

  const squash = env(
    fact('BlogSquashCommitted', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId('req-s1'),
      PreviousFrameEpochId: frameEpochId(0),
      NextFrameEpochId: frameEpochId(1),
      CoveredFrameCount: 1,
      TextRef: blobRef('blob-s1'),
      TextDigest: blobDigest('sha-s1'),
      ProviderRun: providerRun('msg_s1'),
    }),
    'msg_s1',
  )

  const folded = fold.replay([entry(1, 'msg_tip_1', rule0, f0), entry(2, 'msg_tip_2', rule1, f1), squash])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const s = fold.session(folded.value, 'ses-main')
  assert.ok(s.Enforcement, 'enforcement projection exists after fold')

  const tips = enf.recentTips(s.Enforcement)
  assert.equal(tips.length, 1)
  assert.deepEqual(
    tips.map((t) => t.cycleId),
    ['msg_tip_2'],
  )
  assert.equal(tips[0].fieldName, f1)
  // Squash replaces oldest K frames in-place with one Squash frame at the front.
  assert.deepEqual(blog.frameKinds(s.Blog), ['Squash', 'Entry'])
})

// ── 13. work record previous_enforcer_tip blocks (paired with frames) ───────

test('ENFORCER_TIP_13_work_record_contains_previous_enforcer_tip_blocks', () => {
  const block = toml.renderPreviousEnforcerTip('primitive-obsession', 'msg_c1')
  assert.match(block, /\[\[do_not_exec\]\]/)
  assert.match(block, /kind = "previous_enforcer_tip"/)
  assert.match(block, /tip = "primitive-obsession"/)
  assert.match(block, /cycle = "msg_c1"/)

  const plan = proj.build((s) => `«${s}»`, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: [
      { digest: 'sha-f0', body: 'frame body 0' },
      { digest: 'sha-f1', body: 'frame body 1' },
    ],
    delta: { messageId: 'msg_delta', toml: '[[new_work_to_record]]\nuser = "work"' },
    previousTips: [
      { field: 'primitive-obsession', cycleId: 'msg_c1' },
      { field: 'ignored-tdd', cycleId: 'msg_c2' },
    ],
  })

  const tipTexts = plan.texts.filter((t) => t.includes('previous_enforcer_tip'))
  assert.equal(tipTexts.length, 2)
  assert.match(tipTexts[0], /tip = "primitive-obsession"/)
  assert.match(tipTexts[1], /tip = "ignored-tdd"/)
  // Paired observation units: tip₀, frame₀, tip₁, frame₁, delta (not tips∥frames).
  assert.equal(plan.roles.length, 5)
  assert.match(plan.texts[0], /previous_enforcer_tip/)
  assert.equal(plan.texts[1].includes('historic_frame'), true)
  assert.match(plan.texts[2], /previous_enforcer_tip/)
  assert.equal(plan.texts[3].includes('historic_frame'), true)
  assert.equal(plan.messages.at(-1).physical, true)
})

// ── 14. prompt anti-repeat + severe exception ───────────────────────────────

test('ENFORCER_TIP_14_prompt_has_anti_repeat_and_severe_exception', () => {
  const system = readFileSync(bloggerSystemPath, 'utf8')
  assert.match(system, /exactly one tip|exactly once/)
  assert.match(system, /previous_enforcer_tip/)
  assert.match(system, /diversity|密集|prefer diversity|should not be re-selected/i)
  assert.match(system, /severe|blocking|阻断|严重/i)
  assert.doesNotMatch(system, /omit all scores|omit zero-valued scores/i)

  assert.match(prompt.normalInstruction, /required tip|catalog field/)
  assert.match(prompt.squashInstruction, /required tip|catalog field/)
  assert.doesNotMatch(prompt.squashInstruction, /omit all scores/)
  assert.doesNotMatch(prompt.normalInstruction, /omit.*scores/i)
})

// ── 15. multi-call canonical tip by PartOrdinal ─────────────────────────────

test('ENFORCER_TIP_15_multi_call_canonical_tip_is_first_by_part_ordinal', () => {
  const a = enforcer.fieldNames()[0]
  const b = enforcer.fieldNames()[1]
  const merged = enforcer.mergeCalls([
    [2, { text: 'third', tipField: b }],
    [0, { text: 'first', tipField: a }],
    [1, { text: 'second', tipField: b }],
  ])
  assert.equal(merged.tip.fieldName, a)
  assert.equal(merged.mergedText, 'first\n\nsecond\n\nthird')
  assert.equal(merged.multiCall, true)
})
