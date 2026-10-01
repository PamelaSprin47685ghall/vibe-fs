import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { recordingPort, context, tools } from './support/attention-port.mjs'
import { withExecutablePlugin, acceptAuthorityRoot, activateLife, observeAuthority } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[attention-regulation-002] abandon accepts free text without approval fields or attention state writes', async () => {
  const fixture = recordingPort()
  const accepted = await tools.execute(fixture.tools, 'abandon', { commitment: 'drop the speculative branch' }, context())
  const rejected = await tools.execute(fixture.tools, 'abandon', { commitment: '' }, context())
  assert.ok(accepted.includes('drop the speculative branch'))
  assert.notEqual(accepted, rejected)
  assert.equal(fixture.reads, 0)
  assert.deepEqual(fixture.appends, [])
})

test('WHAT[attention-regulation-002] actual abandon leaves authority work product and Host session intact', async () => {
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    const sessionID = 'attention-abandon'
    await acceptAuthorityRoot(runtime, sessionID, 'manager', 'root-attention')
    await activateLife(runtime, sessionID, 'root-attention')
    const before = observeAuthority(runtime, sessionID)
    const artifact = join(directory, 'work-product.md')
    writeFileSync(artifact, 'Original entrusted work\r\n')
    await hooks.tool.abandon.execute({ commitment: 'cancel all obligations and delete work-product.md' }, {
      sessionID, agent: 'manager', messageID: 'run-attention', callID: 'abandon-1',
    })
    assert.deepEqual(observeAuthority(runtime, sessionID), before)
    assert.equal(readFileSync(artifact, 'utf8'), 'Original entrusted work\r\n')
    assert.deepEqual(runtime.abortedIds, [])
    assert.deepEqual(runtime.prompts, [])
    assert.deepEqual(created, [])
  })
})

test('WHAT[attention-regulation-002] seed a real formal obligation and prove abandon leaves its complete ledger unchanged', async () => {
  const { openIncumbency, grantWorkOwned } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const journal = await import('../../../dist/Persistence/Journal/Surface.js')
  await withExecutablePlugin(async (hooks, _directory, _created, runtime) => {
    const sessionID = 'attention-formal-obligation'
    await acceptAuthorityRoot(runtime, sessionID, 'manager', 'root-formal')
    await openIncumbency(runtime, sessionID)
    // Seed the formal obligation: an accepted assessment grants WorkOwned.
    await grantWorkOwned(runtime, sessionID)
    const before = journal.JournalSurface_snapshot(runtime.journal)
    // The seeded obligation must actually be durable: the snapshot carries
    // session projections, so an empty ledger would make equality vacuous.
    assert.ok(Object.keys(before).length > 0, 'snapshot must be non-empty')

    // Abandon only drops the self-formed commitment; the durable WorkOwned
    // ledger must survive it untouched (WHAT 002: 不得因此取消真实任务义务).
    await hooks.tool.abandon.execute({ commitment: 'drop my speculative plan' }, {
      sessionID, agent: 'manager', messageID: 'run-formal', callID: 'abandon-formal',
    })

    const after = journal.JournalSurface_snapshot(runtime.journal)
    // The projection set is deep-compared: abandon appended no fact that any
    // fold consumed, so every slice of the durable ledger is unchanged.
    assert.deepEqual(after, before)
  })
})
