import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const toolHost = await import("../../../dist/OpenCode/Codec/ToolHostSurface.js");

const malformedString = fc.anything({ withBoxedValues: true }).filter(value => typeof value !== 'string')

test('WHAT[host-boundary-030] malformed Host values never gain authority or throw', () => {
  fc.assert(fc.property(malformedString, value => {
    assert.doesNotThrow(() => toolHost.contextDecode({ sessionID: value, agent: value, callID: value, messageID: value }))
    assert.deepEqual(toolHost.contextView(toolHost.contextDecode({ sessionID: value, agent: value })), {
      sessionId: '',
      agent: null,
      toolCallId: null,
      providerRunId: null,
      promptText: null,
    })
    assert.doesNotThrow(() => toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: value, agent: value } }))
    assert.equal(toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: value, agent: value } }), null)
    assert.equal(toolHost.sessionAgent({ agent: value }), null)
  }), { seed: 43030, numRuns: 180 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toolHost = await import("../../../dist/OpenCode/Codec/ToolHostSurface.js");


test('WHAT[host-boundary-030] raw Host booleans arrays identities and agents reject coercion', () => {
  const malformed = toolHost.contextView(toolHost.contextDecode({
    sessionID: 7,
    agent: { toString: () => 'agent' },
    callID: ['call'],
    messageID: true,
    prompt: new String('prompt'),
  }))

  assert.deepEqual(malformed, {
    sessionId: '',
    agent: null,
    toolCallId: null,
    providerRunId: null,
    promptText: null,
  })
})
test('WHAT[host-boundary-030] optional Host arguments distinguish absence from malformed values', () => {
  for (const value of [null, undefined]) {
    const argumentsHandle = toolHost.makeArguments({ expected_tool_calls: value })
    assert.deepEqual(toolHost.argumentOptionalNonNegativeInteger(argumentsHandle, 'expected_tool_calls'), { ok: true, value: null })
  }

  for (const value of ['1', true, {}, [], new Number(1)]) {
    const argumentsHandle = toolHost.makeArguments({ expected_tool_calls: value })
    assert.deepEqual(toolHost.argumentOptionalNonNegativeInteger(argumentsHandle, 'expected_tool_calls'), { ok: false })
  }
})
test('WHAT[host-boundary-030] Host session observations and session.get agents reject coercion', () => {
  assert.deepEqual(
    toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: 's1', info: { parentID: 'p1', agent: 'agent-a' } } }),
    { sessionId: 's1', hasParent: true, agent: 'agent-a' },
  )
  assert.equal(toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: 7, agent: 'agent-a' } }), null)
  assert.deepEqual(
    toolHost.sessionObservation({ type: 'session.updated', properties: { sessionID: 's1', info: { parentID: 7, agent: {} } } }),
    { sessionId: 's1', hasParent: false, agent: null },
  )
  assert.equal(toolHost.sessionAgent({ agent: 7 }), null)
  assert.equal(toolHost.sessionAgent({ agent: { toString: () => 'agent-a' } }), null)
  assert.equal(toolHost.sessionAgent({ agent: ' agent-a ' }), ' agent-a ')
  assert.equal(toolHost.sessionAgent({ data: { agent: 'wrapped-agent' } }), 'wrapped-agent')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");
const HostSignalSurface = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");
const { assertEffectIsInjected, assertFatalBoundary, assertOptionalObservationNoninterference, assertPureContract } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const requireShard = (projects, shardId) => {
  const matches = [...projects.values()].filter((candidate) => candidate.shard === shardId)
  assert.equal(matches.length, 1, `${shardId} must resolve to exactly one production compile shard`)
  return matches[0]
}
const relSources = (project) => project.implementationFiles.map((p) => path.relative(ROOT, p)).sort()
const refShards = (project, projects) => project.references.map((refPath) => projects.get(refPath).shard).sort()
const closureSources = (root, projects) => {
  const closure = new Set()
  const pending = [root]
  while (pending.length > 0) {
    const project = pending.pop()
    if (closure.has(project)) continue
    closure.add(project)
    for (const refPath of project.references) {
      pending.push(projects.get(refPath))
    }
  }
  return new Set([...closure].flatMap(relSources))
}

test('WHAT[host-boundary-030] Host envelope rejects adjacent malformed event and session carriers without throwing', () => {
  for (const value of ['', ' ', 7, true, {}, [], new String('session-1')]) {
    const raw = { type: value, properties: { sessionID: value, sessionId: value, info: { sessionID: value } }, sessionID: value, sessionId: value }
    assert.doesNotThrow(() => HostSignalSurface.envelopeEventType(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeSessionId(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeMessageSessionId(raw))
    assert.equal(HostSignalSurface.envelopeEventType(raw), '')
    assert.equal(HostSignalSurface.envelopeSessionId(raw), null)
    assert.equal(HostSignalSurface.envelopeMessageSessionId(raw), null)
  }

  for (const raw of [null, 7, true, 'session-1', [], {}]) {
    assert.doesNotThrow(() => HostSignalSurface.unwrapEnvelope(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeEventType(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeSessionId(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeMessageSessionId(raw))
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { contextAttachAbort, contextDecode } = await import("../../../dist/OpenCode/Codec/ToolHostSurface.js");


test('WHAT[host-boundary-030] abort marker accepts primitive boolean true only', () => {
  for (const aborted of [1, 'true', {}, [], new Boolean(true)]) {
    let calls = 0
    contextAttachAbort(contextDecode({
      sessionID: 'strict-abort',
      abort: { aborted, addEventListener() {}, removeEventListener() {} },
    }), () => { calls += 1 })
    assert.equal(calls, 0)
  }
})
}
