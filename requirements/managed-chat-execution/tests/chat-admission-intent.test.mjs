import assert from 'node:assert/strict'
import test from 'node:test'

import * as intent from '../../../dist/OpenCode/Host/ChatAdmission/IntentSurface.js'

const message = (overrides = {}) => ({
  sessionId: 'ses-chat',
  physicalUserMessageId: 'msg-chat',
  explicitAgent: null,
  promptKey: null,
  hostCompaction: false,
  hostSynthetic: false,
  ...overrides,
})

const snapshot = (overrides = {}) => ({
  available: true,
  activeAgent: null,
  activeKind: null,
  claims: [],
  acceptedContinuations: [],
  ...overrides,
})

const decide = (decoded, durable = snapshot()) => intent.resolve(decoded, durable)

test('WHAT[INTERACTION-AUTHORITY-005] exhaustive chat admission intent table', () => {
  const claim = {
    promptKey: 'prompt-1',
    sessionId: 'ses-chat',
    origin: 'InteractionRepair',
    effectiveAgent: 'coder',
    selectedAgent: 'coder',
  }

  const cases = [
    {
      label: 'unmanaged fresh host message',
      decoded: message(),
      expected: { case: 'NoManagedExecution', reason: 'UnmanagedMessage' },
    },
    {
      label: 'fresh external managed root',
      decoded: message({ explicitAgent: 'coder' }),
      expected: {
        case: 'ExternalRootIntent',
        sessionId: 'ses-chat',
        physicalUserMessageId: 'msg-chat',
        explicitAgent: 'coder',
        effectiveAgent: 'coder',
        origin: 'HumanRoot',
        identitySeed: 'RootSelection',
        selectedAgent: 'coder',
      },
    },
    {
      label: 'exact claimed plugin prompt',
      decoded: message({ promptKey: 'prompt-1' }),
      durable: snapshot({ claims: [claim] }),
      expected: {
        case: 'PendingPromptIntent',
        sessionId: 'ses-chat',
        physicalUserMessageId: 'msg-chat',
        promptKey: 'prompt-1',
        effectiveAgent: 'coder',
        origin: 'InteractionRepair',
        identitySeed: 'RootSelection',
        selectedAgent: 'coder',
      },
    },
    {
      label: 'host compaction',
      decoded: message({ hostCompaction: true }),
      expected: { case: 'HostInternal', origin: 'HostInternal' },
    },
    {
      label: 'host synthetic',
      decoded: message({ hostSynthetic: true }),
      expected: { case: 'HostInternal', origin: 'HostInternal' },
    },
  ]

  for (const row of cases) {
    assert.deepEqual(intent.resolve(row.decoded, row.durable ?? snapshot()), row.expected, row.label)
  }
})

test('WHAT[INTERACTION-AUTHORITY-007] unknown origin is rejected while active', () => {
  assert.deepEqual(
    decide(message(), snapshot({ activeAgent: 'coder', activeKind: 'HumanRoot' })),
    { case: 'Reject', reason: 'UnknownOriginWhileActive' },
  )
})

test('WHAT[INTERACTION-AUTHORITY-009] explicit agent cannot infer HumanRoot while active', () => {
  assert.deepEqual(
    decide(
      message({ explicitAgent: 'inspector' }),
      snapshot({ activeAgent: 'coder', activeKind: 'HumanRoot' }),
    ),
    { case: 'Reject', reason: 'UnknownOriginWhileActive' },
  )
})

test('WHAT[INTERACTION-AUTHORITY-009] matching user agent continues the exact active root', () => {
  assert.deepEqual(
    decide(
      message({ explicitAgent: 'coder' }),
      snapshot({ activeAgent: 'coder', activeKind: 'HumanRoot' }),
    ),
    {
      case: 'ActiveHumanContinuationIntent',
      sessionId: 'ses-chat',
      physicalUserMessageId: 'msg-chat',
      effectiveAgent: 'coder',
      origin: 'HumanMessage',
      selectedAgent: 'coder',
    },
  )
})

test('WHAT[INTERACTION-AUTHORITY-005] rejects managed intent without physical message identity', () => {
  assert.deepEqual(decide(message({ physicalUserMessageId: null, explicitAgent: 'coder' })), {
    case: 'Reject',
    reason: 'ManagedIntentMissingPhysicalUserMessageId',
  })

  assert.deepEqual(
    decide(
      message({ physicalUserMessageId: null, promptKey: 'prompt-1' }),
      snapshot({
        claims: [
          {
            promptKey: 'prompt-1',
            sessionId: 'ses-chat',
            origin: 'InteractionRepair',
            effectiveAgent: 'coder',
            selectedAgent: 'coder',
          },
        ],
      }),
    ),
    { case: 'Reject', reason: 'ManagedIntentMissingPhysicalUserMessageId' },
  )
})

test('WHAT[INTERACTION-AUTHORITY-005] rejects insufficient exact identity evidence', () => {
  const rows = [
    [message({ sessionId: null, explicitAgent: 'coder' }), 'ManagedIntentMissingSessionId'],
    [message({ explicitAgent: 'legacy-coder' }), 'InvalidExplicitAgent'],
    [message({ promptKey: 'missing' }), 'PromptKeyNotClaimed'],
  ]

  for (const [decoded, reason] of rows) {
    assert.deepEqual(decide(decoded), { case: 'Reject', reason })
  }
})

test('WHAT[INTERACTION-AUTHORITY-008] accepted Host identity outranks claim and compaction', () => {
  const durable = snapshot({
    claims: [
      {
        promptKey: 'prompt-1',
        sessionId: 'ses-chat',
        origin: 'InteractionRepair',
        effectiveAgent: 'coder',
        selectedAgent: 'coder',
      },
    ],
    acceptedContinuations: [{ physicalUserMessageId: 'msg-chat', origin: 'JoinGuard' }],
  })

  assert.deepEqual(
    decide(message({ promptKey: 'prompt-1', hostCompaction: true }), durable),
    { case: 'NoManagedExecution', reason: 'AlreadyAcceptedHostMessage', origin: 'JoinGuard' },
  )
})

test('WHAT[INTERACTION-AUTHORITY-008] registered AgentOwnerRoot outranks external root inference', () => {
  assert.deepEqual(
    decide(
      message({ promptKey: 'unclaimed-owner-root', explicitAgent: 'reviewer' }),
      snapshot({ activeAgent: 'coder', activeKind: 'AgentOwnerRoot' }),
    ),
    { case: 'Reject', reason: 'AgentOwnerRootPromptNotClaimed' },
  )
})

test('WHAT[INTERACTION-AUTHORITY-008] plugin claim is frozen even if the later projection changes', () => {
  const durable = snapshot({
    claims: [
      {
        promptKey: 'prompt-1',
        sessionId: 'ses-chat',
        origin: 'InteractionRepair',
        effectiveAgent: 'coder',
        selectedAgent: 'coder',
      },
    ],
  })

  const resolved = decide(message({ promptKey: 'prompt-1' }), durable)
  durable.claims[0].effectiveAgent = 'reviewer'
  durable.claims.length = 0

  assert.equal(resolved.case, 'PendingPromptIntent')
  assert.equal(resolved.promptKey, 'prompt-1')
  assert.equal(resolved.effectiveAgent, 'coder')
  assert.equal(resolved.selectedAgent, 'coder')
})
