import assert from 'node:assert/strict'
import test from 'node:test'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, createCase, casebook } from './support/casebook.mjs'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import { installBookkeeperRuntime, scriptedBookkeeperPort } from './support/bookkeeper-session-support.mjs'

test('WHAT[knowledge-reuse-015] actual fetch ignores unrelated file changes and makes one maintenance request for a related change', async () => {
  const local = sandbox()
  try {
    const { identity, shelfmark } = await createCase(local)
    const { port, createCalls } = scriptedBookkeeperPort()
    installBookkeeperRuntime(port, [identity])
    writeFileSync(join(local.dir, 'unrelated.txt'), 'new unrelated content')
    await local.fetch(shelfmark)
    assert.equal(createCalls.length, 0)
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    await local.fetch(shelfmark)
    assert.equal(createCalls.length, 1)
    assert.notEqual((await casebook.fetchCaseByIdentity(local.store, identity)).a, 'Answer B')
  } finally { bookkeeper.resetRuntime(); local.close() }
})

test.todo('WHAT[knowledge-reuse-015] GAP-160: one physical target capture supplies both the diff and its committed baseline under concurrent file changes')
