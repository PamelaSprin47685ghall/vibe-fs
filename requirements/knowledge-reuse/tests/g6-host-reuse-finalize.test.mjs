// G6-G Host path: Meditator → same reusable Inspector → Q1/Q2/Q3 → ReuseScope
// close → exactly one CaseFinalize → cold fetch. The delegation runtime stays
// behind its registered owner surface; Casebook lifecycle and persistence use
// their own owner surfaces.

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as syncDelegate from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'

const QUESTIONS = [
  ['Who owns PromptAuthority?', 'Host owns PromptAuthority.'],
  ['Where do Case facts live?', 'Unified EventStore only.'],
  ['When does CaseFinalize run?', 'ReuseScope close, once.'],
]

const waitFor = async (predicate, message, ms = 2000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const settleUntilAccepted = async (runtime, owner, role, answer, runId) => {
  for (let attempt = 0; attempt < 50; attempt += 1) {
    if (await syncDelegate.settle(runtime, owner, role, answer, runId)) return true
    await new Promise((resolve) => setImmediate(resolve))
  }
  return false
}

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_host_reusable_inspector_one_finalize_then_cold_fetch', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-g6-host-reuse-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
  const runtime = await syncDelegate.create(dir)
  const bookkeeperPort = scriptedBookkeeperPort()
  try {
    lifecycle.enable(dir)
    let delegateId

    for (let i = 0; i < QUESTIONS.length; i += 1) {
      const [question, answer] = QUESTIONS[i]
      const pending = syncDelegate.invoke(runtime, 'ses_meditator_g6', 'Inspector', question)
      await waitFor(() => syncDelegate.childCount(runtime) === 1, `Inspector Q${i + 1} did not reuse a single child`)
      const child = syncDelegate.child(runtime, 'ses_meditator_g6', 'Inspector')
      assert.notEqual(child, null)
      await new Promise((resolve) => setImmediate(resolve))
      await new Promise((resolve) => setImmediate(resolve))
      if (i === 0) {
        delegateId = child
      } else {
        assert.equal(child, delegateId, 'GetOrCreate must reuse Inspector session')
      }

      lifecycle.notePrompt(delegateId, question)
      assert.equal(await settleUntilAccepted(runtime, 'ses_meditator_g6', 'Inspector', answer, `asst_q${i + 1}`), true)
      const done = await pending
      assert.equal(done.ok, true, done.ok ? '' : done.error)
      assert.match(String(done.value), new RegExp(answer.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
      lifecycle.noteAnswer(delegateId, answer)
    }

    assert.equal(syncDelegate.childCount(runtime), 1, 'createChild once for reusable Inspector')

    lifecycle.collect(delegateId, 'read', { path: 'a.txt' }, 'hello')
    bookkeeper.setSessionPort(bookkeeperPort.port)
    const first = await lifecycle.tryFinalize(dir, delegateId)
    assert.equal(first.ok, true, `exactly one finalize ok: ${JSON.stringify(first.error)}`)
    assert.equal(bookkeeperPort.createCalls.length, 1, 'exactly one Bookkeeper CreateChildSession')
    assert.equal(bookkeeperPort.programCalls.length >= 1, true, 'js-bookkeeper invoked')

    const store = eventStore.EventStoreSurface_create(join(dir, '.git'), 'g6-host-fetch')
    const published = await casebook.fetchCase(store, 10, delegateId)
    assert.equal(published.ok, true)
    assert.notEqual(published.value, null, 'Case exists after ReuseScope close')
    assert.equal(published.value.sessionId, delegateId)
    assert.equal(published.value.q, CANONICAL_Q)
    assert.equal(published.value.a, CANONICAL_A)
    assert.equal(published.value.a.includes('evidence:'), false)
    assert.equal(published.value.observations.length, 1)

    lifecycle.cleanup(delegateId)
    const cold = await casebook.fetchCase(store, 10, delegateId)
    assert.equal(cold.ok, true)
    assert.notEqual(cold.value, null, 'cleanup must not delete published Case (cold reuse)')
    assert.equal(cold.value.sessionId, delegateId)

    lifecycle.notePrompt(delegateId, 'second finalize must not publish')
    lifecycle.noteAnswer(delegateId, 'should be refused')
    const second = await lifecycle.tryFinalize(dir, delegateId)
    assert.equal(second.ok, false, 'finalize twice is refused')
    assert.match(String(second.error), /already finalized/)

    const still = await casebook.fetchCase(store, 10, delegateId)
    assert.equal(still.value.sessionId, delegateId, 'original Case retained after refused second finalize')
    assert.equal(syncDelegate.childCount(runtime), 1, 'createChild stays once after scope close')
    eventStore.EventStoreSurface_dispose(store)
  } finally {
    bookkeeper.resetSessionPort()
    lifecycle.disable()
    syncDelegate.dispose(runtime)
    rmSync(dir, { recursive: true, force: true })
  }
})
