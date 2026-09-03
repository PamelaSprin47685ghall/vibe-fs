import assert from 'node:assert/strict'
import test from 'node:test'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'

const durableKey = {
  sessionId: 'ses-admission',
  physicalUserMessageId: 'msg-admission',
}

const tagged = (name, value) => [name, value]
const keyWire = ({ sessionId, physicalUserMessageId }) => ({
  SessionId: tagged('SessionId', sessionId),
  PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
})
const factWire = (factCase, payload) => JSON.stringify(['Agent', ['ChatExecution', [factCase, payload]]])

const plainEvidence = (key = durableKey) => ({
  sessionId: key.sessionId,
  physicalUserMessageId: key.physicalUserMessageId,
  logicalRunId: 'run-admission',
  authorityRootUserMessageId: 'root-admission',
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'coder',
      peerAgent: 'coder',
      canonicalRole: 'coder',
      selectedTier: 'deep',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  origin: 'HumanRoot',
  effectiveAgent: 'coder',
})

const evidenceWire = (evidence) => ({
  SessionId: tagged('SessionId', evidence.sessionId),
  LogicalRunId: tagged('LogicalRunId', evidence.logicalRunId),
  AuthorityRootUserMessageId: tagged(
    'AuthorityRootUserMessageId',
    evidence.authorityRootUserMessageId,
  ),
  AuthorityKind: evidence.authorityKind,
  IdentitySeed: [
    'RootSelection',
    {
      InitialTier: 'deep',
      Origin: 'ResolvedAtRoot',
      PeerAgent: evidence.identitySeed.participantIdentity.peerAgent,
      Persona: evidence.identitySeed.participantIdentity.persona,
      PersonaCatalogVersion: evidence.identitySeed.participantIdentity.personaCatalogVersion,
      Role: evidence.identitySeed.participantIdentity.canonicalRole,
      SelectedAgent: evidence.identitySeed.participantIdentity.selectedAgent,
    },
  ],
  PhysicalUserMessageId: tagged('PhysicalUserMessageId', evidence.physicalUserMessageId),
  Origin: ['AuthorityRoot', evidence.origin],
  EffectiveAgent: evidence.effectiveAgent,
})

const acceptedWire = (evidence) =>
  factWire('Accepted', {
    SchemaVersion: 1,
    Key: keyWire(durableKey),
    Evidence: evidenceWire(evidence),
  })

const providerStartedEvidenceWire = () => ({
  Accepted: evidenceWire(durableEvidence),
  ProviderRun: tagged('ProviderRunIdentity', 'provider-admission'),
  RequestKind: 'WorkMain',
  ProjectionChoice: 'UseCommittedEpoch',
})

const startedWire = () =>
  factWire('ProviderStarted', {
    SchemaVersion: 1,
    Key: keyWire(durableKey),
    Evidence: providerStartedEvidenceWire(),
  })

const terminalWire = (disposition) =>
  factWire('Terminal', {
    SchemaVersion: 1,
    Key: keyWire(durableKey),
    Evidence: ['AfterProviderStart', providerStartedEvidenceWire()],
    Disposition: disposition,
  })

const durableEvidence = plainEvidence()
const states = [
  { label: 'None', facts: [], phase: 'None' },
  { label: 'Accepted', facts: [acceptedWire(durableEvidence)], phase: 'Accepted' },
  {
    label: 'ProviderStarted',
    facts: [acceptedWire(durableEvidence), startedWire()],
    phase: 'ProviderStarted',
  },
  ...['Completed', 'Cancelled', 'Rejected', 'Failed'].map((disposition) => ({
    label: `Terminal(${disposition})`,
    facts: [acceptedWire(durableEvidence), startedWire(), terminalWire(disposition)],
    phase: 'Terminal',
    disposition,
  })),
]

const explicitAgents = [
  { label: 'explicit absent', value: null, mismatch: false },
  { label: 'explicit matching', value: 'coder', mismatch: false },
  { label: 'explicit mismatching', value: 'other-coder', mismatch: true },
]

const evidenceCases = [
  { label: 'exact evidence', mutate: (evidence) => evidence, conflict: false, invalid: false },
  {
    label: 'conflicting evidence',
    mutate: (evidence) => ({ ...evidence, logicalRunId: 'run-conflicting' }),
    conflict: true,
    invalid: false,
  },
  {
    label: 'invalid evidence',
    mutate: (evidence) => ({ ...evidence, effectiveAgent: ' ' }),
    conflict: false,
    invalid: true,
  },
]

