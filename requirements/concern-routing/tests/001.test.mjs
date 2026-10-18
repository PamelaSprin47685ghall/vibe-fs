import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

// Superseded by js.fork / js.join / js.deliver / js.ask: automatic session mailbox subscription is retired.
test('WHAT[concern-routing-001] HostSignalBootstrap no longer subscribes a self-mailbox on chat.message admission', () => {
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  assert.doesNotMatch(bootstrap, /ensureSelfMailbox|subscribeSelfMailbox|selfMailboxName|Interaction\.Concern/)
})
