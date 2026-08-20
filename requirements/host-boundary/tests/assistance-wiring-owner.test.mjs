import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[HOST-BOUNDARY-013] assistance eligibility and lifecycle wiring stay with Assistance owner', () => {
  const owner = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')
  const host = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  assert.match(owner, /module AssistanceHostWiring/)
  assert.match(owner, /scope\.AttachNeedHelpSensor needHelpSensor/)
  assert.match(owner, /scope\.AttachAssistance/)
  assert.match(host, /AssistanceHostWiring\.install needHelpSensor sessionPort journal snapshot scope/)
})
