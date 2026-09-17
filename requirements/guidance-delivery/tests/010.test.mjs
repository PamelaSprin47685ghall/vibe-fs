import assert from 'node:assert/strict'
import test from 'node:test'
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const RULEBOOK = join(ROOT, 'resources/enforcer')

const tipNames = () =>
  readdirSync(RULEBOOK, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort()

test('WHAT[GD-010] AUDIENCE_004_corpus_distinctness_entrusted_to_review_without_runtime_similarity_gate', () => {
  // Static check: delivery & catalog sources must not implement runtime text-similarity interceptors
  const catalogSource = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Catalog.fs'), 'utf8')
  const deliverySource = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Guidance/DeliveryProjection.fs'), 'utf8')
  assert.doesNotMatch(catalogSource, /similarityThreshold|cosineSimilarity|lexicalOverlapReject|overlapScore/i)
  assert.doesNotMatch(deliverySource, /similarityThreshold|cosineSimilarity|lexicalOverlapReject|overlapScore/i)

  // Behavioral check: every packaged rule is valid and accessible by exact identity,
  // without being blocked by textual overlap or shared phrases across rules
  const rules = enforcer.rules()
  assert.equal(rules.length, 120, 'all 120 rules must be loaded without similarity suppression')
  for (const rule of rules) {
    const found = enforcer.tryFindByField(rule.fieldName)
    assert.ok(found !== null, `rule ${rule.fieldName} must be retrievable by exact identity`)
    assert.equal(found.name, rule.name)
  }
})
