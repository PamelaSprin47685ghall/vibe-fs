import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

test('WHAT[delegation-007] new sync invocations reject historical Coder and Inspector without creating or prompting a child', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-retired-sync-'))
  const owner = 'retired-role-parent'
  const runtime = await sync.create(directory, [{ sessionId: owner, agent: 'manager' }])
  try {
    for (const role of ['Coder', 'Inspector']) {
      const result = await sync.invoke(runtime, owner, role, 'new research')
      assert.equal(result.ok, false)
      assert.match(result.error, /retired sync delegate role/)
      const batch = await sync.invokeBatch(runtime, owner, role, 'new batch research', 'retired-run', 'call-one', ['call-one'])
      assert.equal(batch.kind, 'Error')
      assert.match(batch.error, /retired sync delegate role/)
      assert.equal(sync.childCount(runtime), 0)
      assert.equal(sync.promptCount(runtime, owner, role), 0)
    }
    const work = sync.invoke(runtime, owner, 'Engineer', 'current research')
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', 0), true)
    assert.equal(await sync.settle(runtime, owner, 'Engineer', 'local facts', 'run-current'), true)
    assert.equal((await work).ok, true)
  } finally {
    sync.dispose(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test.todo('WHAT[delegation-007] the Sphinx program invokes standard Engineer through an acyclic managed path with family-root placement and exact terminal identity, without extra DevOps authority (GAP-153)')
