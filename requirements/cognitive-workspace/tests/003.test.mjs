import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-003] legacy Cognition facts decode only into a fold no-op tombstone', () => {
  const fact = read('src/Wanxiangshu/Composition/Durable/Fact.fs')
  const fold = read('src/Wanxiangshu/Composition/Durable/Fold.fs')
  const envelope = read('src/Wanxiangshu/Persistence/Journal/Envelope.fs')

  assert.match(fact, /type LegacyCognitionFact/)
  assert.match(envelope, /LegacyCognitionFact/)
  assert.match(fold, /AgentFact\.Cognition _ -> Ok projection/)
})
