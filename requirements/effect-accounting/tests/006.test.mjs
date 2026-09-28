import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { withDispatch, dispatch, journal } from './support/dispatch.mjs'
import * as recovery from '../../../dist/Interaction/Dispatch/RecoverySurface.js'

test('WHAT[effect-accounting-006] a performed Host effect with a lost receipt stays unknown after journal reopen', async () => {
  await withDispatch(async ({ directory, handle, open, send }) => {
    const effectFile = join(directory, 'physical-effect.txt')
    let sends = 0
    const result = await send({ SendPrompt: async () => {
      sends += 1
      writeFileSync(effectFile, 'physically performed')
      return dispatch.acceptanceUnknown('response lost after physical acceptance')
    } })
    assert.equal(result.ok, false)
    assert.match(result.error, /Acceptance unknown/)
    assert.equal(readFileSync(effectFile, 'utf8'), 'physically performed')
    const [claim] = dispatch.projectionObservation(handle, 'child').pendingClaims
    assert.ok(claim)
    journal.JournalSurface_dispose(handle)
    const reopened = await open('recovery-writer')
    assert.equal(dispatch.pendingClaimCount(reopened, 'child'), 1)
    const outcomes = await recovery.reconcile(reopened, [])
    assert.equal(outcomes[0].outcome, 'StillPending')
    assert.equal(outcomes[0].promptKey, claim.promptKey)
    assert.equal(sends, 1)
    assert.equal(readFileSync(effectFile, 'utf8'), 'physically performed')
  })
})

test.todo('WHAT[effect-accounting-006] interruption in a distinct producing process preserves exact unknown settlement for the next process')
