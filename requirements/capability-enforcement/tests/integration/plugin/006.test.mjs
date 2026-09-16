import assert from 'node:assert/strict'
import test from 'node:test'
import {
  markerSource,
  markerToolName,
} from '../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import { rolePredicate } from '../../../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
  withPlugin,
} from '../../../../verification-system/tests/support/plugin-fixture.mjs'

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
      agent: 'coder',
      model: { providerID: 'host', modelID: 'placeholder' },
    },
    parts: [],
  }
  await hooks['chat.message']({ sessionID, agent: 'coder' }, output)
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

test('WHAT[ENF-006] AUTOINJ_tryInject_rewrites_active_call_without_synthetic_injection', async () => {
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
    assert.equal(synthetic, undefined, 'zero-synthetic mode must not inject a synthetic skill row')
  })
})

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
      ], 'coder-auto-injected'),
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

test('WHAT[ENF-006] MANAGER_pair_guidance_rides_cursor_suffix_without_synthetic_skill_row', async () => {
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
      ], 'ses-capability-manager'),
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
