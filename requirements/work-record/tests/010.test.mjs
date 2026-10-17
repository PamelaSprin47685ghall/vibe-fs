import assert from 'node:assert/strict'
import test from 'node:test'
import { record, commitFrame, withReopenableJournal } from './support/record.mjs'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as fork from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { toolModule } from '../../delegation/tests/support/fork-runtime.mjs'

test('WHAT[work-record-010] repeated bounded materialization of the same durable facts is deterministic', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-repeat'
    await record.captureOpening(handle, session, 'task', [])
    await record.captureProjection(handle, session, { messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'task' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'bounded work\r\n中文' }] },
    ] })
    const range = { StartInclusive: { Sequence: 1 }, EndExclusive: { Sequence: 3 } }
    const first = await record.lifecycleWorkRecordBounded(handle, session, range)
    assert.equal(typeof first, 'string')
    assert.ok(first.includes('bounded work'))
    assert.equal(await record.lifecycleWorkRecordBounded(handle, session, range), first)
    assert.equal(await record.lifecycleWorkRecordBounded(await reopen(), session, range), first)
  })
})

test.todo('WHAT[work-record-010] actual inspect and fork/join return the same bounded record for the same invocation facts; materializer determinism alone does not prove both consumers; GAP-109')

async function withWriter(directory, run) {
  const boot = await journal.JournalSurface_boot(directory, 'bounded-consumer-writer', process.pid, new Date().toISOString())
  assert.equal(boot.ok, true)
  try {
    return await run(boot.journal)
  } finally {
    journal.JournalSurface_dispose(boot.journal)
  }
}

async function observeDetail(handle, session, id, value) {
  const captured = await trace.captureObservedMessages(handle, session, [{ hostMessageId: id, message: {
    info: { id, role: 'assistant' },
    parts: [{ id: `${id}-part`, type: 'text', text: value }],
  } }])
  assert.equal(captured.ok, true, JSON.stringify(captured.error))
}

