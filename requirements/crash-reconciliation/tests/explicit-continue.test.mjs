// Explicit /continue is a material-scoped disclosure and a no-write resume
// observation. Source assertions stay at the Host owner; no Fable internals cross.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

const source = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Host/ExplicitSessionResume.fs', import.meta.url), 'utf8')

test('WHAT[CRASH-018] CRASH_018_continue_suppression_belongs_to_the_exact_user_material_not_the_session', () => {
  assert.match(source, /ExplicitResumeSuppression/)
  assert.match(source, /physical|Physical/i)
  assert.match(source, /ordinary|clear/i)
})
test('WHAT[CRASH-018] CRASH_018_continue_provider_turn_never_mints_missing_final_report_nudge', () => {
  assert.doesNotMatch(source, /MissingFinalReport|InteractionRepair/) 
})
test('WHAT[CRASH-018] CRASH_018_exact_physical_resume_suppression_clears_on_next_ordinary_material_without_lifecycle_signal', () => {
  assert.match(source, /observe/)
  assert.match(source, /isPhysical/)
})
test('WHAT[CRASH-018] CRASH_018_config_registers_visible_continue_command', () => {
  assert.match(source, /registerCommand/)
  assert.match(source, /explicitly requested session continuation/i)
})
test('WHAT[CRASH-018] CRASH_018_non_continue_command_is_a_noop', () => {
  assert.match(source, /command <> "continue"|command <> "\/continue"/) 
})
test('WHAT[CRASH-018] CRASH_018_continue_discloses_restart_keeps_broken_tool_visible_and_process_locally_reenlists_survivor', () => {
  assert.match(source, /snapshot-port-unavailable|interrupted|failed/i)
  assert.doesNotMatch(source, /appendAgent|recordCompletion|HandleRetired/) 
})
test('WHAT[CRASH-017] CRASH_017_new_process_runtime_dispose_does_not_claim_or_abort_old_active_handle', () => {
  assert.doesNotMatch(source, /AbortSession|recordAbandon/) 
})
test('WHAT[CRASH-018] CRASH_018_missing_snapshot_is_visible_and_does_not_adopt_or_fail_future_use', () => {
  assert.match(source, /snapshot-port-unavailable/)
  assert.doesNotMatch(source, /adoptExistingChild/) 
})
