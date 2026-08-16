// Assistance host owns its quiescence gate and recovery state; semantic tests
// consume source laws and the existing QuiescenceSurface only.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

const assistance = readFileSync(new URL('../../../src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs', import.meta.url), 'utf8')
const sensor = readFileSync(new URL('../../../src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-018] ASSISTANCE_HOST_never_starts_repair_while_turn_is_active', () => {
  assert.match(assistance, /Quiescence|beginAttempt|TurnActive/i)
  assert.match(quiescence.inspect('ses-assist', 'empty'), /quiet|quiescent|available/i)
})
test('WHAT[DELEG-018] ASSISTANCE_HOST_need_help_sensor_has_explicit_signal', () => {
  assert.match(sensor, /NeedHelp|needHelp|signal/i)
})
test('WHAT[DELEG-018] ASSISTANCE_HOST_recovery_is_observable_not_silent', () => {
  assert.match(assistance, /Recover|recovery/i)
  assert.doesNotMatch(assistance, /ignore|swallow/i)
})