for (const mode of ['sync', 'fork-join']) {
  test(`WHAT[work-record-010] actual ${mode} consumes only the current invocation across a crossing frame and cold journal replay`, async () => {
    const directory = mkdtempSync(join(tmpdir(), `bounded-${mode}-`))
    execFileSync('git', ['init', '--quiet', directory])
    const owner = `bounded-${mode}-owner`
    const runtime = mode === 'sync'
      ? await sync.create(directory, [{ sessionId: owner, agent: 'manager' }])
      : await fork.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
    const observe = run => mode === 'sync' ? sync.withJournal(runtime, run) : fork.withJournal(runtime, run)
    const invoke = charge => mode === 'sync'
      ? sync.invoke(runtime, owner, 'Engineer', charge)
      : fork.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', charge)
    const accept = async count => {
      if (mode === 'sync') {
        await sync.awaitPromptCount(runtime, owner, 'Engineer', count)
        assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', count - 1), true)
      } else {
        await fork.awaitPromptCount(runtime, count)
        assert.equal(fork.acceptPrompt(runtime, count - 1), true)
      }
    }
    const settle = async (answer, run) => {
      const settled = mode === 'sync'
        ? await sync.settle(runtime, owner, 'Engineer', answer, run)
        : await fork.settle(runtime, owner, answer, run)
      assert.equal(settled, true)
    }
    try {
      const first = invoke('PRIOR_CHARGE')
      await accept(1)
      if (mode === 'fork-join') await first
      await settle('PRIOR_FINAL', 'prior-run')
      const firstResult = mode === 'sync' ? (await first).value : await fork.executeJoin(runtime, owner)
      assert.match(firstResult, /PRIOR_FINAL/)
      const session = mode === 'sync' ? sync.child(runtime, owner, 'Engineer') : fork.child(runtime)
      let borrowed
      const start = await observe(async handle => {
        borrowed = handle
        await observeDetail(handle, session, 'prior-run', 'PRIOR_DETAIL')
        return trace.headCursor(trace.snapshot(handle, session)).sequence
      })
      await assert.rejects(record.lifecycleWorkRecord(borrowed, session, false), /disposed/)
      let failedBorrow
      await assert.rejects(observe(async handle => {
        failedBorrow = handle
        throw new Error('observation rejected')
      }), /observation rejected/)
      await assert.rejects(record.lifecycleWorkRecord(failedBorrow, session, false), /disposed/)
      const second = mode === 'sync'
        ? invoke('CURRENT_CHARGE')
        : fork.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'CURRENT_CHARGE')
      await accept(2)
      if (mode === 'fork-join') await second
      await observe(async handle => {
        await observeDetail(handle, session, 'current-detail', 'CURRENT_DETAIL\r\n正文')
        const head = trace.headCursor(trace.snapshot(handle, session)).sequence
        assert.ok(head > start)
        await commitFrame(handle, session, {
          from: 0, through: head - 1,
          body: 'PRIOR_DETAIL CURRENT_DETAIL CROSSING_SUMMARY', id: 'crossing',
        })
      })
      await settle('CURRENT_FINAL', 'current-run')
      const result = mode === 'sync' ? (await second).value : await fork.executeJoin(runtime, owner)
      assert.match(result, /CURRENT_DETAIL/)
      assert.match(result, /正文/)
      assert.match(result, /CURRENT_FINAL/)
      assert.doesNotMatch(result, /PRIOR_|CURRENT_CHARGE|CROSSING_SUMMARY/)
      assert.equal(mode === 'sync' ? sync.childCount(runtime) : fork.childCount(runtime), 1)
      const cold = await withWriter(directory, async handle => {
        const finish = trace.headCursor(trace.snapshot(handle, session)).sequence
        return record.lifecycleWorkRecordBounded(handle, session, {
          StartInclusive: { Sequence: start }, EndExclusive: { Sequence: finish }, ProviderRun: 'current-run',
        })
      })
      assert.equal(typeof cold, 'string')
      if (mode === 'sync') assert.equal(result, cold)
      else {
        const expected = cold.replace(/\r\n/g, '\n').split('\n').map(line => line ? `# ${line}` : '#').join('\n')
        assert.ok(result.includes(expected), 'Join must carry the canonical bounded record as instruction comments')
      }
      assert.equal(await withWriter(directory, handle => record.lifecycleWorkRecordBounded(handle, session, {
        StartInclusive: { Sequence: start },
        EndExclusive: { Sequence: trace.headCursor(trace.snapshot(handle, session)).sequence },
        ProviderRun: 'current-run',
      })), cold)
    } finally {
      if (mode === 'sync') sync.dispose(runtime)
      else await fork.disposeRuntime(runtime)
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

for (const delivery of ['join', 'cold-consume']) {
  test(`WHAT[work-record-010] actual Fork completion survives later squash through ${delivery}`, async () => {
    const directory = mkdtempSync(join(tmpdir(), `bounded-frozen-${delivery}-`))
    execFileSync('git', ['init', '--quiet', directory])
    const owner = `frozen-${delivery}-owner`
    let runtime = await fork.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
    try {
      const first = fork.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'PRIOR_CHARGE')
      await fork.awaitPromptCount(runtime, 1)
      assert.equal(fork.acceptPrompt(runtime, 0), true)
      await first
      assert.equal(await fork.settle(runtime, owner, 'PRIOR_FINAL', 'prior-run'), true)
      assert.match(await fork.executeJoin(runtime, owner), /PRIOR_FINAL/)
      const session = fork.child(runtime)
      const start = await fork.withJournal(runtime, async handle => {
        await observeDetail(handle, session, 'prior-detail', 'PRIOR_DETAIL')
        return trace.headCursor(trace.snapshot(handle, session)).sequence
      })
      const second = fork.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'CURRENT_CHARGE')
      await fork.awaitPromptCount(runtime, 2)
      assert.equal(fork.acceptPrompt(runtime, 1), true)
      await second
      const covered = await fork.withJournal(runtime, async handle => {
        await observeDetail(handle, session, 'current-detail', 'CURRENT_DETAIL\r\n正文')
        const through = trace.headCursor(trace.snapshot(handle, session)).sequence - 1
        await commitFrame(handle, session, { from: 0, through: through - 1, body: 'PRIOR_SUMMARY', id: 'prior' })
        await commitFrame(handle, session, { from: through - 1, through, body: 'CURRENT_SUMMARY', id: 'current' })
        return through
      })
      assert.equal(await fork.settle(runtime, owner, 'CURRENT_FINAL', 'current-run'), true)
      const work = fork.workSnapshot(runtime, owner).find(cell => cell.lifecycle === 'CompletedAwaitingJoin')
      assert.ok(work?.completionRef)
      const completed = await fork.withJournal(runtime, handle => journal.JournalSurface_readPayload(handle, work.completionRef))
      assert.equal(completed.ok, true)
      assert.equal(createHash('sha256').update(completed.content).digest('hex'), work.completionDigest)
      const frozen = JSON.parse(completed.content).work_record
      assert.match(frozen, /CURRENT_SUMMARY/)
      assert.match(frozen, /CURRENT_DETAIL\r\n正文/)
      assert.match(frozen, /CURRENT_FINAL/)
      assert.doesNotMatch(frozen, /PRIOR_|CURRENT_CHARGE/)
      const range = await fork.withJournal(runtime, async handle => ({
        StartInclusive: { Sequence: start },
        EndExclusive: { Sequence: trace.headCursor(trace.snapshot(handle, session)).sequence },
        ProviderRun: 'current-run',
      }))
      assert.equal(await fork.withJournal(runtime, handle => record.lifecycleWorkRecordBounded(handle, session, range)), frozen)
      await fork.withJournal(runtime, async handle => {
        await observeDetail(handle, session, 'future-detail', 'FUTURE_DETAIL')
        const through = trace.headCursor(trace.snapshot(handle, session)).sequence - 1
        await commitFrame(handle, session, { from: covered, through, body: 'FUTURE_SUMMARY', id: 'future' })
        const blob = await journal.JournalSurface_writePayload(handle, 'PRIOR_DETAIL CURRENT_DETAIL FUTURE_DETAIL SQUASHED_SUMMARY')
        assert.equal(blob.ok, true)
        const squashed = await record.appendBlogSquash(handle, session, 'squash-run', {
          bloggerSessionId: `blog-${session}`, requestId: 'squash-request',
          previousFrameEpoch: 0, nextFrameEpoch: 1, coveredFrameCount: 3,
          textRef: blob.blobRef, textDigest: blob.blobDigest,
        })
        assert.equal(squashed.ok, true, squashed.error)
      })
      const derived = await fork.withJournal(runtime, handle => record.lifecycleWorkRecordBounded(handle, session, range))
      assert.notEqual(derived, frozen)
      assert.match(derived, /CURRENT_DETAIL\r\n正文/)
      assert.match(derived, /CURRENT_FINAL/)
      assert.doesNotMatch(derived, /PRIOR_|FUTURE_|SQUASHED_SUMMARY|CURRENT_SUMMARY/)
      assert.deepEqual(await fork.withJournal(runtime, handle => journal.JournalSurface_readPayload(handle, work.completionRef)), completed)
      if (delivery === 'join') {
        const joined = await fork.executeJoin(runtime, owner)
        const expected = frozen.replace(/\r\n/g, '\n').split('\n').map(line => line ? `# ${line}` : '#').join('\n')
        assert.ok(joined.includes(expected), joined)
        assert.doesNotMatch(joined, /PRIOR_|FUTURE_|SQUASHED_SUMMARY/)
        assert.equal(fork.workSnapshot(runtime, owner).find(cell => cell.root === work.root).lifecycle, 'Retired')
      } else {
        await fork.disposeRuntime(runtime)
        runtime = undefined
        const cold = await withWriter(directory, handle => journal.JournalSurface_readPayload(handle, work.completionRef))
        assert.deepEqual(cold, completed)
        const consumed = await fork.coldConsumeWorkWithOutcome(directory, owner, work.root, 'confirmed')
        assert.equal(consumed.ok, true, JSON.stringify(consumed))
        assert.equal(consumed.workRecord, frozen)
      }
    } finally {
      if (runtime) await fork.disposeRuntime(runtime)
      rmSync(directory, { recursive: true, force: true })
    }
  })
}
