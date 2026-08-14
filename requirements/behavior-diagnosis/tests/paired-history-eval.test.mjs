// Moved from tests/unit/enforcer/paired-history-eval.test.mjs (cutover Wave 2a); owner: behavior-diagnosis.
//
// A42 Historical Pair Eval — automated harness (rulebook.md §A42).
//
// Scenario: Observation A (work log + tip X), Observation B (work log + tip Y),
// then new material similar to A. The Blogger/enforcer selection path must be
// able to see tip X from A and therefore *could* judge a true repeat.
//
// This file does NOT fake a 120/120 human semantic review (A37–A50 Remaining).
// What it proves: the eval can load the packaged 120-tip catalog, attach real
// historical tip identities onto journalled observations, and surface tip X
// beside A's work log when similar new material is offered.
// Still human: whether the new material *is* a true repeat of A (Protocol C
// in Blogger Role Law is prompt-side; there is no runtime TrueRepeat oracle).

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  blobDigest,
  blobRef,
  bloggerRequestId,
  companionProjection as proj,
  companionPrompt as prompt,
  enforcer,
  enforcerCatalogResource,
  envelope,
  enforcementProjection as enf,
  fact,
  fold,
  frameEpochId,
  listItems,
  prefixEpochId,
  promptResources,
  providerRun,
  sessionId,
  stream,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'
import { observationsOfSession } from '../../../dist/Enforcer/Observation.js'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const RULEBOOK = join(ROOT, 'resources/enforcer')
const FIXTURES = join(dirname(fileURLToPath(import.meta.url)), 'fixtures')

const TIP_X = 'primitive-obsession'
const TIP_Y = 'ignored-tdd'

const WORK_LOG_A = readFileSync(join(FIXTURES, 'observation-a-work-log.txt'), 'utf8').trim()
const WORK_LOG_B = readFileSync(join(FIXTURES, 'observation-b-work-log.txt'), 'utf8').trim()

/** New material similar to A: same primitive-identity collapse, different paths. */
const NEW_MATERIAL_SIMILAR_TO_A = `[[new_work_to_record]]
assistant = "Coder 修改 /src/tenancy/bind.ts：userId 与 tenantId 均声明为 string，绑定函数可互换两个标识。"
`

const MAIN = 'ses-paired-main'
const BLOGGER = 'ses-paired-blogger'
const session = sessionId(MAIN)
const blogger = sessionId(BLOGGER)

const packageTipNames = () =>
  readdirSync(RULEBOOK, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort()

const requireRealTip = (name) => {
  assert.equal(existsSync(join(RULEBOOK, name, 'enforcer.md')), true, `missing ${name}/enforcer.md`)
  assert.equal(existsSync(join(RULEBOOK, name, 'main.md')), true, `missing ${name}/main.md`)
  const rule = enforcer.tryFindByField(name)
  assert.ok(rule, `catalog missing real tip ${name}`)
  assert.equal(rule.ruleId, name)
  assert.equal(rule.fieldName, name)
  return rule
}

let seq = 0
const commitObservation = ({ tip, n, digest, run }) =>
  envelope({
    seq: (seq += 1),
    stream: stream.session(session),
    run,
    fact: fact('BlogObservationCommitted', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId(`req-paired-${n}`),
      FrameEpochId: frameEpochId(0),
      PreviousIngestedThroughSequence: BigInt(n - 1),
      NextIngestedThroughSequence: BigInt(n),
      PreviousCoverableTurnCutoffExclusive: n - 1,
      NextCoverableTurnCutoffExclusive: n,
      NextCoveredPrefixDigest: `digest-paired-${n}`,
      TextRef: blobRef(`blob-paired-${n}`),
      TextDigest: blobDigest(digest),
      ProviderRun: providerRun(run),
      ToolCallIds: [toolCallId(`call-paired-${n}`)],
      TipRuleId: tip,
      FieldNameAtCommit: tip,
      EvidenceRef: undefined,
      ObservedPrefixEpochId: prefixEpochId(0),
    }),
  })

const foldPairedHistory = () => {
  seq = 0
  const result = fold.replay([
    commitObservation({ tip: TIP_X, n: 1, digest: 'sha-obs-a', run: 'run-obs-a' }),
    commitObservation({ tip: TIP_Y, n: 2, digest: 'sha-obs-b', run: 'run-obs-b' }),
  ])
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  const projected = fold.session(result.value, MAIN)
  assert.ok(projected, 'folded session missing')
  return projected
}

const readObservations = (projected) =>
  listItems(observationsOfSession(projected)).map((o) => ({
    tipName: o.TipName,
    cycleId: o.CycleId,
    frameDigest: o.FrameDigest,
  }))

const isPreviousTip = (text) => text.includes('previous_enforcer_tip')
const isHistoricFrame = (text) => text.includes('historic_frame')

test('A42_PAIRED_HISTORY_001_eval_loads_120_tip_catalog_from_real_directories', () => {
  const dirs = packageTipNames()
  assert.equal(dirs.length, 120)
  assert.equal(enforcer.ruleCount, 120)
  assert.deepEqual(enforcer.fieldNames(), dirs)

  requireRealTip(TIP_X)
  requireRealTip(TIP_Y)
  assert.notEqual(TIP_X, TIP_Y)

  const prompts = promptResources.load()
  const base = prompts.BloggerSystemPrompt
  assert.ok(typeof base === 'string' && base.length > 0, 'Blogger system prompt must load from PromptResources')
  const composed = enforcerCatalogResource.composeBloggerSystemPrompt(base, enforcer.rules)
  assert.match(composed, new RegExp(TIP_X))
  assert.match(composed, new RegExp(TIP_Y))
  // Free-form RuleBook prose has no prompt-shape contract. Historical tip
  // delivery is proved below through the typed observation/projection path.
})

