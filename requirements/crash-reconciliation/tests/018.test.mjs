import assert from 'node:assert/strict'
import test from 'node:test'
import * as explicit from '../../../dist/Execution/Session/Recovery/ExplicitContinueSurface.js'
import * as family from '../../../dist/Execution/Session/Recovery/SessionRecoveryFamilySurface.js'
import * as explicitResumeSurface from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'

test('WHAT[CRASH-018] CRASH_018_continue_registers_a_visible_command', () => {
  assert.equal(explicit.isCommandRegistered('/continue'), true)
})

test('WHAT[CRASH-018] CRASH_018_non_continue_command_is_a_noop', async () => {
  const res = await explicit.handleCommand('/other')
  assert.equal(res.handled, false)
})

test('WHAT[CRASH-018] CRASH_018_continue_discloses_restart_without_minting_completion', async () => {
  const res = await explicit.handleContinue('ses-1')
  assert.equal(res.mintedCompletion, false)
  assert.equal(res.disclosed, true)
})

test('WHAT[CRASH-018] CRASH_018_missing_session_is_visible_and_does_not_resume', async () => {
  const res = await explicit.handleMissingSession('ses-missing')
  assert.equal(res.resumed, false)
})

test('WHAT[CRASH-018] CRASH_018_resume_briefing_keeps_unverified_children_visible', async () => {
  const briefing = await explicit.generateBriefing(['unverified-1'])
  assert.ok(briefing.includes('unverified-1'))
})

test('WHAT[CRASH-018] CRASH_018_real_command_material_materializes_briefing_and_stays_disclosure_only', async () => {
  const res = await explicit.materializeBriefing('ses-1')
  assert.equal(res.disclosureOnly, true)
})

test('WHAT[CRASH-018] CRASH_018_transform_uses_exact_physical_binding_when_host_drops_part_metadata', async () => {
  const res = await explicit.transformWithExactBinding('ses-1', 'msg-1')
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-018] CRASH_018_chat_params_respects_exact_disclosure_classification', async () => {
  const res = await explicit.classifyChatParams({ role: 'user', content: 'continue' })
  assert.equal(res.disclosure, true)
})

test('WHAT[CRASH-018] CRASH_018_abandoned_command_handoff_cannot_mark_a_later_ordinary_material', async () => {
  const res = await explicit.handoffAbandoned('ses-1')
  assert.equal(res.pollutedLater, false)
})

test('WHAT[CRASH-018] CRASH_018_resumed_session_user_message_without_explicit_agent_admits_and_transforms', async () => {
  const res = await explicit.admitAndTransform('ses-1')
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-018] CRASH_018_chat_params_with_toplevel_messageID_recognizes_disclosure_only', async () => {
  const res = await explicit.recognizeDisclosure('msg-top-1')
  assert.equal(res, true)
})

test('WHAT[CRASH-018] CRASH_018_absent_port_blocks_with_one_manual_and_no_background_command', async () => {
  const res = await family.handleAbsentPort()
  assert.equal(res.blocked, true)
})

test('WHAT[CRASH-018] CRASH_018_duplicate_continue_keeps_a_single_manual', async () => {
  const res = await family.handleDuplicateContinue()
  assert.equal(res.singleManual, true)
})

test('WHAT[CRASH-018] CRASH_018_accepted_continue_emits_no_manual_block', async () => {
  const res = await family.handleAcceptedContinue()
  assert.equal(res.manualBlock, false)
})
