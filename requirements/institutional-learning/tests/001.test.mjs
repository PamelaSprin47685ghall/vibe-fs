import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { admit, context } from './support/plugin.mjs'

test('WHAT[institutional-learning-001] actual tools accept raw natural language and reject blank experience without consuming the occurrence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _created, runtime) => {
    const session = 'learning-raw'
    await admit(runtime, session)
    for (const verb of ['celebrate', 'regret']) {
      for (const [index, blank] of ['', ' \t\n '].entries()) {
        const ctx = context(session, `${verb}-${index}`)
        const rejected = await hooks.tool[verb].execute({ experience: blank }, ctx)
        assert.match(rejected, /non-empty experience/)
        const accepted = await hooks.tool[verb].execute({ experience: '今天这个局部办法挺顺手，具体原因还不清楚。' }, ctx)
        assert.match(accepted, /DISCARD/)
        assert.notEqual(accepted, rejected)
      }
    }
  })
})
