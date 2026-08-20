import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[DG-002] LoopSensor owns its physical child interruptibility predicate', () => {
  const owner = read('src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  const host = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  assert.match(owner, /module LoopSensor/)
  assert.match(owner, /ownedSessions\.Contains key && sessionParents\.ContainsKey key/)
  assert.match(owner, /let create/)
  assert.match(host, /LoopSensor\.create/)
  assert.doesNotMatch(host, /NeedHelpSensor\.createInterruptiblePredicate/)
})
