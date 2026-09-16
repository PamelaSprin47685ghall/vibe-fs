import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[INSTITUTIONAL-LEARNING-006] positive and negative experiences use the same non-punitive bounded enhancer', () => {
  const tools = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  const calls = [...tools.matchAll(/InstitutionalEnhancer\.evaluate experience rules/g)]
  assert.equal(calls.length, 1, 'both verbs share one execution path and one evaluator')
  assert.doesNotMatch(tools, /Penalty|Punish|Severity|Score/)
})
