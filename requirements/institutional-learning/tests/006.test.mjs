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

test('WHAT[institutional-learning-006] GAP-181: celebrate and regret give a reusable mechanism the same BIRTH opportunity', async () => {
  await withExecutablePlugin(async (hooks, _directory, created, runtime) => {
    const session = 'learning-birth-both-verbs'
    await admit(runtime, session)
    const ctx = (id) => context(session, id)
    const experience = 'A reusable mechanism emerged from this work.'

    const candidateOf = (tipName) => ({
      tipName,
      enforcerTextEn: 'Enforce the distilled mechanism.',
      enforcerTextZh: '执行提炼出的机制。',
      mainTextEn: 'Main handling text.',
      mainTextZh: 'Main 处置正文。',
      trigger: 'the same mechanism recurs',
      negative: 'a one-off local path is not this rule',
    })

    for (const [verb, callId, tipName] of [
      ['celebrate', 'c-birth', 'positive-tip'],
      ['regret', 'r-birth', 'negative-tip'],
    ]) {
      const result = await hooks.tool[verb].execute({ experience, candidate: candidateOf(tipName) }, ctx(callId))
      assert.match(result, /BIRTH/)
      assert.match(result, new RegExp(tipName))
    }

    assert.deepEqual(created, [])
    assert.deepEqual(runtime.prompts, [])
  })
})
