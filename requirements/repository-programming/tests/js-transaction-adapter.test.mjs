import assert from 'node:assert/strict'
import test from 'node:test'
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'

import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import {
  run,
  caseName,
  rewritten,
  created,
  failureCode,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { pending } from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const makeDirectory = (prefix) => mkdtempSync(join(tmpdir(), prefix))

const openStore = (commonDir) => {
  mkdirSync(commonDir, { recursive: true })
  const handle = createEventStore(commonDir, randomUUID().replaceAll('-', ''))
  return { handle, close: () => disposeEventStore(handle) }
}

const runWorkflow = (workspaceRoot, store, body) => run(
  workspaceRoot,
  'Coder',
  'en',
  `class Js extends JsProgram {
  async run() {
${body}
  }
}`,
  2000,
  Date.now() + 60_000,
  1 << 20,
  store,
)

const mutationView = (mutation) => ({
  path: mutation.path,
  originalText: mutation.originalText,
  newText: mutation.newText,
})

test('WHAT[REPOSITORY-PROGRAMMING-015] Adapter JS015_MutationFs_commits_atomically_and_reopen_never_blind_retries_an_ambiguous_partial_commit', async () => {
  const workspace = makeDirectory('wxs-js-mutation-adapter-')
  const committedEvents = makeDirectory('wxs-js-mutation-committed-')
  const interruptedEvents = makeDirectory('wxs-js-mutation-interrupted-')

  try {
    writeFileSync(join(workspace, 'first.txt'), 'first:old', 'utf8')
    writeFileSync(join(workspace, 'second.txt'), 'second:old', 'utf8')

    let committedStore = openStore(committedEvents)
    const committed = await runWorkflow(workspace, committedStore.handle, `    this.rewrite('first.txt', 'first:new');
    this.rewrite('second.txt', 'second:new');
    await this.write('created.txt', 'created:new');
    return { receipt: 'multi-file-committed' };`)

    assert.equal(caseName(committed), 'Succeeded', 'workflow returns a committed receipt')
    assert.deepEqual(rewritten(committed), ['first.txt', 'second.txt'])
    assert.deepEqual(created(committed), ['created.txt'])
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'first:new')
    assert.equal(readFileSync(join(workspace, 'second.txt'), 'utf8'), 'second:new')
    assert.equal(readFileSync(join(workspace, 'created.txt'), 'utf8'), 'created:new')
    assert.deepEqual(pending(committedStore.handle), [], 'Committed removes Prepared from Current')
    committedStore.close()

    committedStore = openStore(committedEvents)
    assert.deepEqual(pending(committedStore.handle), [], 'Committed projection survives EventStore reopen')
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'first:new')
    assert.equal(readFileSync(join(workspace, 'second.txt'), 'utf8'), 'second:new')
    assert.equal(readFileSync(join(workspace, 'created.txt'), 'utf8'), 'created:new')
    committedStore.close()

    // The second target snapshots as absent, but its missing parent makes the
    // physical write fail after MutationFs has written the first target. The
    // production adapter must roll that partial effect back and leave Prepared
    // durable because no Committed receipt is safe to claim.
    writeFileSync(join(workspace, 'first.txt'), 'cut:old', 'utf8')
    const interruptedStore = openStore(interruptedEvents)
    const interrupted = await runWorkflow(workspace, interruptedStore.handle, `    this.rewrite('first.txt', 'cut:new');
    await this.write('missing-parent/second.txt', 'must-not-survive');
    return { receipt: 'must-not-commit' };`)

    assert.equal(caseName(interrupted), 'Failed')
    assert.equal(failureCode(interrupted), 'TRANSACTION_COMMIT_FAILED')
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'cut:old', 'partial first write was rolled back')
    assert.equal(existsSync(join(workspace, 'missing-parent/second.txt')), false)

    const waiting = pending(interruptedStore.handle)
    assert.equal(waiting.length, 1, 'Prepared remains durable after ambiguous physical failure')
    assert.equal(waiting[0].workspaceRoot, workspace)
    assert.equal(typeof waiting[0].transactionId, 'string')
    assert.notEqual(waiting[0].transactionId, '')
    assert.deepEqual(waiting[0].mutations.map(mutationView), [
      { path: 'first.txt', originalText: 'cut:old', newText: 'cut:new' },
      { path: 'missing-parent/second.txt', originalText: null, newText: 'must-not-survive' },
    ])
    interruptedStore.close()

    // Make a retry physically possible only after the interrupted process is
    // gone. Reopening Current must observe evidence, never replay the plan.
    mkdirSync(join(workspace, 'missing-parent'))
    const reopenedInterrupted = openStore(interruptedEvents)
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'cut:old')
    assert.equal(existsSync(join(workspace, 'missing-parent/second.txt')), false, 'reopen did not blindly retry')
    assert.deepEqual(
      pending(reopenedInterrupted.handle).map((item) => ({
        transactionId: item.transactionId,
        workspaceRoot: item.workspaceRoot,
        mutations: item.mutations.map(mutationView),
      })),
      waiting.map((item) => ({
        transactionId: item.transactionId,
        workspaceRoot: item.workspaceRoot,
        mutations: item.mutations.map(mutationView),
      })),
      'pending projection is durable and unchanged across reopen',
    )
    reopenedInterrupted.close()
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(committedEvents, { recursive: true, force: true })
    rmSync(interruptedEvents, { recursive: true, force: true })
  }
})
