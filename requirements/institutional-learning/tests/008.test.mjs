import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[INSTITUTIONAL-LEARNING-008] occurrence replay keeps the first frozen result and does not create a second disposition', () => {
  let state = learning.empty()
  state = learning.commit('ses-a', 'learn-1', 'celebrate', 'raw', 'rev-1', 'DISCARD', 'frozen-first', ['defer-1'], state)
  state = learning.commit('ses-a', 'learn-1', 'celebrate', 'changed', 'rev-2', 'ABSORB', 'frozen-second', [], state)
  assert.equal(learning.frozen('ses-a', 'learn-1', state), 'frozen-first')

  const tool = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  assert.match(tool, /InstitutionalLearningProjection\.tryFind/)
  assert.match(tool, /Some record -> return record\.FrozenResult/)
})
