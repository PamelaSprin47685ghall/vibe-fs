import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[INSTITUTIONAL-LEARNING-003] enhancer is bounded to the supplied experience and live rulebook snapshot', () => {
  const enhancer = read('src/Wanxiangshu/Enforcer/InstitutionalLearning/Enhancer.fs')
  assert.match(enhancer, /let evaluate \(experience: string\) \(rules: EnforcerRule list\)/)
  assert.doesNotMatch(enhancer, /readFile|PackageResources|Http|fetch|Network|Repository/)
})
