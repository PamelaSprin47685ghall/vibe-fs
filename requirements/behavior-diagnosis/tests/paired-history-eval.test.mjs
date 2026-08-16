// A42 Historical Pair Eval — the Blogger owner surface keeps historical tip
// identity beside each frame while leaving semantic repeat judgment human.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as observation from '../../../dist/Enforcer/ObservationSurface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const FIXTURES = join(dirname(fileURLToPath(import.meta.url)), 'fixtures')
const TIP_X = 'primitive-obsession'
const TIP_Y = 'ignored-tdd'
const WORK_LOG_A = readFileSync(join(FIXTURES, 'observation-a-work-log.txt'), 'utf8').trim()
const WORK_LOG_B = readFileSync(join(FIXTURES, 'observation-b-work-log.txt'), 'utf8').trim()
const NEW_MATERIAL_SIMILAR_TO_A = `[[new_work_to_record]]
assistant = "Coder 修改 /src/tenancy/bind.ts：userId 与 tenantId 均声明为 string，绑定函数可互换两个标识。"
`

const cycle = (n, tip, run) => ({
  mainSessionId: 'ses-paired-main',
  bloggerSessionId: 'ses-paired-blogger',
  run,
  toolCallIds: [`call-paired-${n}`],
  textRef: `blob-paired-${n}`,
  textDigest: `sha-obs-${n === 1 ? 'a' : 'b'}`,
  tipRuleId: tip,
  fieldNameAtCommit: tip,
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
})

const foldPairedHistory = () => {
  let enforcement = observation.emptyEnforcement
  let blogState = observation.emptyBlog
  const frames = [
    { digest: 'sha-obs-a', ref: 'blob-frame-a' },
    { digest: 'sha-obs-b', ref: 'blob-frame-b' },
  ]

  for (let n = 1; n <= 2; n += 1) {
    const appliedCycle = observation.applyEnforcementCycle(enforcement, cycle(n, n === 1 ? TIP_X : TIP_Y, `run-obs-${n === 1 ? 'a' : 'b'}`))
    assert.equal(appliedCycle.ok, true, appliedCycle.ok ? '' : appliedCycle.error)
    enforcement = appliedCycle.value
    const entry = observation.applyBlogEntry(
      {
        frameEpoch: 0,
        previousIngestedThroughSequence: n - 1,
        nextIngestedThroughSequence: n,
        previousCoverableTurnCutoffExclusive: n - 1,
        nextCoverableTurnCutoffExclusive: n,
        nextCoveredPrefixDigest: `digest-paired-${n}`,
      },
      observation.blogFrame({ kind: 'Entry', ...frames[n - 1] }),
      blogState,
    )
    assert.equal(entry.ok, true, entry.ok ? '' : entry.error)
    blogState = entry.value
  }

  return { enforcement, blog: blogState }
}

const readObservations = ({ enforcement, blog: blogState }) =>
  observation.observationsOf(enforcement, blogState).map((o) => ({
    tipName: o.tipName,
    cycleId: o.cycleId,
    frameDigest: o.frameDigest,
  }))

const isPreviousTip = (text) => text.includes('previous_enforcer_tip')
const isHistoricFrame = (text) => text.includes('historic_frame')

test('WHAT[BD-016] A42_PAIRED_HISTORY_001_eval_loads_120_tip_catalog_from_owner_surface', () => {
  const rules = enforcer.rules()
  assert.equal(rules.length, 120)
  assert.equal(enforcer.ruleCount(), 120)
  assert.deepEqual(enforcer.fieldNames(), rules.map((r) => r.fieldName))

  const x = enforcer.tryFindByField(TIP_X)
  const y = enforcer.tryFindByField(TIP_Y)
  assert.ok(x)
  assert.equal(x.ruleId, TIP_X)
  assert.equal(x.fieldName, TIP_X)
  assert.ok(y)
  assert.equal(y.ruleId, TIP_Y)
  assert.equal(y.fieldName, TIP_Y)
  assert.notEqual(TIP_X, TIP_Y)

  const composed = enforcer.composeBloggerSystemPrompt('base blogger prompt', 'en')
  assert.match(composed, new RegExp(TIP_X))
  assert.match(composed, new RegExp(TIP_Y))
})

