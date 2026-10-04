import assert from 'node:assert/strict'
import test from 'node:test'
import * as guidance from '../../../dist/Enforcer/Guidance/TipSurface.js'
import { link, observe, withJournal, main } from './support/journal.mjs'

test('WHAT[guidance-delivery-001] a new occurrence of the same tip has its own first delivery', async () => {
  await withJournal(async ({ journal }) => {
    await link(journal)
    await observe(journal)
    assert.equal((await guidance.resolve(journal, main)).presentation, 'Full')
    assert.equal((await guidance.resolve(journal, main)).presentation, 'IdentityOnly')
    await observe(journal, 2)
    assert.equal((await guidance.resolve(journal, main)).presentation, 'Full')
  })
})
