import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'
import {
  CANONICAL_A,
  installBookkeeperRuntime,
  scriptedBookkeeperPort,
} from '../../knowledge-reuse/tests/support/bookkeeper-session-support.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delegate-settle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return {
    dir,
    cleanup: () => rmSync(dir, { recursive: true, force: true }),
  }
}

test('WHAT[managed-session-lifecycle-022] CASE_SETTLE_uncommitted_finalize_does_not_publish_a_case', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    // No Bookkeeper runtime installed: the CaseFinalize transaction cannot be
    // attempted. The finalize is NotCommitted — the identity is retained for
    // resume and no half-published Case exists.
    const key = 'insp-settle-uncommitted'
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.noteAnswer(key, 'Host owns PromptAuthority.')

    const settled = await lifecycle.tryFinalize(dir, key)
    assert.equal(settled.ok, false)
    assert.match(String(settled.error), /bookkeeper runtime unavailable/)

    const handle = eventStore.create(join(dir, '.git'), 'insp-settle-uncommitted-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.equal(fetched.value, null)
    } finally {
      eventStore.dispose(handle)
    }
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})
