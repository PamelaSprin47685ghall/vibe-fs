import assert from 'node:assert/strict'
import test from 'node:test'
import * as delivery from '../../../dist/Enforcer/Guidance/DeliverySurface.js'
import { guidance, link, observe, withJournal, main, tip } from './support/journal.mjs'

test('WHAT[guidance-delivery-003] repeated lookup in the same horizon returns only stable identity', async () => {
  await withJournal(async ({ journal }) => {
    await link(journal)
    await observe(journal)
    assert.equal((await guidance.resolve(journal, main)).presentation, 'Full')
    const repeat = await guidance.resolve(journal, main)
    assert.deepEqual(repeat, { presentation: 'IdentityOnly', text: `tip: ${tip}`, tipName: tip })
    assert.deepEqual(await guidance.resolve(journal, main), repeat)
  })
})

test('WHAT[guidance-delivery-003] identity-only fold cannot establish Full coverage', () => {
  const state = delivery.apply(tip, 'IdentityOnly', delivery.empty)
  assert.equal(delivery.hasFullDelivered(tip, state), false)
})
