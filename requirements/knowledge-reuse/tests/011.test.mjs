import assert from 'node:assert/strict'
import test from 'node:test'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, createCase, deferred } from './support/casebook.mjs'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import { installBookkeeperRuntime, scriptedBookkeeperPort } from './support/bookkeeper-session-support.mjs'

test('WHAT[knowledge-reuse-011] overlapping fetches join one actual in-flight maintenance and receive the same result', async () => {
  const local = sandbox()
  const reached = deferred()
  const release = deferred()
  let first
  let second
  try {
    const { identity, shelfmark } = await createCase(local)
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    const { port, createCalls } = scriptedBookkeeperPort()
    const send = port.SendPrompt
    port.SendPrompt = async (...args) => { reached.resolve(); await release.promise; return send(...args) }
    installBookkeeperRuntime(port, [identity])
    first = local.fetch(shelfmark)
    await reached.promise
    second = local.fetch(shelfmark)
    assert.equal(createCalls.length, 1)
    release.resolve()
    assert.equal(await first, await second)
    assert.equal(createCalls.length, 1)
  } finally {
    release.resolve()
    await Promise.allSettled([first, second])
    bookkeeper.resetRuntime()
    local.close()
  }
})

test.todo('WHAT[knowledge-reuse-011] GAP-160: real replica branches become DomainConflict and converge through explicit resolution without LWW')
