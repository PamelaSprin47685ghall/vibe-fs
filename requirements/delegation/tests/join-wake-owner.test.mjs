import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[DELEG-015] external chat material join wake is owned by Handle/OpenCode', () => {
  const owner = read('src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/JoinWake.fs')
  const host = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  assert.match(owner, /module JoinWake/)
  assert.match(owner, /ChatAdmissionIntent\.Decision\.ExternalRootIntent evidence/)
  assert.match(owner, /registry\.SignalUserMessage evidence\.Key\.SessionId/)
  assert.match(host, /JoinWake\.observeChatMessage scope\.Sessions\.JoinInterrupts intent/)
  assert.doesNotMatch(host, /SignalUserMessage sessionId/)
})
