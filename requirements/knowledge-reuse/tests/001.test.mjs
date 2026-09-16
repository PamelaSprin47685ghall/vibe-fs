import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../dist/Knowledge/Casebook/StoreSurface.js'
import * as domain from '../../../dist/Knowledge/Casebook/DomainSurface.js'

test('WHAT[KNOWLEDGE-REUSE-001] CASE001_casebook_is_best_effort_semantic_cache_with_readonly_observation_replay', async () => {
  const caseId = 'case-001-replay'
  const question = 'how does foo work?'
  const answer = 'foo works via bar'
  const observation = domain.readObservation('src/foo.txt', 'hash-1')

  let s = store.empty
  const created = store.captureCase(caseId, question, answer, [observation], '2026-01-01T00:00:00Z', s)
  assert.equal(created.ok, true)
  s = created.value

  const fetched = store.fetchCase(caseId, [observation], s)
  assert.equal(fetched.ok, true)
  assert.equal(fetched.value.fresh, true)
  assert.equal(fetched.value.answer, answer)

  const changedObs = domain.readObservation('src/foo.txt', 'hash-2')
  const staleFetch = store.fetchCase(caseId, [changedObs], s)
  assert.equal(staleFetch.ok, true)
  assert.equal(staleFetch.value.fresh, false)
  assert.equal(staleFetch.value.answer, answer)
})
