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
} from '../support/domain.mjs'

runtimeResources.installFromPackage()

const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')
const { latestTipNudge } = await import('../../../dist/Session/EnforcerHost.js')
const { tryInject } = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')

const main = sessionId('ses-nudge-main')
const blogger = sessionId('ses-nudge-blogger')
const journalStream = (id) => stream.session(id)

const append = (journal, id, value, run) => {
  const result = AgentJournalModule_appendAgent(journalStream(id), run, value, journal)
  assert.equal(caseOf(result), 'Ok')
}

const seed = ({ withAssociation = true, withTip = true } = {}) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-latest-tip-'))
  const created = agentJournal.create({ directory })
  assert.equal(created.ok, true)
  const journal = created.journal

  if (withAssociation) {
    append(journal, main, agentFact('CompanionBloggerLinked', {
      SessionId: main,
      BloggerSessionId: blogger,
      BloggerAgent: 'fast-blogger',
    }))
  }

  if (withTip) {
    const field = 'primitive-obsession'
    append(journal, main, agentFact('BlogEntryCommitted', {
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

test('ENFORCER_TIP_NUDGE_001_latest_tip_uses_owner_recent_tip_catalog_nudge', () => {
  const fixture = seed()
  try {
    const result = latestTipNudge(fixture.journal, blogger)
    assert.equal(result, 'A domain concept is crossing a boundary as a primitive. Introduce a distinct type so invalid substitutions become impossible.')
  } finally {
    fixture.dispose()
  }
})

test('ENFORCER_TIP_NUDGE_002_missing_recent_tip_returns_none', () => {
  const fixture = seed({ withTip: false })
  try {
    assert.equal(latestTipNudge(fixture.journal, blogger), undefined)
  } finally {
    fixture.dispose()
  }
})

test('ENFORCER_TIP_NUDGE_003_missing_owner_returns_none', () => {
  const fixture = seed({ withAssociation: false })
  try {
    assert.equal(latestTipNudge(fixture.journal, blogger), undefined)
  } finally {
    fixture.dispose()
  }
})

const guideline = '# Pair programming auto-injected'
const anchor = toList([{ info: { id: 'user-1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }])
const markerOutput = (messages) => {
  const items = listItems(messages)
  // pair sits before trailing user: call, result, user
  const result = items.find((m) => m?.parts?.[0]?.tool === 'auto-injected' && m?.parts?.[0]?.state?.status === 'completed')
  return result?.parts?.[0]?.state?.output
}

test('CTX_002_GUIDELINE_001_marker_without_nudge_is_guideline_text', () => {
  const result = resultOf(tryInject(undefined, 'ses-auto-injected', guideline, anchor))
  assert.equal(result.ok, true, result.error ?? '')
  assert.equal(markerOutput(result.value), guideline)
})

test('CTX_002_GUIDELINE_002_marker_with_nudge_uses_double_newline', () => {
  const nudge = 'A domain concept is crossing a boundary as a primitive. Introduce a distinct type so invalid substitutions become impossible.'
  const result = resultOf(tryInject(undefined, 'ses-auto-injected-nudge', `${nudge}\n\n${guideline}`, anchor))
  assert.equal(result.ok, true, result.error ?? '')
  assert.equal(markerOutput(result.value), `${nudge}\n\n${guideline}`)
})
