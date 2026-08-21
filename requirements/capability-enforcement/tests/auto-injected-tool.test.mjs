// HOST-013: the pair hint borrows the Host-owned skill wire with an empty name.
// `skill` stays real and usable for non-empty names; only active empty-name loads
// are reserved for synthetic injection and rewritten to DENIED.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  markerSource,
  markerToolName,
} from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { withExecutablePlugin, withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const withSession = (messages, sessionID = 'ses-auto-injected') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))

const admitManagedRoot = async (hooks, sessionID = 'ses-auto-injected') => {
  const output = {
    message: {
      id: `root-${sessionID}`,
      role: 'user',
      sessionID,
      agent: 'fast-coder',
      model: { providerID: 'host', modelID: 'placeholder' },
    },
    parts: [],
  }
  await hooks['chat.message']({ sessionID, agent: 'fast-coder' }, output)
}

test('WHAT[ENF-006] AUTOINJ_skill_wire_stays_host_owned_and_is_not_plugin_registered', async () => {
  assert.equal(markerToolName, 'skill')
  assert.equal(rolePredicate('skill', 'coder'), false, 'Host-owned skill is not a plugin role tool')
  assert.equal(rolePredicate('skill', 'manager'), false)
  assert.equal(rolePredicate('skill', 'blogger'), false)

  await withPlugin(async (hooks) => {
    assert.equal(hooks.tool['auto-injected'], undefined, 'legacy auto-injected must not be in hooks.tool')
    assert.equal(hooks.tool.skill, undefined, 'skill remains Host-owned rather than plugin-registered')
  })
})

test('WHAT[ENF-006] AUTOINJ_active_empty_skill_call_is_denied_without_touching_real_skill_names', async () => {
  await withExecutablePlugin(async (hooks) => {
    await admitManagedRoot(hooks)
    const transformed = {
      messages: withSession([
        {
          role: 'assistant',
          info: { id: 'asst-empty-skill' },
          parts: [{
            type: 'tool',
            tool: 'skill',
            callID: 'call-empty',
            state: { status: 'error', input: { name: '' }, error: 'Skill not found' },
          }],
        },
        {
          role: 'assistant',
          info: { id: 'asst-real-skill' },
          parts: [{
            type: 'tool',
            tool: 'skill',
            callID: 'call-real',
            state: { status: 'completed', input: { name: 'pdfs' }, output: 'real skill output' },
          }],
        },
        {
          role: 'user',
          info: { id: 'root-ses-auto-injected' },
          parts: [{ type: 'text', text: 'hello' }],
        },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    const rewritten = transformed.messages.find((message) => message.info?.id === 'asst-empty-skill')
    assert.ok(rewritten)
    const part = rewritten.parts[0]
    assert.equal(part.state.status, 'completed', 'empty-name skill failure must be rewritten to completed')
    assert.equal(part.state.error, undefined, 'error field must be cleared')
    assert.match(part.state.output, /DENIED|禁止/, 'result must contain denial text')
    assert.match(part.state.output, /skill/, 'denial must identify the reserved empty-name skill load')

    const real = transformed.messages.find((message) => message.info?.id === 'asst-real-skill')
    assert.ok(real)
    assert.deepEqual(real.parts[0].state.input, { name: 'pdfs' })
    assert.equal(real.parts[0].state.output, 'real skill output')
  })
})

test('WHAT[ENF-006] AUTOINJ_tryInject_rewrites_active_call_while_preserving_synthetic_injection', async () => {
  await withExecutablePlugin(async (hooks) => {
    await admitManagedRoot(hooks)
    const transformed = {
      messages: withSession([
        {
          role: 'assistant',
          info: { id: 'asst-1' },
          parts: [
            {
              type: 'tool',
              tool: 'skill',
              callID: 'call-active',
              state: { status: 'error', input: { name: '' }, error: 'Skill not found' },
            },
          ],
        },
        {
          role: 'user',
          info: { id: 'root-ses-auto-injected' },
          parts: [{ type: 'text', text: 'hello' }],
        },
      ]),
    }

    await hooks['experimental.chat.messages.transform']({}, transformed)
    const rewrittenActive = transformed.messages.find((message) => message.info?.id === 'asst-1')
    assert.ok(rewrittenActive)
    assert.equal(rewrittenActive.parts[0].state.status, 'completed')
    assert.match(rewrittenActive.parts[0].state.output, /DENIED/)

    const synthetic = transformed.messages.find(
      (message) => message.info?.source === markerSource,
    )
    assert.ok(synthetic)
    assert.equal(synthetic.parts[0].tool, 'skill')
    assert.deepEqual(synthetic.parts[0].state.input, { name: '' })
    assert.equal(synthetic.parts[0].state.status, 'completed')
    assert.equal(typeof synthetic.parts[0].state.output, 'string')
    assert.match(synthetic.parts[0].state.output, /^# /)
    assert.doesNotMatch(synthetic.parts[0].state.output, /<skill_content|<\/skill_content>/)
  })
})
