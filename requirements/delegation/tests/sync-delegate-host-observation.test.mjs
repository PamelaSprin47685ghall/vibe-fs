import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[DELEG-008] SyncDelegate owns Host tool-call stream observation', () => {
  const owner = read('src/Wanxiangshu/Execution/Delegation/SyncDelegate/OpenCode/Observation.fs')
  const rootHost = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  assert.match(owner, /module SyncDelegateHostObservation/)
  assert.match(owner, /"message\.part\.updated"/)
  assert.match(owner, /SyncDelegate\.tryRoleOfToolName/)
  assert.match(owner, /ObserveProviderToolCall/)
  assert.match(rootHost, /SyncDelegateHostObservation\.observe scope\.SyncDelegateRuntime raw/)
  assert.doesNotMatch(rootHost, /ObserveProviderToolCall|tryRoleOfToolName|messageID|callID/)
})
