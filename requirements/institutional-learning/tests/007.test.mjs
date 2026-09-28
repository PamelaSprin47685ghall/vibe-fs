import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin, observeAuthority } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { admit, context } from './support/plugin.mjs'

test('WHAT[institutional-learning-007] real celebrate returns the learning receipt before the deferred tail; regret and replay leave new work pending', async () => {
  await withExecutablePlugin(async (hooks, _directory, created, runtime) => {
    const session = 'learning-tail'
    await admit(runtime, session)
    const before = observeAuthority(runtime, session)
    const ctx = id => context(session, id)
    const experience = { experience: 'The local experiment worked once.' }
    await hooks.tool.defer.execute({ new_work: 'FIRST PENDING ITEM' }, ctx('defer-1'))
    const regret = await hooks.tool.regret.execute(experience, ctx('regret'))
    assert.match(regret, /DISCARD/)
    assert.equal(regret.includes('PENDING ITEM'), false)
    const receipt = await hooks.tool.celebrate.execute(experience, ctx('celebrate-1'))
    assert.ok(receipt.indexOf('DISCARD') < receipt.indexOf('FIRST PENDING ITEM'))
    assert.ok(receipt.trimEnd().endsWith('FIRST PENDING ITEM'))
    await hooks.tool.defer.execute({ new_work: 'SECOND PENDING ITEM' }, ctx('defer-2'))
    assert.equal(await hooks.tool.celebrate.execute(experience, ctx('celebrate-1')), receipt)
    const next = await hooks.tool.celebrate.execute(experience, ctx('celebrate-2'))
    assert.ok(next.trimEnd().endsWith('SECOND PENDING ITEM'))
    assert.equal(next.includes('FIRST PENDING ITEM'), false)
    assert.deepEqual(observeAuthority(runtime, session), before)
    assert.deepEqual(created, [])
    assert.deepEqual(runtime.prompts, [])
    assert.deepEqual(runtime.abortedIds, [])
  })
})

test.todo('WHAT[institutional-learning-007] GAP-180: pause a real BIRTH admission before completion and prove deferred work cannot surface early')
