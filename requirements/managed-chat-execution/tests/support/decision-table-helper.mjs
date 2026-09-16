import * as chatExecution from '../../../../dist/Execution/Session/ChatExecution/Surface.js'
import { acceptManagedChat, providerStarted, terminal } from './chat-wire.mjs'

export const evaluateCounterworlds = () => {
  const durableKey = {
    sessionId: 'ses-admission',
    physicalUserMessageId: 'msg-admission',
  }

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
        canonicalRole: 'coder',
        selectedTier: 'deep',
        persona: 'Coder',
        personaCatalogVersion: 1,
        origin: 'ResolvedAtRoot',
      },
    },
    origin: 'HumanRoot',
  })

  const durableEvidence = plainEvidence()
  const acceptedFact = acceptManagedChat(
    durableEvidence.logicalRunId,
    durableEvidence.authorityRootUserMessageId,
    durableEvidence.authorityKind,
    durableEvidence.identitySeed,
    durableKey,
  )
  const startedFact = providerStarted(durableKey, 'provider-admission')

  const states = [
    { label: 'None', facts: [], phase: 'None' },
    { label: 'Accepted', facts: [acceptedFact], phase: 'Accepted' },
    { label: 'ProviderStarted', facts: [acceptedFact, startedFact], phase: 'ProviderStarted' },
    ...['Completed', 'Cancelled', 'Rejected', 'Failed'].map((disposition) => ({
      label: `Terminal(${disposition})`,
      facts: [acceptedFact, startedFact, terminal(durableKey, 'provider-admission', disposition)],
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
  const invalidEvidence = { ...durableEvidence, logicalRunId: ' ' }

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

  const observedIntents = new Set()
  const observedErrors = new Set()

  for (const row of cases) {
    const result = chatExecution.admitIntent(row.facts, row.message, row.attemptedEvidence)
    if (row.expected.error) {
      if (!result.ok && result.error?.kind === row.expected.error) {
        observedErrors.add(result.error?.kind)
      }
    } else {
      if (result.ok && result.intent?.kind === row.expected.intent) {
        observedIntents.add(result.intent?.kind)
      }
    }
  }

  const allIntentsDistinguished = ['AlreadyStarted', 'AlreadyTerminal', 'NeedAcceptance', 'ResumeAccepted'].every((k) => observedIntents.has(k))
  const allErrorsDistinguished = ['AttemptEvidenceInvalid', 'AttemptKeyMismatch', 'ExistingEvidenceConflict', 'ExplicitAgentMismatch', 'StateKeyMismatch'].every((k) => observedErrors.has(k))

  return {
    allDistinguished: allIntentsDistinguished && allErrorsDistinguished,
  }
}
