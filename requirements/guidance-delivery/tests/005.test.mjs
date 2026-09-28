import assert from 'node:assert/strict'
import test from 'node:test'
import { guidance, link, observe, withJournal, main } from './support/journal.mjs'

test('WHAT[guidance-delivery-005] real reanchor restores full text and subsequent lookup returns identity', async () => {
  await withJournal(async (fixture) => {
    await link(fixture.journal)
    await observe(fixture.journal)
    const first = await guidance.resolve(fixture.journal, main)
    assert.equal(first.presentation, 'Full')
    assert.equal((await guidance.resolve(fixture.journal, main)).presentation, 'IdentityOnly')
    const reanchor = await guidance.appendContextReanchored(fixture.journal, {
      session: main, previousEpoch: 0, nextEpoch: 1, observedCompactionRun: 'compaction-1',
    })
    assert.equal(reanchor.ok, true, reanchor.error)
    await fixture.reopen()
    assert.deepEqual(await guidance.resolve(fixture.journal, main), first)
    assert.equal((await guidance.resolve(fixture.journal, main)).presentation, 'IdentityOnly')
  })
})

test.todo('WHAT[guidance-delivery-005] GAP-115 actual restoration preserves occurrence frontier without adding a first-delivery fact')
