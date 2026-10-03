import test from 'node:test'
import assert from 'node:assert/strict'
import { store, persistence, digest, mustOk, body, createdBody, batch, append, current, withStore } from './persistence-support.mjs'

test('WHAT[sphinx-v2-011] durable command replay returns its original content-bound receipt after later revisions and a new writer', async () => {
  await withStore(async ({ open, close }) => {
    const first = open()
    const creation = await append(first, batch('receipt-inquiry', 'create', [createdBody('original goal')]))
    const command = { inquiry: 'receipt-inquiry', commandId: 'cancel', commandFingerprint: digest('command:cancel'), reason: 'stop' }
    assert.deepEqual(mustOk(persistence.admitCancel(first, command)), { outcome: 'fresh' })
    const cancelled = await append(first, batch('receipt-inquiry', 'cancel', [body('CancelRequested', { reason: 'stop' })], creation))
    await append(first, batch('receipt-inquiry', 'later', [body('CancelRequested', { reason: 'later request' })], cancelled))
    const before = current(first, 'receipt-inquiry')
    assert.equal(mustOk(before).revision, '2')
    const repeated = await store.append(first, [cancelled])
    assert.equal(repeated.ok, true)
    assert.deepEqual(repeated.cuts, [])
    assert.deepEqual(current(first, 'receipt-inquiry'), before)
    assert.deepEqual(mustOk(persistence.admitCancel(first, command)), { outcome: 'replayed', revision: '1', eventId: cancelled.id })
    close(first)
    const reopened = open()
    assert.deepEqual(current(reopened, 'receipt-inquiry'), before)
    assert.deepEqual(mustOk(persistence.admitCancel(reopened, command)), { outcome: 'replayed', revision: '1', eventId: cancelled.id })
    const conflict = persistence.admitCancel(reopened, { ...command, reason: 'different', commandFingerprint: digest('different content') })
    assert.equal(conflict.ok, false)
    assert.equal(conflict.error.code, 'COMMAND_CONFLICT')
    assert.deepEqual(current(reopened, 'receipt-inquiry'), before)
    assert.deepEqual(store.read(reopened, cancelled.id), cancelled)
  })
})


test.todo('WHAT[sphinx-v2-011] the actual runtime returns the same receipt for exact result replay and rejects same work attempt fence with changed payload before stale revision checks')
