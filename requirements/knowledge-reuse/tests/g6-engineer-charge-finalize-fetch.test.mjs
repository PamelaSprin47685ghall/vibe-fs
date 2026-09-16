// engineer charge → registered SyncDelegateSurface → lifecycle → Bookkeeper → fetch.
// EngineerCharge and the reusable runtime remain owner-private; assertions stay
// on provider-visible TOML and durable Casebook output.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as syncDelegate from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import {
  CANONICAL_A,
  CANONICAL_Q,
  installBookkeeperRuntime,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'

const QUESTIONS = [
  ['Who owns PromptAuthority?', 'Host owns PromptAuthority.'],
  ['Where do Case facts live?', 'Unified EventStore only.'],
  ['When does CaseFinalize run?', 'ReuseScope close, once.'],
]
test('WHAT[KNOWLEDGE-REUSE-010] G6_engineer_charge_sync_delegate_lifecycle_bookkeeper_fetch', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-g6-engineer-charge-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
  const owner = 'ses_meditator_engineer_charge'
  const runtime = await syncDelegate.create(dir, [{ sessionId: owner, agent: 'manager' }])
  const bookkeeperPort = scriptedBookkeeperPort()
  try {
    lifecycle.enable(dir)
    let delegateId

    for (let i = 0; i < QUESTIONS.length; i += 1) {
      const [question, answer] = QUESTIONS[i]
      const pending = syncDelegate.executeEngineerCharge(runtime, owner, question)
      await syncDelegate.awaitPromptCount(runtime, owner, 'Engineer', i + 1)
      assert.equal(syncDelegate.acceptPrompt(runtime, owner, 'Engineer', i), true)
      assert.equal(syncDelegate.childCount(runtime), 1, `EngineerCharge Q${i + 1} did not reuse a single child`)
      const child = syncDelegate.child(runtime, owner, 'Engineer')
      assert.notEqual(child, null, 'Engineer child must be attached')
      if (i === 0) {
        delegateId = child
      } else {
        assert.equal(child, delegateId, 'GetOrCreate must reuse the Engineer session')
      }

      lifecycle.notePrompt(delegateId, question)
      assert.equal(await syncDelegate.settle(runtime, owner, 'Engineer', answer, `asst_q${i + 1}`), true)
      const text = await pending
      assert.match(text, /Recent work/, `Engineer Q${i + 1} must return the bounded Recent work section`)
      assert.match(text, new RegExp(answer.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
      for (let prior = 0; prior < i; prior += 1) {
        assert.equal(
          text.includes(QUESTIONS[prior][1]),
          false,
          `Engineer Q${i + 1} must not leak Q${prior + 1} terminal prose into its bounded record`,
        )
      }
      assert.equal(parseToml(text).error, undefined)
      lifecycle.noteAnswer(delegateId, answer)
    }

    assert.equal(syncDelegate.childCount(runtime), 1, 'Engineer CreateChildSession once')
    lifecycle.collect(delegateId, 'read', { path: 'a.txt' }, 'hello')

    installBookkeeperRuntime(bookkeeperPort.port, [delegateId])
    const first = await lifecycle.tryFinalize(dir, delegateId)
    assert.equal(first.ok, true, `tryFinalize ok: ${JSON.stringify(first.error)}`)
    assert.equal(bookkeeperPort.createCalls.length, 1, 'detached Bookkeeper sibling lane once')
    assert.equal(bookkeeperPort.programCalls.length >= 1, true, 'js-bookkeeper must reshape Q and A in one program')
    assert.equal(bookkeeperPort.prompts.some((text) => String(text).includes('CaseFinalize')), true)
    assert.equal(bookkeeperPort.prompts.some((text) => String(text).includes('Q1')), true)
    assert.equal(bookkeeperPort.prompts.some((text) => String(text).includes('Q3')), true)

    const store = eventStore.create(join(dir, '.git'), 'g6-engineer-charge-fetch')
    const fetched = await casebook.fetchCase(store, 10, delegateId)
    assert.equal(fetched.ok, true)
    assert.notEqual(fetched.value, null, 'Case exists after finalize')
    assert.equal(fetched.value.sessionId, delegateId)
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.notEqual(fetched.value.q, QUESTIONS[2][0], 'fetch must not return the last Engineer Q')
    assert.equal(fetched.value.a, CANONICAL_A)
    assert.equal(String(fetched.value.a).includes('evidence:'), false)
    assert.equal(String(fetched.value.a).includes('digest'), false)
    eventStore.dispose(store)
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    syncDelegate.dispose(runtime)
    rmSync(dir, { recursive: true, force: true })
  }
})
