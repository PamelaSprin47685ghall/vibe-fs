import assert from 'node:assert/strict'
import test from 'node:test'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[CRASH-018] CRASH_018_continue_registers_a_visible_command', () => {
  const config = {}
  resume.registerCommand(config)
  assert.equal(typeof config.command.continue.template, 'string')
  assert.match(config.command.continue.template, /explicitly requested session continuation/i)
  assert.match(config.command.continue.template, /same user material/i)
  assert.doesNotMatch(config.command.continue.template, /briefing attached to this command/i)
  assert.match(config.command.continue.description, /resume this session/i)
})

test('WHAT[CRASH-018] CRASH_018_non_continue_command_is_a_noop', async () => {
  const actual = await resume.run('status', 'session-1', '')
  assert.deepEqual(actual.parts, [])
})

test('WHAT[CRASH-018] CRASH_018_continue_discloses_restart_without_minting_completion', async () => {
  const output = await resume.run('/continue', 'session-1', 'reuse child')
  assert.equal(output.parts.length, 1)
  assert.equal(output.parts[0].type, 'text')
  assert.match(output.parts[0].text, /restart briefing/)
  assert.match(output.parts[0].text, /interrupted\/failed/i)
  assert.match(output.parts[0].text, /User \/continue arguments: reuse child/)
  assert.match(output.parts[0].text, /Do not infer that it completed|do not manufacture a terminal result/i)
})

test('WHAT[CRASH-018] CRASH_018_missing_session_is_visible_and_does_not_resume', async () => {
  const output = await resume.run('continue', '', '')
  assert.equal(output.parts.length, 1)
  assert.match(output.parts[0].text, /no session id was supplied/i)
  assert.match(output.parts[0].text, /previous interrupted tool remains failed/i)
})

test('WHAT[CRASH-018] CRASH_018_resume_briefing_keeps_unverified_children_visible', async () => {
  const output = await resume.run('continue', 'session-1', '')
  assert.match(output.parts[0].text, /Surviving sub sessions re-enlisted process-locally/i)
  assert.match(output.parts[0].text, /Durable children that were not re-enlisted/i)
  assert.match(output.parts[0].text, /- none/)
})

test('WHAT[CRASH-018] CRASH_018_real_command_material_materializes_briefing_and_stays_disclosure_only', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_continue_exact_material'
    const oldRootID = `root-${sessionID}`
    const continueID = 'msg-continue-exact'

    // Durable history survives restart, while the new process intentionally has
    // no SessionExecutionBinding user-facing agent observation yet.
    await acceptAuthorityRoot(runtime, sessionID, 'coder')

    const commandOutput = { parts: [] }
    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )
    assert.equal(commandOutput.parts[0].metadata?.wanxiangshu_explicit_resume, true)

    // Real OpenCode does not promise that command.execute.before output.parts are
    // copied into the physical user message. Reproduce that boundary: chat.message
    // receives only the command template and production must materialize the
    // staged dynamic briefing itself. The test must not hand-forward commandOutput.
    const config = {}
    resume.registerCommand(config)
    const physicalOutput = {
      message: { id: continueID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: config.command.continue.template }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: continueID },
      physicalOutput,
    )

    const materialized = physicalOutput.parts.filter(
      (part) => part.metadata?.wanxiangshu_explicit_resume === true,
    )
    assert.equal(materialized.length, 1, 'chat.message must materialize the pending briefing exactly once')
    assert.match(materialized[0].text, /\[wanxiangshu restart briefing\]/)

    const oldUser = {
      info: { id: oldRootID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: 'old interrupted work' }],
    }
    const continueUser = {
      info: { id: continueID, sessionID, role: 'user' },
      parts: physicalOutput.parts,
    }

    const providerOutput = { messages: [oldUser, continueUser] }
    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      providerOutput,
    )

    assert.deepEqual(
      providerOutput.messages,
      [
        oldUser,
        {
          info: { role: 'assistant' },
          role: 'assistant',
          parts: [{ type: 'text', text: '.' }],
        },
        continueUser,
      ],
      '/continue exact material must bypass ordinary semantic message transforms',
    )

    runtime.pushHostMessage(sessionID, oldUser)
    runtime.pushHostMessage(sessionID, {
      info: {
        id: 'asst-old-needs-repair',
        sessionID,
        role: 'assistant',
        parentID: oldRootID,
        finish: 'tool-calls',
        time: { created: 1, completed: 2 },
      },
      parts: [],
    })
    runtime.pushHostMessage(sessionID, continueUser)

    const promptCount = runtime.prompts.length
    hooks.event({ event: { type: 'session.idle', properties: { sessionID } } })
    await hooks.dispose()

    assert.equal(
      runtime.prompts.length,
      promptCount,
      '/continue disclosure must not repair the previous active root or emit any detached prompt',
    )
  })
})

