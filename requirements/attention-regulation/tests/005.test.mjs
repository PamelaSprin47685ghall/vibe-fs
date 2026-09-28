import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin, acceptAuthorityRoot, activateLife, observeAuthority } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[attention-regulation-005] actual celebrate appends deferred items at the tail once while regret and replay do not consume new work', async () => {
  await withExecutablePlugin(async (hooks, _directory, created, runtime) => {
    const sessionID = 'attention-celebrate'
    await acceptAuthorityRoot(runtime, sessionID, 'manager', 'root-attention')
    await activateLife(runtime, sessionID, 'root-attention')
    const before = observeAuthority(runtime, sessionID)
    const ctx = (callID) => ({ sessionID, agent: 'manager', messageID: 'run-attention', callID })
    const defer = (text, callID) => hooks.tool.defer.execute({ new_work: text }, ctx(callID))
    const args = { experience: 'A local experiment completed.' }
    await defer('FIRST DEFERRED ITEM', 'defer-1')
    await defer('SECOND DEFERRED ITEM', 'defer-2')
    const regret = await hooks.tool.regret.execute(args, ctx('regret-1'))
    assert.equal(regret.includes('DEFERRED ITEM'), false)
    const first = await hooks.tool.celebrate.execute(args, ctx('celebrate-1'))
    assert.ok(first.indexOf('FIRST DEFERRED ITEM') > 0)
    assert.ok(first.indexOf('SECOND DEFERRED ITEM') > first.indexOf('FIRST DEFERRED ITEM'))
    assert.ok(first.trimEnd().endsWith('SECOND DEFERRED ITEM'))
    await defer('THIRD DEFERRED ITEM', 'defer-3')
    assert.equal(await hooks.tool.celebrate.execute(args, ctx('celebrate-1')), first)
    const next = await hooks.tool.celebrate.execute(args, ctx('celebrate-2'))
    assert.ok(next.includes('THIRD DEFERRED ITEM'))
    assert.equal(next.includes('FIRST DEFERRED ITEM'), false)
    assert.equal(next.includes('SECOND DEFERRED ITEM'), false)
    const finished = await hooks.tool.celebrate.execute(args, ctx('celebrate-3'))
    assert.equal(finished.includes('DEFERRED ITEM'), false)
    assert.deepEqual(observeAuthority(runtime, sessionID), before)
    assert.deepEqual(runtime.prompts, [])
    assert.deepEqual(runtime.abortedIds, [])
    assert.deepEqual(created, [])
  })
})
