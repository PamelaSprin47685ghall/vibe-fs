import assert from 'node:assert/strict'
import test from 'node:test'
import { guidance, link, observe, withJournal, main } from './support/journal.mjs'

test('WHAT[guidance-delivery-007] aliases give the same Full and Identity bytes from equal durable starting states', async () => {
  const sequence = async (read) => withJournal(async ({ journal }) => {
    await link(journal)
    await observe(journal)
    return [await read(journal, main), await read(journal, main)]
  })
  assert.deepEqual(await sequence(guidance.latestNudge), await sequence(guidance.latest))
})