test('WHAT[CRASH-018] CRASH_018_transform_uses_exact_physical_binding_when_host_drops_part_metadata', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses_continue_exact_binding'
    const continueID = 'msg-continue-exact-binding'

    await acceptAuthorityRoot(runtime, sessionID, 'coder')

    const commandOutput = { parts: [] }
    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )

    const config = {}
    resume.registerCommand(config)
    const physicalOutput = {
      message: { id: continueID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: config.command.continue.template }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: continueID },
      physicalOutput,
    )

    assert.equal(
      physicalOutput.parts.some((part) => part.metadata?.wanxiangshu_explicit_resume === true),
      true,
      'chat.message must bind the exact physical material before provider transform',
    )

    // Some Host projections do not preserve custom part metadata between
    // chat.message and messages.transform. Keep the exact physical id but strip
    // the marker to prove the process-local binding, not metadata survival, owns
    // suppression at this boundary.
    const providerOutput = {
      messages: [
        {
          info: { id: continueID, sessionID, role: 'user' },
          parts: [{ type: 'text', text: config.command.continue.template }],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      providerOutput,
    )

    assert.equal(
      createdIds.length,
      0,
      'exact /continue binding must prevent ordinary Companion/Strength child creation when marker metadata is absent',
    )
    assert.equal(runtime.prompts.length, 0)
    assert.equal(providerOutput.messages.length, 1)
    assert.equal(providerOutput.messages[0].info.id, continueID)
  })
})

test('WHAT[CRASH-018] CRASH_018_chat_params_respects_exact_disclosure_classification', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_continue_chat_params'
    const continueID = 'msg-continue-chat-params'

    await acceptAuthorityRoot(runtime, sessionID, 'manager')

    const commandOutput = { parts: [] }
    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )

    const config = {}
    resume.registerCommand(config)
    const physicalOutput = {
      message: { id: continueID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: config.command.continue.template }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: continueID },
      physicalOutput,
    )

    const paramsOutput = { options: {} }
    await hooks['chat.params'](
      {
        sessionID,
        agent: 'manager',
        model: {
          id: 'host-default-model',
          providerID: 'fixture',
          capabilities: { temperature: true },
          options: {},
        },
        provider: {},
        message: {
          id: continueID,
          sessionID,
          role: 'user',
          model: { providerID: 'fixture', modelID: 'host-default-model' },
        },
      },
      paramsOutput,
    )

    assert.equal(
      paramsOutput.temperature,
      undefined,
      'disclosure-only /continue must not inherit managed execution temperature policy',
    )
    assert.equal(paramsOutput.options.temperature, undefined)
  })
})

test('WHAT[CRASH-018] CRASH_018_abandoned_command_handoff_cannot_mark_a_later_ordinary_material', async () => {
  await withExecutablePlugin(async (hooks) => {
    const sessionID = 'ses_continue_handoff_superseded'
    const commandOutput = { parts: [] }

    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )
    assert.equal(commandOutput.parts[0].metadata?.wanxiangshu_explicit_resume, true)

    const ordinaryID = 'msg-after-abandoned-continue'
    const ordinaryOutput = {
      message: { id: ordinaryID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: 'ordinary user work after the command was abandoned' }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: ordinaryID },
      ordinaryOutput,
    )

    assert.equal(
      ordinaryOutput.parts.some((part) => part.metadata?.wanxiangshu_explicit_resume === true),
      false,
      'a stale command handoff must be discarded rather than attached to a newer ordinary material',
    )
  })
})

test('WHAT[CRASH-018] CRASH_018_resumed_session_user_message_without_explicit_agent_admits_and_transforms', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_resumed_user_continue'
    const userMessageID = 'msg-user-continue-1'

    await acceptAuthorityRoot(runtime, sessionID, 'manager')

    const userOutput = {
      message: { id: userMessageID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: '继续' }],
    }

    // Host sends chat.message with only sessionID (no explicit agent in user message)
    await hooks['chat.message'](
      { sessionID, messageID: userMessageID },
      userOutput,
    )

    // User message should have managed model routed onto output.message.model
    assert.ok(userOutput.message.model, 'user message must receive routed model')
    assert.ok(userOutput.message.model.providerID, 'routed model must have providerID')
    assert.ok(userOutput.message.model.modelID, 'routed model must have modelID')

    const transformOutput = {
      messages: [
        {
          info: { id: userMessageID, sessionID, role: 'user' },
          parts: [{ type: 'text', text: '继续' }],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      transformOutput,
    )

    assert.ok(transformOutput.messages.length > 0, 'transform must succeed without error')
  })
})

test('WHAT[CRASH-018] CRASH_018_chat_params_with_toplevel_messageID_recognizes_disclosure_only', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_continue_toplevel_messageid'
    const continueID = 'msg-continue-toplevel-id'

    await acceptAuthorityRoot(runtime, sessionID, 'manager')

    const commandOutput = { parts: [] }
    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )

    const config = {}
    resume.registerCommand(config)
    const physicalOutput = {
      message: { id: continueID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: config.command.continue.template }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: continueID },
      physicalOutput,
    )

    const paramsOutput = { options: {} }
    // Host sends input with top-level messageID and no nested message.id
    await hooks['chat.params'](
      {
        sessionID,
        messageID: continueID,
        agent: 'manager',
        model: {
          id: 'host-default-model',
          providerID: 'fixture',
          capabilities: { temperature: true },
          options: {},
        },
        provider: {},
      },
      paramsOutput,
    )

    assert.equal(
      paramsOutput.temperature,
      undefined,
      'disclosure-only /continue with top-level messageID must bypass managed execution temperature policy without throwing',
    )
  })
})
