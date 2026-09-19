import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { markerSource } = await import("../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");
const { withExecutablePlugin, acceptAuthorityRoot } = await import("../../../../verification-system/tests/support/plugin-fixture.mjs");

const withSession = (messages, sessionID = 'engineer-auto-injected') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))

test('WHAT[capability-enforcement-006] HOST_013_skill_stays_host_owned_and_legacy_marker_is_not_plugin_registered', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-auto-injected', 'engineer')
    assert.equal(hooks.tool['auto-injected'], undefined, 'legacy auto-injected must not be in hooks.tool')
    assert.equal(hooks.tool.skill, undefined, 'skill remains Host-owned rather than plugin-registered')

    const transformed = {
      messages: withSession([
        { role: 'user', info: { id: 'root-engineer-auto-injected' }, parts: [{ type: 'text', text: 'start' }] },
        { role: 'assistant', info: { id: 'c1' }, parts: [{ type: 'tool', tool: 'read', callID: 't1', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
        { role: 'assistant', info: { id: 'r1' }, parts: [{ type: 'tool', tool: 'read', callID: 't1', state: { status: 'completed', input: {}, output: 'ok1', time: { start: 0, end: 0 } } }] },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    const synthetic = transformed.messages.find((message) => message.info?.source === markerSource)
    assert.equal(synthetic, undefined, 'zero-synthetic mode must not inject a synthetic skill row')
    const terminal = transformed.messages.find((message) => message.info?.id === 'r1')
    assert.ok(terminal, 'terminal real tool result survives the transform')
    const output = terminal.parts?.[0]?.state?.output ?? ''
    assert.ok(output.startsWith('ok1\0\uFEFF'), 'guidance travels as NUL+BOM suffix on the terminal real tool result')
    assert.match(output, /#/, 'suffix carries guidance bytes')
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { markerSource, markerToolName } = await import("../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");
const { permissions } = await import("../../../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin, withPlugin } = await import("../../../../verification-system/tests/support/plugin-fixture.mjs");

const TOOL_NAMES = [
  'fork', 'resume', 'commission', 'join', 'horizon', 'todowrite', 'fission',
  'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm',
  'bash-honeypot', 'assume',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'run', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'review', 'chronicle', 'fetch', 'suicide',
]
const PLUGIN_TOOL_NAMES = [
  'fork', 'resume', 'commission', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'join', 'horizon', 'fission', 'review', 'suicide', 'run',
  'mv', 'rm', 'bash-honeypot', 'assume', 'chronicle',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'js-engineer', 'js-devops',
]
const HOST_OWNED_TOOL_NAMES = [
  'todowrite', 'read', 'write', 'edit', 'glob', 'grep', 'skill',
]
const ROLE_NAMES = ['orchestrator', 'manager', 'engineer', 'devops', 'blogger']
const COGNITIVE_TOOLS = ['enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']
const ALLOWED = {
  orchestrator: ['commission', 'join', 'horizon', 'assume', ...COGNITIVE_TOOLS],
  manager: ['fork', 'resume', 'join', 'horizon', 'todowrite', 'review', 'suicide', 'assume', ...COGNITIVE_TOOLS],
  engineer: ['fission', 'read', 'write', 'edit', 'glob', 'grep', 'fetch', 'mv', 'rm', 'bash-honeypot', 'assume', ...COGNITIVE_TOOLS],
  devops: [
    'join', 'horizon', 'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm', 'run',
    'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
    'assume', ...COGNITIVE_TOOLS,
  ],
  blogger: ['chronicle'],
}
const withSession = (messages, sessionID = 'ses-capability-manager') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))
const fullConfig = () => ({
  agent: Object.fromEntries(
    ROLE_NAMES.map((role) => [role, {}]),
  ),
})

test('WHAT[capability-enforcement-006] MANAGER_pair_guidance_rides_cursor_suffix_without_synthetic_skill_row', async () => {
  assert.equal(markerToolName, 'skill')
  assert.equal(typeof markerSource, 'string')
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-capability-manager', 'manager')
    assert.equal(hooks.tool.skill, undefined, 'skill remains Host-owned')
    assert.equal(hooks.tool['auto-injected'], undefined, 'legacy auto-injected must not be plugin-registered')
    const transformed = {
      messages: withSession([
        { role: 'user', info: { id: 'root-ses-capability-manager' }, parts: [{ type: 'text', text: 'start' }] },
        { role: 'assistant', info: { id: 'c1' }, parts: [{ type: 'tool', tool: 'read', callID: 't1', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
        { role: 'assistant', info: { id: 'r1' }, parts: [{ type: 'tool', tool: 'read', callID: 't1', state: { status: 'completed', input: {}, output: 'ok1', time: { start: 0, end: 0 } } }] },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    const synthetic = transformed.messages.find((message) => message.info?.source === markerSource)
    assert.equal(synthetic, undefined, 'zero-synthetic mode must not inject a synthetic skill row')
    const terminal = transformed.messages.find((message) => message.info?.id === 'r1')
    assert.ok(terminal, 'terminal real tool result survives the transform')
    const output = terminal.parts?.[0]?.state?.output ?? ''
    assert.ok(output.startsWith('ok1\0\uFEFF'), 'guidance travels as NUL+BOM suffix on the terminal real tool result')
    assert.match(output, /#/, 'suffix carries guidance bytes')
    assert.equal(hooks.tool[markerToolName], undefined, 'cursor suffix borrows no plugin-registered skill name')
  })
})
}
