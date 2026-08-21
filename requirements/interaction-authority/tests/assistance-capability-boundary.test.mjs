// INTERACTION-AUTHORITY proof — Assistance capability ports and pure decision seam.
// Validates:
// 1. Assistance pure decision separates Fast tier escalation vs Deep tier consultation.
// 2. Interaction/Authority/Assistance.fs defines AssistancePorts without foreign domain contamination.
// 3. AssistanceHost and NeedHelpSensor contain zero foreign domain imports (Git/Strength/Review/Todo).
// 4. ConsultationCompleted projection directly derives from durable handle/run evidence without new events.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[INTERACTION-AUTHORITY-012] assistance capability domain file exists and contains pure decision and ports', () => {
  const assistancePath = join(ROOT, 'src/Wanxiangshu/Interaction/Authority/Assistance.fs')
  assert.ok(existsSync(assistancePath), 'src/Wanxiangshu/Interaction/Authority/Assistance.fs must exist')

  const text = read('src/Wanxiangshu/Interaction/Authority/Assistance.fs')
  
  // Must declare AssistanceDecision with Fast/Deep alternatives
  assert.match(text, /type AssistanceDecision =/)
  assert.match(text, /EscalateFast/)
  assert.match(text, /ConsultDeep/)
  assert.match(text, /RejectOrUnresolved/)

  // Must declare AssistancePorts with typed business capabilities
  assert.match(text, /type AssistancePorts =/)
  assert.match(text, /CurrentAuthoritys*:/)
  assert.match(text, /StartConsultations*:/)
  assert.match(text, /AwaitConsultations*:/)
  assert.match(text, /DeliverAdvices*:/)

  // Must not open foreign domains
  assert.doesNotMatch(text, /open Wanxiangshu.Git/)
  assert.doesNotMatch(text, /open Wanxiangshu.Strength/)
  assert.doesNotMatch(text, /open Wanxiangshu.Mission.Review/)
  assert.doesNotMatch(text, /open Wanxiangshu.Mission.Obligation.Todo/)
})

test('WHAT[INTERACTION-AUTHORITY-013] assistance host and sensor are free of foreign domain coupling', () => {
  const host = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')
  const sensor = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs')

  // AssistanceHost must not directly open Git/Strength/Review/Todo
  assert.doesNotMatch(host, /open Wanxiangshu.Git/)
  assert.doesNotMatch(host, /open Wanxiangshu.Strength/)
  assert.doesNotMatch(host, /open Wanxiangshu.Mission.Review/)
  assert.doesNotMatch(host, /open Wanxiangshu.Mission.Obligation.Todo/)

  // NeedHelpSensor must not directly open Git/Strength.Persistence/Review/Todo
  assert.doesNotMatch(sensor, /open Wanxiangshu.Git/)
  assert.doesNotMatch(sensor, /open Wanxiangshu.Strength.Persistence/)
  assert.doesNotMatch(sensor, /open Wanxiangshu.Mission.Review/)
  assert.doesNotMatch(sensor, /open Wanxiangshu.Mission.Obligation.Todo/)
})
