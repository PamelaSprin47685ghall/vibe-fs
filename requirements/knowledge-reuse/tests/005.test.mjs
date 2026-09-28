import assert from 'node:assert/strict'
import test from 'node:test'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, createCase, casebook, index, parse } from './support/casebook.mjs'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import { CANONICAL_Q, CANONICAL_A, installBookkeeperRuntime, scriptedBookkeeperPort } from './support/bookkeeper-session-support.mjs'

test('WHAT[knowledge-reuse-005] actual fetch refreshes changed files once and atomically retains the completion baseline', async () => {
  const local = sandbox()
  try {
    const { identity, baseline, shelfmark } = await createCase(local)
    const { port, createCalls } = scriptedBookkeeperPort()
    installBookkeeperRuntime(port, [identity])
    assert.equal(parse(await local.fetch(shelfmark)).answer, 'Answer B')
    assert.equal(createCalls.length, 0)
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    assert.equal(parse(await local.fetch(shelfmark)).answer, CANONICAL_A)
    const maintained = await casebook.fetchCaseByIdentity(local.store, identity)
    assert.equal(maintained.q, CANONICAL_Q)
    assert.equal(maintained.completionFileState, baseline)
    assert.notEqual(maintained.maintenanceFileState, baseline)
    const again = await local.fetch(index.shelfmarkFor(identity, CANONICAL_Q))
    assert.equal(parse(again).answer, CANONICAL_A)
    assert.match(again, /No change was found|没有变化/)
    assert.equal(createCalls.length, 1)
  } finally { bookkeeper.resetRuntime(); local.close() }
})

test('WHAT[knowledge-reuse-005] failed actual maintenance returns the old answer and leaves both baselines unchanged', async () => {
  const local = sandbox()
  try {
    bookkeeper.resetRuntime()
    const { identity, baseline, shelfmark } = await createCase(local)
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    const result = await local.fetch(shelfmark)
    assert.equal(parse(result).answer, 'Answer B')
    assert.match(result, /could not|unable|未|无法/i)
    const current = await casebook.fetchCaseByIdentity(local.store, identity)
    assert.equal(current.completionFileState, baseline)
    assert.equal(current.maintenanceFileState, baseline)
  } finally { local.close() }
})

test('WHAT[knowledge-reuse-005] maintained durable baseline must supply the actual old bytes in its diff', { todo: 'GAP-161: persistent baseline diff substitutes a hash for original content' }, async () => {
  const local = sandbox()
  try {
    const { baseline } = await createCase(local)
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    const diff = await casebook.computeMaintenanceDiff(local.dir, baseline)
    assert.match(diff.diffSummary, /-version-B/)
    assert.match(diff.diffSummary, /\+version-C/)
  } finally { local.close() }
})
