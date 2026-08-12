// Rulebook Main tip Full / Identity delivery (first Full main.md, repeat identity).
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  agentFact,
  agentJournal,
  caseOf,
  runtimeResources,
  providerLanguage,
  sessionId,
  stream,
  bloggerRequestId,
  frameEpochId,
  blobDigest,
  blobRef,
  prefixEpochId,
  providerRun,
  toolCallId,
} from '../support/domain.mjs'

runtimeResources.installFromPackage()

const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')
const {
  EnforcerTipGuidance_latestTipGuidance: latestTipGuidance,
  EnforcerTipGuidance_latestTipNudge: latestTipNudge,
  EnforcerTipGuidance_resolveTipGuidance: resolveTipGuidance,
} = await import('../../../dist/Session/EnforcerTipGuidance.js')

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const MAIN_MD = readFileSync(
  join(ROOT, 'resources/enforcer/primitive-obsession/main.md'),
  'utf8',
).trim()
const MAIN_ZH_CN_MD = readFileSync(
  join(ROOT, 'resources/enforcer/primitive-obsession/main.zh-CN.md'),
  'utf8',
).trim()

const main = sessionId('ses-tip-delivery-main')
const blogger = sessionId('ses-tip-delivery-blogger')
const journalStream = (id) => stream.session(id)

const append = (journal, id, value, run) => {
  const result = AgentJournalModule_appendAgent(journalStream(id), run, value, journal)
  assert.equal(caseOf(result), 'Ok', `append failed: ${JSON.stringify(result)}`)
}

const seedOwnerWithTip = (journal, { tip = 'primitive-obsession', runSuffix = '1' } = {}) => {
  append(
    journal,
    main,
    agentFact('CompanionBloggerLinked', {
      SessionId: main,
      BloggerSessionId: blogger,
      BloggerAgent: 'fast-blogger',
    }),
  )

  append(
    journal,
    main,
    agentFact('BlogObservationCommitted', {
      SessionId: main,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId(`req-tip-${runSuffix}`),
      FrameEpochId: frameEpochId(0),
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: BigInt(runSuffix),
      PreviousCoverableTurnCutoffExclusive: 0,
      NextCoverableTurnCutoffExclusive: Number(runSuffix),
      NextCoveredPrefixDigest: `digest-tip-${runSuffix}`,
      TextRef: blobRef(`blob-tip-${runSuffix}`),
      TextDigest: blobDigest(`sha-tip-${runSuffix}`),
      ProviderRun: providerRun(`run-tip-${runSuffix}`),
      ToolCallIds: [toolCallId(`call-tip-${runSuffix}`)],
      TipRuleId: tip,
      FieldNameAtCommit: tip,
      EvidenceRef: undefined,
      ObservedPrefixEpochId: prefixEpochId(0),
    }),
    providerRun(`run-tip-${runSuffix}`),
  )
}

const withJournal = (fn) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-tip-guidance-'))
  const created = agentJournal.create({ directory })
  assert.equal(created.ok, true)
  try {
    return fn(created.journal)
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}

const presentationOf = (guidance) => {
  if (guidance == null) return undefined
  const p = guidance.Presentation
  if (p == null) return undefined
  if (typeof p === 'string') return p
  // Fable RequireQualifiedAccess DU compiles to { tag, fields }.
  // Declaration order: Full = 0, IdentityOnly = 1.
  if (typeof p.tag === 'number') {
    if (p.tag === 0) return 'Full'
    if (p.tag === 1) return 'IdentityOnly'
  }
  if (Object.prototype.hasOwnProperty.call(p, 'Full')) return 'Full'
  if (Object.prototype.hasOwnProperty.call(p, 'IdentityOnly')) return 'IdentityOnly'
  return undefined
}

const textOf = (guidance) => guidance?.Text ?? guidance?.text

test('ENFORCER_TIP_DELIVERY_001_first_resolve_is_full_main_md', () => {
  withJournal((journal) => {
    seedOwnerWithTip(journal)
    const guidance = resolveTipGuidance(journal, main)
    assert.ok(guidance, 'expected tip guidance')
    assert.equal(presentationOf(guidance), 'Full')
    const text = textOf(guidance)
    assert.match(text, /tip = "primitive-obsession"/)
    assert.ok(
      text.includes(MAIN_MD) || text.includes('Introduce a distinct type so invalid substitutions become impossible.'),
      'Full guidance must include main.md body',
    )
    assert.ok(text.length > 80, `Full text too short: ${text.length}`)
  })
})

