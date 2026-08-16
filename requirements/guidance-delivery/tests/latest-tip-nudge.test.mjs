import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  agentFact,
  agentJournal,
  caseOf,
  envelope,
  listItems,
  fact,
  frameEpochId,
  blobDigest,
  blobRef,
  bloggerRequestId,
  prefixEpochId,
  providerRun,
  resultOf,
  runtimeResources,
  sessionId,
  stream,
  toList,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'

runtimeResources.installFromPackage()

const { AgentJournalModule_appendAgent } = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const { EnforcerTipGuidance_latestTipNudge: latestTipNudge } = await import(
  '../../../dist/Enforcer/Guidance/Tip.js',
)
const { EnforcerTipGuidance_latestTipGuidance: latestTipGuidance } = await import(
  '../../../dist/Enforcer/Guidance/Tip.js',
)
const { tryInject } = await import('../../../dist/OpenCode/Host/PairProgrammingThoughtTransform.js')

const main = sessionId('ses-nudge-main')
const blogger = sessionId('ses-nudge-blogger')
const journalStream = (id) => stream.session(id)

const append = async (journal, id, value, run) => {
  const result = await AgentJournalModule_appendAgent(journalStream(id), run, value, journal)
  assert.equal(caseOf(result), 'Ok')
}

const seed = async ({ withAssociation = true, withTip = true } = {}) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-latest-tip-'))
  const created = await agentJournal.create({ directory })
  assert.equal(created.ok, true)
  const journal = created.journal

  if (withAssociation) {
    await append(journal, main, agentFact('CompanionBloggerLinked', {
      SessionId: main,
      BloggerSessionId: blogger,
      BloggerAgent: 'fast-blogger',
    }))
  }

  if (withTip) {
    const field = 'primitive-obsession'
    await append(journal, main, agentFact('BlogObservationCommitted', {
      SessionId: main,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId('req-nudge-1'),
      FrameEpochId: frameEpochId(0),
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: 1n,
      PreviousCoverableTurnCutoffExclusive: 0,
      NextCoverableTurnCutoffExclusive: 1,
      NextCoveredPrefixDigest: 'digest-nudge-1',
      TextRef: blobRef('blob-nudge-1'),
      TextDigest: blobDigest('sha-nudge-1'),
      ProviderRun: providerRun('run-nudge-1'),
      ToolCallIds: [toolCallId('call-nudge-1')],
      TipRuleId: 'primitive-obsession',
      FieldNameAtCommit: field,
      EvidenceRef: undefined,
      ObservedPrefixEpochId: prefixEpochId(0),
    }), providerRun('run-nudge-1'))
  }

  return {
    journal,
    dispose: () => {
      created.dispose()
      rmSync(directory, { recursive: true, force: true })
    },
  }
}

test('WHAT[GD-004] ENFORCER_TIP_NUDGE_001_latest_tip_first_delivery_is_full_main_md', async () => {
  const fixture = await seed()
  try {
    const result = await latestTipNudge(fixture.journal, blogger)
    assert.ok(typeof result === 'string' && result.length > 0, 'expected tip guidance text')
    assert.match(result, /tip = "primitive-obsession"/)
    assert.match(result, /Create a distinct (domain )?type/)
    // Second call for same tip must be identity-only (durable Full delivery recorded).
    const again = await latestTipNudge(fixture.journal, blogger)
    assert.equal(again, 'tip: primitive-obsession')
  } finally {
    fixture.dispose()
  }
})

test('WHAT[GD-007] ENFORCER_TIP_NUDGE_001b_latestTipNudge_is_same_bytes_as_latestTipGuidance', async () => {
  const fixture = await seed()
  try {
    // 先交付一次 Full（推进 durable Frontier），之后 latest 稳定为 Identity 文本。
    await latestTipNudge(fixture.journal, blogger)
    // GD-007: latestTipNudge 是 latestTipGuidance 的同字节别名（同一时刻
    // 两者返回同一字节——Full/Identity 文本，不是旧 Nudge 字段）。
    const viaNudge = await latestTipNudge(fixture.journal, blogger)
    const viaGuidance = await latestTipGuidance(fixture.journal, blogger)
    assert.equal(viaGuidance, viaNudge)
  } finally {
    fixture.dispose()
  }
})

test('WHAT[GD-006] ENFORCER_TIP_NUDGE_002_missing_recent_tip_returns_none', async () => {
  const fixture = await seed({ withTip: false })
  try {
    assert.equal(await latestTipNudge(fixture.journal, blogger), undefined)
  } finally {
    fixture.dispose()
  }
})

test('WHAT[GD-006] ENFORCER_TIP_NUDGE_003_missing_owner_returns_none', async () => {
  const fixture = await seed({ withAssociation: false })
  try {
    assert.equal(await latestTipNudge(fixture.journal, blogger), undefined)
  } finally {
    fixture.dispose()
  }
})

const guideline = '# Pair programming auto-injected'
const anchor = toList([{ info: { id: 'user-1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }])
const markerOutput = (messages) => {
  const items = listItems(messages)
  // pair sits before trailing user: completed -, user
  const result = items.find((m) => m?.parts?.[0]?.tool === '-' && m?.parts?.[0]?.state?.status === 'completed')
  return result?.parts?.[0]?.state?.output
}

test('WHAT[GD-009] CTX_002_GUIDELINE_001_marker_without_nudge_is_guideline_text', async () => {
  const result = resultOf(await tryInject(undefined, 'ses-auto-injected', guideline, anchor))
  assert.equal(result.ok, true, result.error ?? '')
  assert.equal(markerOutput(result.value), guideline)
})

test('WHAT[GD-009] CTX_002_GUIDELINE_002_marker_with_nudge_uses_double_newline', async () => {
  const nudge = 'A domain concept is crossing a boundary as a primitive. Introduce a distinct type so invalid substitutions become impossible.'
  const result = resultOf(await tryInject(undefined, 'ses-auto-injected-nudge', `${nudge}\n\n${guideline}`, anchor))
  assert.equal(result.ok, true, result.error ?? '')
  assert.equal(markerOutput(result.value), `${nudge}\n\n${guideline}`)
})
