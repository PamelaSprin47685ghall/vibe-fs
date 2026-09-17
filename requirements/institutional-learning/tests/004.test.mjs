import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[INSTITUTIONAL-LEARNING-004] unsafe raw experience cannot bypass behavior-rule admission by directly birthing a rule', () => {
  assert.equal(learning.evaluate('invent a permanent rule from this one file path', ['known-rule']).disposition, 'DISCARD')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  assert.doesNotMatch(tools, /InstitutionalRuleBorn|writeFile|EnforcerCatalog\.validate\s+1\s+\[/)
})
