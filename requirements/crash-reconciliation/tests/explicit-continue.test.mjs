import assert from 'node:assert/strict'
import test from 'node:test'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[CRASH-018] CRASH_018_continue_registers_a_visible_command', () => {
  const config = {}
  resume.registerCommand(config)
  assert.equal(typeof config.command.continue.template, 'string')
  assert.match(config.command.continue.template, /explicitly requested session continuation/i)
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

test('WHAT[CRASH-018] CRASH_018_marked_continue_material_suppresses_the_old_active_root_on_idle', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_continue_exact_material'
    const oldRootID = `root-${sessionID}`
    const continueID = 'msg-continue-exact'

    // Durable history survives restart, while the new process intentionally has
    // no SessionExecutionBinding user-facing agent observation yet.
    await acceptAuthorityRoot(runtime, sessionID, 'fast-coder')

    const commandOutput = { parts: [] }
    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )
    assert.equal(commandOutput.parts[0].metadata?.wanxiangshu_explicit_resume, true)

    // /continue is not a new AuthorityRoot and has no PromptKey. chat.message
    // must still bind it as the exact physical material reconciliation follows.
    await hooks['chat.message'](
      { sessionID, messageID: continueID },
      {
        message: { id: continueID, sessionID, role: 'user' },
        parts: commandOutput.parts,
      },
    )

    const oldUser = {
      info: { id: oldRootID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: 'old interrupted work' }],
    }
    const continueUser = {
      info: { id: continueID, sessionID, role: 'user' },
      parts: commandOutput.parts,
    }

    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      { messages: [oldUser, continueUser] },
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
