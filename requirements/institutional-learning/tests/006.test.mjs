import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { admit, context } from './support/plugin.mjs'

test('WHAT[institutional-learning-006] actual celebrate and regret both accept informal experiences without fabricating rule creation', async () => {
  await withExecutablePlugin(async (hooks, _directory, created, runtime) => {
    const session = 'learning-both-verbs'
    await admit(runtime, session)
    for (const [verb, experience] of [['celebrate', 'An experiment happened to work today.'], ['regret', 'An experiment happened to fail today.']]) {
      const result = await hooks.tool[verb].execute({ experience }, context(session, verb))
      assert.match(result, /DISCARD/)
      assert.match(result, /no rule was created/)
    }
    assert.deepEqual(created, [])
    assert.deepEqual(runtime.prompts, [])
  })
})

test.todo('WHAT[institutional-learning-006] GAP-181: a reusable positive mechanism has the same BIRTH opportunity as a negative one; two DISCARD receipts cannot prove this')