const stateKeys = [
  { label: 'matching state key', mismatch: false },
  { label: 'mismatched state key', mismatch: true },
]
const attemptKeys = [
  { label: 'matching attempt key', mismatch: false },
  { label: 'mismatched attempt key', mismatch: true },
]

const expectedFor = (state, stateKey, attemptKey, explicitAgent, evidenceCase, attemptedEvidence) => {
  if (state.phase !== 'None' && stateKey.mismatch) return { error: 'StateKeyMismatch' }
  if (state.phase === 'Terminal') {
    return { intent: 'AlreadyTerminal', disposition: state.disposition }
  }
  if (evidenceCase.invalid) return { error: 'AttemptEvidenceInvalid' }
  if (attemptKey.mismatch) return { error: 'AttemptKeyMismatch' }
  if (explicitAgent.mismatch) return { error: 'ExplicitAgentMismatch' }
  if (state.phase !== 'None' && evidenceCase.conflict) {
    return { error: 'ExistingEvidenceConflict' }
  }
  if (state.phase === 'None') return { intent: 'NeedAcceptance', evidence: attemptedEvidence }
  if (state.phase === 'Accepted') return { intent: 'ResumeAccepted', evidence: durableEvidence }
  return { intent: 'AlreadyStarted', evidence: durableEvidence }
}

const table = states.flatMap((state) =>
  stateKeys
    .filter((stateKey) => state.phase !== 'None' || !stateKey.mismatch)
    .flatMap((stateKey) =>
      attemptKeys.flatMap((attemptKey) =>
        explicitAgents.flatMap((explicitAgent) =>
          evidenceCases.map((evidenceCase) => {
            const messageKey = stateKey.mismatch
              ? { sessionId: 'ses-message-other', physicalUserMessageId: 'msg-message-other' }
              : durableKey
            const attemptedKey = attemptKey.mismatch
              ? { sessionId: 'ses-attempt-other', physicalUserMessageId: 'msg-attempt-other' }
              : messageKey
            const attemptedEvidence = evidenceCase.mutate(plainEvidence(attemptedKey))

            return {
              label: [
                state.label,
                stateKey.label,
                attemptKey.label,
                explicitAgent.label,
                evidenceCase.label,
              ].join(' / '),
              facts: state.facts,
              message: { ...messageKey, explicitAgent: explicitAgent.value },
              attemptedEvidence,
              expected: expectedFor(
                state,
                stateKey,
                attemptKey,
                explicitAgent,
                evidenceCase,
                attemptedEvidence,
              ),
            }
          }),
        ),
      ),
    ),
)

test('WHAT[CHATEXEC-004] pure admission decision rejects conflicting exact evidence', () => {
  assert.ok(table.length >= 48, `decision table must contain at least 48 rows; received ${table.length}`)

  const observedIntents = new Set()
  const observedErrors = new Set()

  for (const row of table) {
    const result = chatExecution.admitIntent(row.facts, row.message, row.attemptedEvidence)

    assert.equal(result.ok, !row.expected.error, `${row.label}: success classification`)

    if (row.expected.error) {
      assert.equal(result.intent, null, `${row.label}: rejected decision has no intent`)
      assert.equal(result.error?.kind, row.expected.error, `${row.label}: typed admission error`)
      observedErrors.add(result.error?.kind)
      continue
    }

    assert.equal(result.error, null, `${row.label}: admitted decision has no error`)
    assert.equal(result.intent?.kind, row.expected.intent, `${row.label}: admission intent`)
    observedIntents.add(result.intent?.kind)

    if (row.expected.disposition) {
      assert.equal(
        result.intent?.disposition,
        row.expected.disposition,
        `${row.label}: terminal disposition`,
      )
    } else {
      assert.deepEqual(result.intent?.evidence, row.expected.evidence, `${row.label}: intent evidence`)
    }
  }

  assert.deepEqual(
    [...observedIntents].sort(),
    ['AlreadyStarted', 'AlreadyTerminal', 'NeedAcceptance', 'ResumeAccepted'],
    'table must assert every admission intent',
  )
  assert.deepEqual(
    [...observedErrors].sort(),
    [
      'AttemptEvidenceInvalid',
      'AttemptKeyMismatch',
      'ExistingEvidenceConflict',
      'ExplicitAgentMismatch',
      'StateKeyMismatch',
    ],
    'table must assert every typed admission error',
  )
})
