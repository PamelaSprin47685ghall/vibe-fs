import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  abandonReason,
  agentFact,
  agentJournal,
  authority,
  authorityRoot,
  bloggerRequestId,
  caseOf,
  hostToolPartId,
  idValue,
  logicalRunId,
  payloadOf,
  promptKey,
  providerRun,
  sessionId,
  stream,
  toList,
  toolCallId,
} from '../../../verification-system/tests/support/domain.mjs'

const { AgentJournalModule_appendAgent } = await import('../../../../dist/Persistence/Journal/AgentJournal.js')
const { SessionMessage, SessionToolPart, SnapshotToolPartState } = await import(
  '../../../../dist/OpenCode/Host/SessionSnapshotPort.js'
)
const probe = await import('../../../../dist/Enforcer/Cycle/BloggerProbe.js')
const rejudge = probe.BloggerRecoveryProbe_rejudgeToolRecovery ?? probe.rejudgeToolRecovery
const repairState = probe.BloggerRecoveryProbe_repairState ?? probe.repairState

const chronicle = (suffix, state = new SnapshotToolPartState(1, ['{}'])) =>
  new SessionToolPart(
    hostToolPartId(`part-chronicle-${suffix}`),
    toolCallId(`call-chronicle-${suffix}`),
    'chronicle',
    '{}',
    state,
  )

const assistant = (id, toolParts) =>
  new SessionMessage(
    id,
    'assistant',
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    true,
    false,
    undefined,
    [],
    toolParts,
  )

