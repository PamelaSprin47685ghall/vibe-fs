import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'
import { withRestartablePlugin, configureManagedPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { admit, context } from './support/plugin.mjs'

test('WHAT[institutional-learning-008] projection keeps the first frozen occurrence, isolates sessions and does not mutate its input', () => {
  const empty = learning.empty()
  const first = learning.commit('session-a', 'learning-1', 'celebrate', 'raw', 'rev-1', 'DISCARD', 'first', ['defer-1'], empty)
  const replay = learning.commit('session-a', 'learning-1', 'celebrate', 'changed', 'rev-2', 'ABSORB', 'second', [], first)
  const other = learning.commit('session-b', 'learning-1', 'regret', 'other', 'rev-1', 'DISCARD', 'other', [], replay)
  assert.equal(learning.frozen('session-a', 'learning-1', empty), null)
  assert.equal(learning.frozen('session-a', 'learning-1', other), 'first')
  assert.equal(learning.frozen('session-b', 'learning-1', other), 'other')
  assert.equal(learning.frozen('session-a', 'unknown', other), null)
})

test('WHAT[institutional-learning-008] actual plugin restart restores the frozen receipt and consumed deferred work without consuming later reminders', async () => {
  await withRestartablePlugin(async (start, _directory, fixture) => {
    const boot = async () => {
      const hooks = await start()
      await configureManagedPlugin(hooks)
      return hooks
    }
    let hooks = await boot()
    const session = 'learning-restart'
    await fixture.withRuntime(runtime => admit(runtime, session))
    const ctx = id => context(session, id)
    const args = { experience: 'This was one local success.' }
    await hooks.tool.defer.execute({ new_work: 'ORIGINAL REMINDER' }, ctx('defer-1'))
    const frozen = await hooks.tool.celebrate.execute(args, ctx('celebrate-1'))
    assert.ok(frozen.includes('ORIGINAL REMINDER'))
    await fixture.stop(hooks)
    hooks = await boot()
    await hooks.tool.defer.execute({ new_work: 'LATER REMINDER' }, ctx('defer-2'))
    assert.equal(await hooks.tool.celebrate.execute(args, ctx('celebrate-1')), frozen)
    const next = await hooks.tool.celebrate.execute(args, ctx('celebrate-2'))
    assert.ok(next.includes('LATER REMINDER'))
    assert.equal(next.includes('ORIGINAL REMINDER'), false)
  })
})

test.todo('WHAT[institutional-learning-008] GAP-180: injected staging, durable commit and rulebook revision failures commit no partial learning/rule/deferred result')
test.todo('WHAT[institutional-learning-008] GAP-182: required DeferredWorkResurfaced fact versus current single LearningDispositionCommitted fact needs a contract decision')
