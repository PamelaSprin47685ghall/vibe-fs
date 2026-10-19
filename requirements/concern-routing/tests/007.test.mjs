import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[concern-routing-007] the historical tombstone creates no Concern state, port, or active registration', () => {
  const fact = read('src/Wanxiangshu/Composition/Durable/Fact.fs')
  assert.match(fact, /type LegacyConcernFact/)
  assert.doesNotMatch(fact, /ConcernFactCases|MessagePublished|MailboxRetired/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/ProjectionUpdate.fs'), /applyConcern|ConcernProjection|ConcernFactCases/)
  const fold = read('src/Wanxiangshu/Composition/Durable/Fold.fs')
  assert.match(fold, /AgentFact\.Concern _ -> Ok projection/)
  assert.doesNotMatch(fold, /applyConcern/)
  assert.doesNotMatch(read('src/Wanxiangshu/Composition/Durable/AgentJournalPortAdapter.fs'), /forConcern|ConcernJournalPort/)

  const factMap = JSON.parse(read('resources/ablation/fact-map.json'))
  assert.equal('AgentFact.Concern' in (factMap.facts ?? {}), false)
})
