import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const journalCodec = await import("../../../dist/Persistence/Journal/CodecSurface.js");
const factCodec = await import("../../../dist/Persistence/Journal/FactCodecSurface.js");

const SESSION = 'ses_a'
const ROOT_ACCEPTED = {
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SchemaVersion: 2,
    SessionId: SESSION,
    LogicalRunId: 'run_a',
    AuthorityRootUserMessageId: 'msg_root_a',
    AuthorityKind: 'HumanRoot',
    IdentitySeed: {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: { participant: 'engineer', role: 'engineer', persona: 'Engineer', personaCatalogVersion: 1, origin: 'ResolvedAtRoot' },
    },
  },
}
const env = (overrides = {}) => ({
  runtime: 'rt_a',
  seq: 1,
  observedAt: '2026-01-02T03:04:05Z',
  id: 'a'.repeat(32),
  stream: { kind: 'Session', id: SESSION },
  providerRun: null,
  fact: ROOT_ACCEPTED,
  ...overrides,
})
const readEnvelope = (value) => ({
  runtime: value.runtime,
  seq: Number(value.seq),
  event: value.id,
  stream: value.stream,
  providerRun: value.providerRun,
  fact: value.fact.case,
})
const mustOk = (result, label = 'result') => {
  assert.equal(result.ok, true, `${label} should be Ok: ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[durable-events-016] durable fact vocabulary has no OpenCode infrastructure dependency', () => {
  const readSource = (path) => readFileSync(new URL(`../../../${path}`, import.meta.url), 'utf8')
  const envelope = readSource('src/Wanxiangshu/Persistence/Journal/Envelope.fs')
  const fact = readSource('src/Wanxiangshu/Composition/Durable/Fact.fs')
  const hostFacts = readSource('src/Wanxiangshu/Host/Facts.fs')

  assert.doesNotMatch(envelope, /Wanxiangshu\.OpenCode/)
  assert.doesNotMatch(fact, /Wanxiangshu\.OpenCode/)
  assert.match(hostFacts, /namespace Wanxiangshu\.Host/)
  assert.doesNotMatch(hostFacts, /Wanxiangshu\.OpenCode/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const eventCodec = await import("../../../dist/Persistence/EventStore/CodecSurface.js");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");

const A = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
const envelope = ({
  id = A,
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
  payloadRefs = [],
} = {}) => ({
  id,
  stream,
  type: eventType,
  parents,
  payload,
  payloadRefs,
})

test('WHAT[durable-events-016] Git_contract_exposes_canonical_store_ref', () => {
  assert.equal(eventStore.canonicalStoreRef, 'refs/wanxiang/store')
})
}


