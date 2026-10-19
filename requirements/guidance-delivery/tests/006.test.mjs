import assert from 'node:assert/strict'
import test from 'node:test'
import { guidance, link, observe, withJournal, main, blogger, tip } from './support/journal.mjs'

test('WHAT[guidance-delivery-006] Blogger resolves its linked Main and shares that delivery state', async () => {
  await withJournal(async ({ journal }) => {
    await link(journal)
    await observe(journal)
    const result = await guidance.resolve(journal, blogger)
    assert.equal(result.presentation, 'Full')
    assert.equal(result.tipName, tip)
    assert.equal((await guidance.resolve(journal, main)).presentation, 'IdentityOnly')
  })
})

test('WHAT[guidance-delivery-006] an unlinked or tip-less session resolves to no invented guidance', async () => {
  await withJournal(async ({ journal }) => {
    await link(journal)
    assert.equal(await guidance.resolve(journal, main), null)
    await observe(journal, 1, 'freely-authored-tip')
    const delivered = await guidance.resolve(journal, main)
    assert.equal(delivered.presentation, 'Full')
    assert.equal(delivered.tipName, 'freely-authored-tip')
    assert.equal(await guidance.resolve(journal, 'unrelated'), null)
  })
})

test('WHAT[guidance-delivery-006] a tip without association cannot invent an owner', async () => {
  await withJournal(async ({ journal }) => {
    await observe(journal)
    assert.equal(await guidance.resolve(journal, main), null)
  })
})