test('A42_PAIRED_HISTORY_002_observations_a_and_b_carry_real_historical_tip_ids', () => {
  requireRealTip(TIP_X)
  requireRealTip(TIP_Y)

  const projected = foldPairedHistory()
  const observations = readObservations(projected)
  assert.deepEqual(observations, [
    { tipName: TIP_X, cycleId: 'run-obs-a', frameDigest: 'sha-obs-a' },
    { tipName: TIP_Y, cycleId: 'run-obs-b', frameDigest: 'sha-obs-b' },
  ])

  const tips = enf.recentTips(projected.Enforcement)
  assert.deepEqual(
    tips.map((t) => ({ field: t.fieldName, ruleId: t.ruleId, cycleId: t.cycleId })),
    [
      { field: TIP_X, ruleId: TIP_X, cycleId: 'run-obs-a' },
      { field: TIP_Y, ruleId: TIP_Y, cycleId: 'run-obs-b' },
    ],
  )

  const decodedX = enforcer.decodeCall({ text: WORK_LOG_A, tip: TIP_X })
  assert.equal(decodedX.ok, true)
  assert.equal(decodedX.value.tip.fieldName, TIP_X)
  const decodedY = enforcer.decodeCall({ text: WORK_LOG_B, tip: TIP_Y })
  assert.equal(decodedY.ok, true)
  assert.equal(decodedY.value.tip.fieldName, TIP_Y)
})

test('A42_PAIRED_HISTORY_003_selection_path_sees_tip_x_when_new_material_resembles_a', () => {
  const projected = foldPairedHistory()
  const observations = readObservations(projected)
  assert.equal(observations[0].tipName, TIP_X)

  const previousTips = enf.recentTips(projected.Enforcement).map((t) => ({
    field: t.fieldName,
    cycleId: t.cycleId,
  }))
  assert.equal(previousTips[0].field, TIP_X)

  const bodiesByDigest = {
    'sha-obs-a': WORK_LOG_A,
    'sha-obs-b': WORK_LOG_B,
  }
  const frames = observations.map((o) => ({
    digest: o.frameDigest,
    body: bodiesByDigest[o.frameDigest],
  }))
  assert.ok(frames[0].body.includes('accountId'))
  assert.ok(NEW_MATERIAL_SIMILAR_TO_A.includes('userId'))
  assert.ok(NEW_MATERIAL_SIMILAR_TO_A.includes('string'))

  const plan = proj.build((s) => `«${s}»`, {
    blogger: BLOGGER,
    epoch: 0,
    kind: proj.normal,
    frames,
    delta: { messageId: 'msg-new-similar-to-a', toml: NEW_MATERIAL_SIMILAR_TO_A },
    previousTips,
  })

  assert.deepEqual(
    plan.texts.map((t) => {
      if (isPreviousTip(t)) return 'tip'
      if (isHistoricFrame(t)) return 'frame'
      if (t.includes('[[new_work_to_record]]')) return 'delta'
      return 'other'
    }),
    ['tip', 'frame', 'tip', 'frame', 'delta'],
  )

  assert.match(plan.texts[0], /kind = "previous_enforcer_tip"/)
  assert.match(plan.texts[0], new RegExp(`tip = "${TIP_X}"`))
  assert.match(plan.texts[0], /cycle = "run-obs-a"/)
  assert.ok(plan.texts[1].includes(WORK_LOG_A), 'observation A work log must sit beside tip X')
  assert.match(plan.texts[2], new RegExp(`tip = "${TIP_Y}"`))
  assert.ok(plan.texts[3].includes(WORK_LOG_B))
  assert.equal(plan.messages.at(-1).physical, true)
  assert.equal(plan.texts.at(-1), prompt.newWork(NEW_MATERIAL_SIMILAR_TO_A))

  const stillSelectable = enforcer.decodeCall({
    text: 'candidate continuation for similar material',
    tip: TIP_X,
  })
  assert.equal(stillSelectable.ok, true, 'catalog must still admit tip X for a possible true repeat')
})

test('A42_PAIRED_HISTORY_004_proved_vs_still_human', async () => {
  const host = await import('../../../dist/Enforcer/Host.js')
  assert.equal(host.trueRepeat, undefined)
  assert.equal(host.isTrueRepeat, undefined)
  assert.equal(host.judgeTrueRepeat, undefined)

  const projected = foldPairedHistory()
  const observations = readObservations(projected)
  const tipXVisible = observations.some((o) => o.tipName === TIP_X && o.cycleId === 'run-obs-a')
  assert.equal(tipXVisible, true)

  const aTokens = ['accountId', 'orderId', 'string']
  const similarToA = aTokens.some((token) => NEW_MATERIAL_SIMILAR_TO_A.includes(token) || WORK_LOG_A.includes(token))
  assert.equal(similarToA, true)

  // Harness-level candidate only: history identity + fixture similarity.
  // Not a semantic verdict that the new material is a true repeat of A.
  const candidateRepeat = tipXVisible && NEW_MATERIAL_SIMILAR_TO_A.includes('string')
  assert.equal(candidateRepeat, true)
  // This fixture proves historical visibility only. Semantic repeat judgment remains
  // a human/editorial responsibility; free-form RuleBook prose has no machine rubric gate.
})
