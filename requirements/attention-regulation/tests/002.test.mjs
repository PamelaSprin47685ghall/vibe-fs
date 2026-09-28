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

test.todo('WHAT[attention-regulation-002] GAP-118 seed a real formal obligation and prove abandon leaves its complete ledger unchanged')
