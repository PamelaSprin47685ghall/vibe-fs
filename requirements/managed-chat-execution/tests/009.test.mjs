import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const intent = await import("../../../dist/OpenCode/Host/ChatAdmission/IntentSurface.js");

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
  activeParticipant: null,
  activeKind: null,
  claims: [],
  acceptedContinuations: [],
  ...overrides,
})
const decide = (decoded, durable = snapshot()) => intent.resolve(decoded, durable)

test('WHAT[interaction-authority-009] explicit agent cannot infer HumanRoot while active', () => {
  assert.deepEqual(
    decide(
      message({ explicitAgent: 'manager' }),
      snapshot({ activeParticipant: 'engineer', activeKind: 'HumanRoot' }),
    ),
    { case: 'Reject', reason: 'UnknownOriginWhileActive' },
  )
})
test('WHAT[interaction-authority-009] matching user agent continues the exact active root', () => {
  assert.deepEqual(
    decide(
      message({ explicitAgent: 'engineer' }),
      snapshot({ activeParticipant: 'engineer', activeKind: 'HumanRoot' }),
    ),
    {
      case: 'ActiveHumanContinuationIntent',
      sessionId: 'ses-chat',
      physicalUserMessageId: 'msg-chat',
      participant: 'engineer',
      origin: 'HumanMessage',
    },
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const chatExecution = await import("../../../dist/Execution/Session/ChatExecution/Surface.js");

const fixture = readFileSync(
  new URL('./fixtures/chat-execution-v1.json', import.meta.url),
  'utf8',
).trim()
const keyWire = {
  SessionId: ['SessionId', 'ses-chat-fixture'],
  PhysicalUserMessageId: ['PhysicalUserMessageId', 'msg-chat-fixture'],
}
const factWire = (factCase, payload) => JSON.stringify(['Agent', ['ChatExecution', [factCase, payload]]])
const acceptedEvidence = JSON.parse(fixture)[1][1][1].Evidence
const startedEvidence = {
  Accepted: acceptedEvidence,
  ProviderRun: ['ProviderRunIdentity', 'provider-chat-fixture'],
  RequestKind: 'WorkMain',
  ProjectionChoice: 'UseCommittedEpoch',
}
const started = factWire('ProviderStarted', {
  Evidence: startedEvidence,
  Key: keyWire,
  SchemaVersion: 1,
})
const terminal = factWire('Terminal', {
  Disposition: 'Completed',
  Evidence: ['AfterProviderStart', startedEvidence],
  Key: keyWire,
  SchemaVersion: 1,
})
const canonicalize = (wire) => {
  const result = chatExecution.canonicalize(wire)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const acceptedPayload = (wire) => wire[1][1][1]

test('WHAT[managed-chat-execution-009] durable execution fact round-trip excludes process-local artifacts', () => {
  const history = [canonicalize(fixture), canonicalize(started), canonicalize(terminal)]

  for (const line of history) {
    assert.doesNotMatch(
      line,
      /"[^"]*(?:lease|handle|binding|waiter|callback|queue|cancellationToken|subscription)[^"]*"\s*:/i,
    )
  }
})
}
