import assert from 'node:assert/strict'
import test from 'node:test'
import { guidance, link, observe, withJournal, main } from './support/journal.mjs'

test('WHAT[guidance-delivery-004] disk reopen restores delivery decisions and keeps Main owners separate', async () => {
  await withJournal(async (fixture) => {
    await link(fixture.journal)
    await observe(fixture.journal)
    await link(fixture.journal, 'other-main', 'other-blogger')
    await observe(fixture.journal, 1, undefined, 'other-main', 'other-blogger')
    assert.equal((await guidance.resolve(fixture.journal, main)).presentation, 'Full')
    await fixture.reopen()
    assert.equal((await guidance.resolve(fixture.journal, main)).presentation, 'IdentityOnly')
    assert.equal((await guidance.resolve(fixture.journal, 'other-main')).presentation, 'Full')
    await fixture.reopen()
    assert.equal((await guidance.resolve(fixture.journal, 'other-main')).presentation, 'IdentityOnly')
  })
})