test('WHAT[BD-016] A42_PAIRED_HISTORY_002_observations_a_and_b_carry_real_historical_tip_ids', () => {
  const projected = foldPairedHistory()
  assert.deepEqual(readObservations(projected), [
    { tipName: TIP_X, cycleId: 'run-obs-a', frameDigest: 'sha-obs-a' },
    { tipName: TIP_Y, cycleId: 'run-obs-b', frameDigest: 'sha-obs-b' },
  ])

  const tips = observation.recentTips(projected.enforcement)
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

test('WHAT[BD-016] A42_PAIRED_HISTORY_003_selection_path_sees_tip_x_when_new_material_resembles_a', () => {
  const projected = foldPairedHistory()
  const observations = readObservations(projected)
  assert.equal(observations[0].tipName, TIP_X)

  const previousTips = observation.recentTips(projected.enforcement).map((t) => ({
    tipName: t.fieldName,
    cycleId: t.cycleId,
  }))
  assert.equal(previousTips[0].tipName, TIP_X)

  const bodiesByDigest = {
    'sha-obs-a': WORK_LOG_A,
    'sha-obs-b': WORK_LOG_B,
  }
  const frames = observations.map((o) => ({ digest: o.frameDigest, body: bodiesByDigest[o.frameDigest] }))
  assert.ok(frames[0].body.includes('accountId'))
  assert.ok(NEW_MATERIAL_SIMILAR_TO_A.includes('userId'))
  assert.ok(NEW_MATERIAL_SIMILAR_TO_A.includes('string'))

  const plan = blog.buildProjectionPlan({
    bloggerSessionId: 'ses-paired-blogger',
    frameEpoch: 0,
    kind: 'Normal',
    frameBodies: frames,
    physicalDelta: { id: 'msg-new-similar-to-a', toml: NEW_MATERIAL_SIMILAR_TO_A },
    previousTips,
    normalInstructionLines: ['normal history'],
    squashInstructionLines: ['squash history'],
  })

  assert.deepEqual(
    plan.messages.map((message) => {
      if (isPreviousTip(message.text)) return 'tip'
      if (isHistoricFrame(message.text)) return 'frame'
      if (message.text.includes('[[new_work_to_record]]')) return 'delta'
      return 'other'
    }),
    ['tip', 'frame', 'tip', 'frame', 'delta'],
  )

  assert.match(plan.messages[0].text, /kind = "previous_enforcer_tip"/)
  assert.match(plan.messages[0].text, new RegExp(`tip = "${TIP_X}"`))
  assert.match(plan.messages[0].text, /cycle = "run-obs-a"/)
  assert.ok(plan.messages[1].text.includes(WORK_LOG_A), 'observation A work log must sit beside tip X')
  assert.match(plan.messages[2].text, new RegExp(`tip = "${TIP_Y}"`))
  assert.ok(plan.messages[3].text.includes(WORK_LOG_B))
  assert.equal(plan.messages.at(-1).isPhysical, true)
  assert.ok(plan.messages.at(-1).text.includes(NEW_MATERIAL_SIMILAR_TO_A))

  const stillSelectable = enforcer.decodeCall({ text: 'candidate continuation for similar material', tip: TIP_X })
  assert.equal(stillSelectable.ok, true, 'catalog must still admit tip X for a possible true repeat')
})

test('WHAT[BD-016] A42_PAIRED_HISTORY_004_history_visibility_is_proved_without_a_true_repeat_oracle', () => {
  const projected = foldPairedHistory()
  const observations = readObservations(projected)
  const tipXVisible = observations.some((o) => o.tipName === TIP_X && o.cycleId === 'run-obs-a')
  assert.equal(tipXVisible, true)

  const aTokens = ['accountId', 'orderId', 'string']
  const similarToA = aTokens.some((token) => NEW_MATERIAL_SIMILAR_TO_A.includes(token) || WORK_LOG_A.includes(token))
  assert.equal(similarToA, true)

  // History identity plus fixture similarity is the candidate only. The owner
  // surface deliberately has no machine TrueRepeat verdict.
  const candidateRepeat = tipXVisible && NEW_MATERIAL_SIMILAR_TO_A.includes('string')
  assert.equal(candidateRepeat, true)
})
