import assert from 'node:assert/strict'
import test from 'node:test'
import { withDispatch, dispatch, journal } from './support/dispatch.mjs'

test('WHAT[effect-accounting-003] physical send sees its exact intent already recoverable from the production journal', async () => {
  await withDispatch(async ({ open, send }) => {
    let sends = 0
    const result = await send({
      SendPrompt: async (_session, _text, options) => {
        const reopened = await open('observer-writer')
        const claims = dispatch.projectionObservation(reopened, 'child').pendingClaims
        assert.equal(claims.length, 1)
        assert.equal(claims[0].promptKey, options.Metadata.wanxiangshu_prompt_key)
        sends += 1
        return dispatch.admittedWithReceipt('transport-receipt')
      },
    })
    assert.equal(result.ok, true, result.error)
    assert.equal(sends, 1)
  })
})

test('WHAT[effect-accounting-003] a disposed journal handle prevents the physical send', async () => {
  await withDispatch(async ({ handle, send }) => {
    journal.JournalSurface_dispose(handle)
    let sends = 0
    await assert.rejects(() => send({ SendPrompt: async () => {
      sends += 1
      return dispatch.admittedWithReceipt('must-not-happen')
    } }), /Journal handle is disposed/)
    assert.equal(sends, 0)
  })
})

test.todo('WHAT[effect-accounting-003] actual worktree creation and provider todo mutation are blocked until their own intent commit succeeds')
