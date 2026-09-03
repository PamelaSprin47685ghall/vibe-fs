// requirements/capability-enforcement/tests/integration/plugin/auto-injected-tool.test.mjs — HOST-013: universal cursor mode.
//
// Layer 3: `skill` is not plugin-owned; legacy `auto-injected` is not a real Tool.Def.
// Zero synthetic skill messages on every provider; guidance travels only as a
// NUL+BOM suffix on the terminal real tool result.

import assert from 'node:assert/strict'
import test from 'node:test'
import { markerSource } from '../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const withSession = (messages, sessionID = 'coder-auto-injected') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))

test('WHAT[ENF-006] HOST_013_skill_stays_host_owned_and_legacy_marker_is_not_plugin_registered', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'coder-auto-injected', 'coder')
    assert.equal(hooks.tool['auto-injected'], undefined, 'legacy auto-injected must not be in hooks.tool')
    assert.equal(hooks.tool.skill, undefined, 'skill remains Host-owned rather than plugin-registered')

    const transformed = {
      messages: withSession([
        { role: 'user', info: { id: 'root-coder-auto-injected' }, parts: [{ type: 'text', text: 'start' }] },
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
