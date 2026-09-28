import assert from 'node:assert/strict'
import test from 'node:test'
import { withDispatch, dispatch } from './support/dispatch.mjs'
import * as recovery from '../../../dist/Interaction/Dispatch/RecoverySurface.js'

test('WHAT[effect-accounting-005] unreadable absent and mismatched physical evidence retain the claim; exact evidence settles without resend', async () => {
  await withDispatch(async ({ handle, send }) => {
    let sends = 0
    await send({ SendPrompt: async () => {
      sends += 1
      return dispatch.acceptanceUnknown('receipt lost')
    } })
    const [claim] = dispatch.projectionObservation(handle, 'child').pendingClaims
    assert.ok(claim)
    const unreadable = await recovery.reconcileWithUnreadableSnapshot(handle, 'Host unavailable')
    assert.equal(unreadable[0].outcome, 'Unreadable')
    for (const messages of [[],
      [{ id: 'wrong-key', role: 'user', metadata: { wanxiangshu_prompt_key: 'another-key' } }],
      [{ id: 'wrong-role', role: 'assistant', metadata: { wanxiangshu_prompt_key: claim.promptKey } }],
    ]) {
      const unresolved = await recovery.reconcile(handle, messages)
      assert.equal(unresolved[0].outcome, 'StillPending')
      assert.equal(dispatch.pendingClaimCount(handle, 'child'), 1)
      assert.equal(sends, 1)
    }
    const physical = { id: 'physical-message', role: 'user', metadata: { wanxiangshu_prompt_key: claim.promptKey } }
    const resolved = await recovery.reconcile(handle, [physical])
    assert.equal(resolved[0].outcome, 'Proven')
    assert.equal(resolved[0].physicalMessageId, physical.id)
    assert.equal(dispatch.pendingClaimCount(handle, 'child'), 0)
    assert.deepEqual(await recovery.reconcile(handle, [physical]), [])
    assert.equal(sends, 1)
  })
})
