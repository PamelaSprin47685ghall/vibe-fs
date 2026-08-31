import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const key = {
  sessionId: 'ses-acceptance',
  physicalUserMessageId: 'msg-acceptance',
}

const evidence = (overrides = {}) => ({
  sessionId: key.sessionId,
  physicalUserMessageId: key.physicalUserMessageId,
  logicalRunId: 'run-acceptance',
  authorityRootUserMessageId: 'root-acceptance',
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'fast-coder',
      peerAgent: 'deep-coder',
      canonicalRole: 'coder',
      selectedTier: 'fast',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  origin: 'HumanRoot',
  effectiveAgent: 'fast-coder',
  ...overrides,
})

const accepted = (attempt = evidence(), appendOutcome = 'Committed') =>
  chatExecution.acceptanceScenario(attempt, appendOutcome)

const withJournal = async (label, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-chat-acceptance-${label}-`))
  const opened = await journal.JournalSurface_bootWithWriterId(
    directory,
    `writer-${label}`,
    `runtime-${label}`,
    4242,
    '2026-08-30T00:00:00Z',
  )
  assert.equal(opened.ok, true, JSON.stringify(opened.error))

  try {
    await action(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

const rootIdentity = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    selectedAgent: 'fast-manager',
    peerAgent: 'deep-manager',
    canonicalRole: 'manager',
    selectedTier: 'fast',
    persona: 'Coordinator',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const hostPort = (sendPrompt) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: sendPrompt,
})

test('WHAT[CHATEXEC-011] external and plugin roots share AcceptManagedChatIntent', async () => {
  await withJournal('shared-owner', async (handle) => {
    const external = await dispatch.acceptManagedExternal(
      handle,
      'ses-external',
      'msg-external',
      'fast-manager',
    )
    assert.equal(external.ok, true, external.error)
    assert.equal(external.origin, 'HumanRoot')

    const owner = await dispatch.acceptHumanRootSelection(
      handle,
      'ses-owner',
      'msg-owner',
      rootIdentity,
    )
    assert.equal(owner.ok, true, owner.error)

    const inherited = authority.issueInheritedIdentitySeed('fast-coder', owner.profile)
    assert.equal(inherited.ok, true, inherited.error)

    const sent = await dispatch.sendAgentOwnerRoot(
      hostPort(async () => dispatch.admittedWithReceipt('plugin-root-receipt')),
      handle,
      'ses-plugin',
      'plugin work',
      inherited.value,
    )
    assert.equal(sent.ok, true, sent.error)

    const plugin = await dispatch.acceptManagedPromptClaim(
      handle,
      'ses-plugin',
      'msg-plugin',
      sent.key,
      'fast-coder',
    )
    assert.equal(plugin.ok, true, plugin.error)
    assert.equal(plugin.origin, 'AgentOwnerRoot')
  })
})

test('WHAT[CHATEXEC-004] durable acceptance is projected before its witness exists', async () => {
  const result = await accepted()

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.deepEqual(result.trace, ['Read', 'Append', 'Committed', 'ReRead', 'Witness'])
  assert.deepEqual(result.witness.key, key)
  assert.deepEqual(result.witness.evidence, evidence())
  assert.equal(result.acceptanceAppendCount, 1)
  assert.equal(result.capacityEffectCount, 0)
  assert.equal(result.hostEffectCount, 0)
})

test('WHAT[CHATEXEC-004] exact duplicate reconstructs an equivalent witness without another append', async () => {
  const result = await chatExecution.acceptanceDuplicateScenario(evidence())

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.deepEqual(result.firstWitness, result.secondWitness)
  assert.equal(result.acceptanceAppendCount, 1)
  assert.deepEqual(result.secondTrace, ['Read', 'Witness'])
})

test('WHAT[CHATEXEC-004] established evidence conflict is typed and appends nothing', async () => {
  const result = await chatExecution.acceptanceConflictScenario(
    evidence(),
    evidence({ effectiveAgent: 'deep-coder' }),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'EstablishedEvidenceConflict')
  assert.equal(result.acceptanceAppendCount, 1)
  assert.equal(result.witness, null)
})

for (const outcome of ['NotAttempted', 'CommitUnknown']) {
  test(`WHAT[CHATEXEC-004] persistence ${outcome} does not acquire capacity after journal uncertainty`, async () => {
    const result = await accepted(evidence(), outcome)

    assert.equal(result.ok, false)
    assert.equal(result.error.kind, outcome)
    assert.equal(result.witness, null)
    assert.equal(result.capacityEffectCount, 0)
    assert.equal(result.hostEffectCount, 0)
    assert.equal(result.trace.includes('Witness'), false)
  })
}
