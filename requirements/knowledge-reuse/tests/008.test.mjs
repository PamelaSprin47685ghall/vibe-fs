import assert from 'node:assert/strict'
import test from 'node:test'
import { sandbox, createCase, casebook } from './support/casebook.mjs'

test('WHAT[knowledge-reuse-008] actual access events increase order and determine LRU victims independently of timestamps', async () => {
  const local = sandbox()
  try {
    for (const identity of ['first', 'second', 'third']) await createCase(local, identity)
    const before = await casebook.fetchCaseByIdentity(local.store, 'first')
    assert.equal((await casebook.touchAccess(local.store, 'first')).ok, true)
    const touched = await casebook.fetchCaseByIdentity(local.store, 'first')
    assert.ok(touched.accessOrder > before.accessOrder)
    const cases = await Promise.all(['first', 'second', 'third'].map(identity => casebook.fetchCaseByIdentity(local.store, identity)))
    const result = casebook.evict(2, cases)
    assert.deepEqual(result.victims, ['second'])
    assert.deepEqual(result.kept.map(item => item.identity).sort(), ['first', 'third'])
    assert.deepEqual(casebook.evict(3, cases).victims, [])
    assert.equal((await casebook.evictCase(local.store, 'second')).ok, true)
    assert.equal(await casebook.fetchCaseByIdentity(local.store, 'second'), null)
    assert.notEqual(await casebook.fetchCaseByIdentity(local.store, 'first'), null)
  } finally { local.close() }
})

test.todo('WHAT[knowledge-reuse-008] GAP-160: production capacity pressure appends each required eviction rather than only hiding excess entries')
