import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

// Superseded by ANCHOR view.extra and assume: durable Fact, ProjectionUpdate, Fold, AgentJournalPortAdapter, and fact-map contain no Attention family.
test('WHAT[attention-regulation-006] durable fact, fold, adapter, and ablation fact-map carry no Attention state', () => {
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/Fact.fs'), /\bAttention\b|AttentionFactCases/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/ProjectionUpdate.fs'), /applyAttention|AttentionProjection|AttentionFactCases/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/Fold.fs'), /AgentFact\.Attention|applyAttention/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/AgentJournalPortAdapter.fs'), /forAttention|AttentionJournalPort/)

  const factMap = JSON.parse(read('resources/ablation/fact-map.json'))
  assert.equal('AgentFact.Attention' in (factMap.facts ?? {}), false)
})
