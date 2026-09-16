import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[INSTITUTIONAL-LEARNING-001] celebrate and regret accept one raw natural-language experience without a rule template', () => {
  const tools = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  assert.match(tools, /Name = "celebrate"[\s\S]*?Arguments = \[ "experience", argument \]/)
  assert.match(tools, /Name = "regret"[\s\S]*?Arguments = \[ "experience", argument \]/)
  assert.equal(learning.evaluate('a local success happened', ['known-rule']).disposition, 'DISCARD')
})
