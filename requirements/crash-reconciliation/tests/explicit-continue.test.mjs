import assert from 'node:assert/strict'
import test from 'node:test'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'

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