export const snapshotRejudgeChronicleCardinality = async () => {
  const directory = mkdtempSync(join(tmpdir(), 'enforcer-153-chronicle-evidence-'))
  const created = await agentJournal.create({ directory })
  assert.equal(created.ok, true)
  const journal = created.journal
  const blog = sessionId('ses-blog-tool-evidence')

  try {
    const root = await AgentJournalModule_appendAgent(
      stream.session(blog),
      undefined,
      agentFact('AuthorityRootAccepted', {
        SessionId: blog,
        LogicalRunId: logicalRunId('blog-run-tool-evidence'),
        AuthorityRootUserMessageId: authorityRoot('msg-blog-tool-evidence'),
        AuthorityKind: 'AgentOwnerRoot',
        SelectedAgent: 'fast-blogger',
        PeerAgent: 'deep-blogger',
        CanonicalRole: 'blogger',
        SelectedTier: 'fast',
      }),
      journal,
    )
    assert.equal(caseOf(root), 'Ok')

    const claimedRun = providerRun('asst-nudge-origin')
    const claim = await AgentJournalModule_appendAgent(
      stream.session(blog),
      undefined,
      agentFact('PluginPromptClaimed', {
        PromptKey: promptKey('pk-nudge-origin'),
        SessionId: blog,
        ContinuationKind: 'InteractionRepair',
        LogicalRunId: logicalRunId('blog-run-tool-evidence'),
        AuthorityRootUserMessageId: authorityRoot('msg-blog-tool-evidence'),
        EffectiveAgent: 'fast-blogger',
        PayloadDigest: authority.repairPayloadDigest(bloggerRequestId('req-tool-evidence'), claimedRun, 'blogger-missing-tool'),
      }),
      journal,
    )
    assert.equal(caseOf(claim), 'Ok')

    const first = chronicle('1')
    const one = rejudge(
      journal,
      blog,
      bloggerRequestId('req-tool-evidence'),
      toList([assistant('asst-nudge-origin', []), assistant('asst-fixed', [first])]),
    )
    const two = rejudge(
      journal,
      blog,
      bloggerRequestId('req-tool-evidence'),
      toList([
        assistant('asst-nudge-origin', []),
        assistant('asst-invalid-multi', [first, chronicle('2')]),
      ]),
    )
    const mixed = rejudge(
      journal,
      blog,
      bloggerRequestId('req-tool-evidence'),
      toList([
        assistant('asst-nudge-origin', []),
        assistant('asst-invalid-mixed', [first, chronicle('failed', new SnapshotToolPartState(2, ['error']))]),
      ]),
    )

    return { one: caseOf(one), two: caseOf(two), mixed: caseOf(mixed) }
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}

export const repairStateRequestIsolationAndAbandonLifecycle = async () => {
  const directory = mkdtempSync(join(tmpdir(), 'enforcer-153-repair-lifecycle-'))
  const created = await agentJournal.create({ directory })
  assert.equal(created.ok, true)
  const journal = created.journal
  const blog = sessionId('ses-blog-repair-lifecycle')
  const request1 = bloggerRequestId('req-1')

  try {
    const root = await AgentJournalModule_appendAgent(
      stream.session(blog),
      undefined,
      agentFact('AuthorityRootAccepted', {
        SessionId: blog,
        LogicalRunId: logicalRunId('blog-run-1'),
        AuthorityRootUserMessageId: authorityRoot('msg-blog-root'),
        AuthorityKind: 'AgentOwnerRoot',
        SelectedAgent: 'fast-blogger',
        PeerAgent: 'deep-blogger',
        CanonicalRole: 'blogger',
        SelectedTier: 'fast',
      }),
      journal,
    )
    assert.equal(caseOf(root), 'Ok')

    const nudge = await AgentJournalModule_appendAgent(
      stream.session(blog),
      undefined,
      agentFact('PluginPromptClaimed', {
        PromptKey: promptKey('pk-claimed'),
        SessionId: blog,
        ContinuationKind: 'InteractionRepair',
        LogicalRunId: logicalRunId('blog-run-1'),
        AuthorityRootUserMessageId: authorityRoot('msg-blog-root'),
        EffectiveAgent: 'fast-blogger',
        PayloadDigest: authority.repairPayloadDigest(request1, providerRun('asst-p1'), 'blogger-missing-tool'),
      }),
      journal,
    )
    assert.equal(caseOf(nudge), 'Ok')

    const sameRequest = repairState(journal, blog, 'req-1', providerRun('asst-p2'), [])
    const otherRequestAfterNudge = repairState(journal, blog, 'req-2', providerRun('asst-p2'), [])

    const aabb = await AgentJournalModule_appendAgent(
      stream.session(blog),
      undefined,
      agentFact('PluginPromptClaimed', {
        PromptKey: promptKey('pk-old-aabb'),
        SessionId: blog,
        ContinuationKind: 'InteractionRepair',
        LogicalRunId: logicalRunId('blog-run-1'),
        AuthorityRootUserMessageId: authorityRoot('msg-blog-root'),
        EffectiveAgent: 'fast-blogger',
        PayloadDigest: authority.repairPayloadDigest(request1, providerRun('asst-old-aabb'), 'blogger-aabb'),
      }),
      journal,
    )
    assert.equal(caseOf(aabb), 'Ok')
    const otherRequestAfterAabb = repairState(journal, blog, 'req-2', providerRun('asst-p3'), [])

    const abandoned = await AgentJournalModule_appendAgent(
      stream.session(blog),
      undefined,
      agentFact('PluginPromptAbandoned', {
        PromptKey: promptKey('pk-old-aabb'),
        SessionId: blog,
        Reason: abandonReason.sendFailed('host rejected AABB before acceptance'),
      }),
      journal,
    )
    assert.equal(caseOf(abandoned), 'Ok')
    const afterAbandonedAabb = repairState(journal, blog, 'req-1', providerRun('asst-p3'), [])

    return {
      sameRequest: caseOf(sameRequest),
      sameRequestClaimedRun: idValue.providerRun(payloadOf(sameRequest)),
      otherRequestAfterNudge: caseOf(otherRequestAfterNudge),
      otherRequestAfterAabb: caseOf(otherRequestAfterAabb),
      afterAbandonedAabb: caseOf(afterAbandonedAabb),
    }
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}