test('ENFORCER_TIP_DELIVERY_002_second_resolve_same_tip_is_identity_only', () => {
  withJournal((journal) => {
    seedOwnerWithTip(journal)
    const first = resolveTipGuidance(journal, main)
    assert.equal(presentationOf(first), 'Full')
    const firstText = textOf(first)

    const second = resolveTipGuidance(journal, main)
    assert.ok(second, 'expected repeat guidance')
    assert.equal(presentationOf(second), 'IdentityOnly')
    const secondText = textOf(second)
    assert.equal(secondText, 'tip: primitive-obsession')
    assert.ok(!secondText.includes(MAIN_MD), 'Identity must not repeat full main.md')
    assert.ok(
      !secondText.includes('Introduce a distinct type so invalid substitutions become impossible.'),
      'Identity must not include main body nudge sentence',
    )
    assert.ok(firstText.length > secondText.length, 'Full body longer than identity')
  })
})

test('ENFORCER_TIP_DELIVERY_003_latestTipGuidance_matches_resolve_text', () => {
  withJournal((journal) => {
    seedOwnerWithTip(journal)
    const viaResolve = textOf(resolveTipGuidance(journal, main))
    // second call after Full already recorded
    const viaLatest = latestTipGuidance(journal, main)
    const viaAlias = latestTipNudge(journal, main)
    assert.equal(viaLatest, 'tip: primitive-obsession')
    assert.equal(viaAlias, viaLatest)
    assert.ok(viaResolve.includes('primitive-obsession'))
  })
})

test('ENFORCER_TIP_DELIVERY_004_blogger_session_id_resolves_owner_main', () => {
  withJournal((journal) => {
    seedOwnerWithTip(journal)
    const viaBlogger = resolveTipGuidance(journal, blogger)
    assert.ok(viaBlogger, 'blogger id must resolve to owner tip')
    assert.equal(presentationOf(viaBlogger), 'Full')
    assert.match(textOf(viaBlogger), /tip = "primitive-obsession"/)
  })
})

test('ENFORCER_TIP_DELIVERY_005_missing_tip_returns_none', () => {
  withJournal((journal) => {
    append(
      journal,
      main,
      agentFact('CompanionBloggerLinked', {
        SessionId: main,
        BloggerSessionId: blogger,
        BloggerAgent: 'fast-blogger',
      }),
    )
    assert.equal(resolveTipGuidance(journal, main), undefined)
    assert.equal(latestTipGuidance(journal, main), undefined)
  })
})

test('ENFORCER_PROMPT_017_full_tip_guidance_uses_owner_session_zh_cn_rulebook', () => {
  providerLanguage.clearAllForTests()
  try {
    const bound = providerLanguage.bindOnce(main, providerLanguage.simplifiedChinese)
    assert.equal(bound.ok, true)
    withJournal((journal) => {
      seedOwnerWithTip(journal, { runSuffix: '7' })
      const guidance = resolveTipGuidance(journal, main)
      assert.equal(presentationOf(guidance), 'Full')
      const text = textOf(guidance)
      assert.match(text, /# Enforcer Tip（规则提示）/)
      assert.ok(text.includes(MAIN_ZH_CN_MD), 'Full zh-CN guidance must include main.zh-CN.md body')
      assert.match(text, /[\u3400-\u9fff]/)
      assert.ok(!text.includes(MAIN_MD), 'zh-CN guidance must not silently fall back to English main.md')
    })
  } finally {
    providerLanguage.clearAllForTests()
  }
})

test('ENFORCER_TIP_DELIVERY_006_context_reanchor_clears_full_so_next_is_full_again', () => {
  withJournal((journal) => {
    seedOwnerWithTip(journal)
    const first = resolveTipGuidance(journal, main)
    assert.equal(presentationOf(first), 'Full')
    assert.equal(presentationOf(resolveTipGuidance(journal, main)), 'IdentityOnly')

    // HOST-006: compaction reanchor voids FullDeliveredTips with Blog/Prefix.
    append(
      journal,
      main,
      agentFact('ContextReanchored', {
        SessionId: main,
        PreviousEpochId: prefixEpochId(0),
        NextEpochId: prefixEpochId(1),
        ObservedCompactionRun: providerRun('run-compaction-tip'),
      }),
      providerRun('run-compaction-tip'),
    )

    const after = resolveTipGuidance(journal, main)
    assert.ok(after, 'post-reanchor must still resolve tip')
    assert.equal(presentationOf(after), 'Full', 'reanchor must re-emit Full main.md')
    assert.match(textOf(after), /tip = "primitive-obsession"/)
    assert.ok(
      textOf(after).includes('Introduce a distinct type so invalid substitutions become impossible.') ||
        textOf(after).includes(MAIN_MD),
      're-Full must include main body',
    )
  })
})
