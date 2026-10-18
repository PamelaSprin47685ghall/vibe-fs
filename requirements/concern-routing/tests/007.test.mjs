import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

// Superseded by js.fork / js.join / js.deliver / js.ask: durable Fact, ProjectionUpdate, Fold, AgentJournalPortAdapter, and fact-map contain no Concern family.
test('WHAT[concern-routing-007] durable fact, fold, adapter, and ablation fact-map carry no Concern state', () => {
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/Fact.fs'), /\bConcern\b|ConcernFactCases/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/ProjectionUpdate.fs'), /applyConcern|ConcernProjection|ConcernFactCases/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/Fold.fs'), /AgentFact\.Concern|applyConcern/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/AgentJournalPortAdapter.fs'), /forConcern|ConcernJournalPort/)

  const factMap = JSON.parse(read('resources/ablation/fact-map.json'))
  assert.equal('AgentFact.Concern' in (factMap.facts ?? {}), false)
})
