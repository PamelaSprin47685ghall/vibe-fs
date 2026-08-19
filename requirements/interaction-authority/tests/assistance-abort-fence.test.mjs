import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[INTERACTION-AUTHORITY-012] NEEDHELP claim survives TurnAborted until fresh SessionIdle consumes it', () => {
  const sensor = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs')
  const host = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')

  assert.match(sensor, /member _\.TryObserveAssistanceClaim/)
  assert.match(sensor, /TryObserveAssistanceClaim[\s\S]*?armed\.Contains/)
  assert.match(sensor, /member _\.TryConsumeAssistanceClaim/)

  const ownerSide = host.match(/let handleOwnerSideTurn([\s\S]*?)let activeConsultationAbort/)
  assert.ok(ownerSide, 'owner-side assistance routing must be inspectable')
  assert.match(ownerSide[1], /TryObserveAssistanceClaim/)
  assert.doesNotMatch(ownerSide[1], /TryConsumeAssistanceClaim/)

  const fence = host.match(/let withFreshAssistanceQuiescence([\s\S]*?)let escalateFastOwnerRequest/)
  assert.ok(fence, 'fresh-idle assistance fence must be inspectable')
  assert.match(fence[1], /match context\.Quiescence with/)
  assert.match(fence[1], /\| None -> Task\.FromResult AssistanceTurnDisposition\.Handled/)
  assert.match(fence[1], /\| Some _ ->[\s\S]*?TryConsumeAssistanceClaim[\s\S]*?continueAfterIdle/)
})

