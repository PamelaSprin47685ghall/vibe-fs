import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, writeFileSync } from 'node:fs'
import { setTimeout as delay } from 'node:timers/promises'
import { withFinitePhysicalWait } from '../finite-physical-wait.mjs'

const mode = process.env.WXS_FINITE_ACK_MODE
const evidence = process.env.WXS_FINITE_ACK_EVIDENCE
const marker = process.env.WXS_FINITE_ACK_MARKER
const options = { operation: `ACK boundary ${mode}`, limitMs: 3000 }
let callbacks = 0

if (mode === 'context-closed-late') {
  let operation
  test('close the context after the real begin is sent', async context => {
    operation = withFinitePhysicalWait(context, options, () => { callbacks += 1 })
      .then(() => ({ fulfilled: true }), error => ({ error }))
    const deadline = performance.now() + 800
    while (!existsSync(marker)) {
      assert.ok(performance.now() < deadline, 'the real begin must reach the socket')
      await delay(5)
    }
  })
  test('a late grant cannot run the closed context', async () => {
    const outcome = await operation
    assert.equal(outcome.fulfilled, undefined)
    assert.match(outcome.error.message, /context closed/)
    await delay(400)
    assert.equal(callbacks, 0)
    writeFileSync(evidence, JSON.stringify({ callbacks }))
  })
} else {
  test(`actual helper ACK boundary ${mode}`, async context => {
    if (mode === 'positive') {
      const value = await withFinitePhysicalWait(context, options, () => {
        callbacks += 1
        return 41
      })
      assert.equal(value, 41)
    } else if (mode === 'end-lost') {
      const original = new Error('original action exception identity')
      await assert.rejects(withFinitePhysicalWait(context, options, () => {
        callbacks += 1
        throw original
      }), error => {
        assert.ok(error instanceof AggregateError)
        assert.equal(error.cause, original)
        assert.equal(error.errors[0], original)
        assert.equal(error.errors.length, 2)
        assert.match(error.errors[1].message, /reply deadline expired/)
        return true
      })
    } else {
      await assert.rejects(withFinitePhysicalWait(context, options, () => { callbacks += 1 }),
        mode === 'refused' ? /controlled refusal/
          : mode === 'wrong-instance' ? /identity mismatch/
          : mode === 'malformed' ? /Invalid physical wait reply/ : /reply deadline expired/)
      if (mode === 'deadline-late') await delay(400)
    }
    await assert.rejects(withFinitePhysicalWait(context,
      { operation: 'no retry after the first exchange', limitMs: 3000 }, () => { callbacks += 1 }),
    /already declared/)
    assert.equal(callbacks, ['positive', 'end-lost'].includes(mode) ? 1 : 0)
    writeFileSync(evidence, JSON.stringify({ callbacks }))
  })
}
