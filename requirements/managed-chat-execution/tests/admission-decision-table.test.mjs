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

const byState = Object.fromEntries(states.map((state) => [state.label, state]))
const otherKey = {
  sessionId: 'ses-admission-other',
  physicalUserMessageId: 'msg-admission-other',
}
const exactMessage = { ...durableKey, explicitAgent: null }
const conflictEvidence = { ...durableEvidence, logicalRunId: 'run-conflicting' }
const invalidEvidence = { ...durableEvidence, effectiveAgent: ' ' }

const cases = [
  {
    label: 'wrong state key cannot borrow a terminal result',
    facts: byState['Terminal(Completed)'].facts,
    message: { ...otherKey, explicitAgent: null },
    attemptedEvidence: plainEvidence(otherKey),
    expected: { error: 'StateKeyMismatch' },
  },
  {
    label: 'exact terminal ignores a malformed new attempt',
    facts: byState['Terminal(Completed)'].facts,
    message: exactMessage,
    attemptedEvidence: invalidEvidence,
    expected: { intent: 'AlreadyTerminal', disposition: 'Completed' },
  },
  {
    label: 'malformed evidence is rejected',
    facts: [],
    message: exactMessage,
    attemptedEvidence: invalidEvidence,
    expected: { error: 'AttemptEvidenceInvalid' },
  },
  {
    label: 'attempt key must match the physical message',
    facts: [],
    message: exactMessage,
    attemptedEvidence: plainEvidence(otherKey),
    expected: { error: 'AttemptKeyMismatch' },
  },
  {
    label: 'explicit agent must match accepted identity',
    facts: [],
    message: { ...durableKey, explicitAgent: 'other-coder' },
    attemptedEvidence: durableEvidence,
    expected: { error: 'ExplicitAgentMismatch' },
  },
  {
    label: 'existing acceptance rejects conflicting evidence',
    facts: byState.Accepted.facts,
    message: exactMessage,
    attemptedEvidence: conflictEvidence,
    expected: { error: 'ExistingEvidenceConflict' },
  },
  {
    label: 'fresh exact attempt needs durable acceptance',
    facts: byState.None.facts,
    message: exactMessage,
    attemptedEvidence: durableEvidence,
    expected: { intent: 'NeedAcceptance', evidence: durableEvidence },
  },
  {
    label: 'equal accepted evidence resumes pre-provider admission',
    facts: byState.Accepted.facts,
    message: exactMessage,
    attemptedEvidence: durableEvidence,
    expected: { intent: 'ResumeAccepted', evidence: durableEvidence },
  },
  {
    label: 'equal provider-started evidence is already started',
    facts: byState.ProviderStarted.facts,
    message: exactMessage,
    attemptedEvidence: durableEvidence,
    expected: { intent: 'AlreadyStarted', evidence: durableEvidence },
  },
  ...['Cancelled', 'Rejected', 'Failed'].map((disposition) => ({
    label: `terminal ${disposition} remains exact`,
    facts: byState[`Terminal(${disposition})`].facts,
    message: exactMessage,
    attemptedEvidence: durableEvidence,
    expected: { intent: 'AlreadyTerminal', disposition },
  })),
]

test('WHAT[CHATEXEC-004] fixed admission counterworlds distinguish every intent and rejection', () => {

  const observedIntents = new Set()
  const observedErrors = new Set()

  for (const row of cases) {
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
    'fixed counterworlds must assert every admission intent',
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
    'fixed counterworlds must assert every typed admission error',
  )
})
