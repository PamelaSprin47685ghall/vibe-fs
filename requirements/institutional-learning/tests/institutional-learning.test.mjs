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

test('WHAT[INSTITUTIONAL-LEARNING-002] one enhancer evaluation yields exactly one ABSORB BIRTH or DISCARD disposition with no score state', () => {
  assert.equal(learning.evaluate('known-rule explained the mechanism', ['known-rule']).disposition, 'ABSORB')
  assert.equal(learning.evaluate('unrelated local anecdote', ['known-rule']).disposition, 'DISCARD')
  const enhancer = read('src/Wanxiangshu/Enforcer/InstitutionalLearning/Enhancer.fs')
  assert.doesNotMatch(enhancer, /\bScore\b|\bThreshold\b|\bRetryLoop\b|^\s*(?:while|for)\s/m)
})

test('WHAT[INSTITUTIONAL-LEARNING-003] enhancer is bounded to the supplied experience and live rulebook snapshot', () => {
  const enhancer = read('src/Wanxiangshu/Enforcer/InstitutionalLearning/Enhancer.fs')
  assert.match(enhancer, /let evaluate \(experience: string\) \(rules: EnforcerRule list\)/)
  assert.doesNotMatch(enhancer, /readFile|PackageResources|Http|fetch|Network|Repository/)
})

test('WHAT[INSTITUTIONAL-LEARNING-004] unsafe raw experience cannot bypass behavior-rule admission by directly birthing a rule', () => {
  assert.equal(learning.evaluate('invent a permanent rule from this one file path', ['known-rule']).disposition, 'DISCARD')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  assert.doesNotMatch(tools, /InstitutionalRuleBorn|writeFile|EnforcerCatalog\.validate\s+1\s+\[/)
})

test('WHAT[INSTITUTIONAL-LEARNING-005] no reusable trigger or nonduplicate mechanism degrades to DISCARD rather than attention-tax debt', () => {
  assert.equal(learning.evaluate('one-off timestamp 2026-08-20 in /tmp/a', ['known-rule']).disposition, 'DISCARD')
})

test('WHAT[INSTITUTIONAL-LEARNING-006] positive and negative experiences use the same non-punitive bounded enhancer', () => {
  const tools = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  const calls = [...tools.matchAll(/InstitutionalEnhancer\.evaluate experience rules/g)]
  assert.equal(calls.length, 1, 'both verbs share one execution path and one evaluator')
  assert.doesNotMatch(tools, /Penalty|Punish|Severity|Score/)
})

test('WHAT[INSTITUTIONAL-LEARNING-007] celebrate alone resurfaces deferred work and the same durable fact updates attention coverage', () => {
  const tools = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  assert.match(tools, /ExperienceKind\.Celebrate -> AttentionProjection\.pending/)
  assert.match(tools, /ExperienceKind\.Regret -> \[\]/)

  const fold = read('src/Wanxiangshu/Composition/Durable/Fold.fs')
  assert.match(fold, /InstitutionalLearningFactFold\.fold projection learning[\s\S]*?AttentionFactFold\.foldLearning updated learning/)
})

test('WHAT[INSTITUTIONAL-LEARNING-008] occurrence replay keeps the first frozen result and does not create a second disposition', () => {
  let state = learning.empty()
  state = learning.commit('ses-a', 'learn-1', 'celebrate', 'raw', 'rev-1', 'DISCARD', 'frozen-first', ['defer-1'], state)
  state = learning.commit('ses-a', 'learn-1', 'celebrate', 'changed', 'rev-2', 'ABSORB', 'frozen-second', [], state)
  assert.equal(learning.frozen('ses-a', 'learn-1', state), 'frozen-first')

  const tool = read('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs')
  assert.match(tool, /tryFind sessionId occurrence[\s\S]*?Some record -> return record\.FrozenResult/)
})

