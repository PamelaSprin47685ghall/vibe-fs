import assert from 'node:assert/strict'
import test from 'node:test'
import { record, commitFrame, withReopenableJournal, readBoundedInNewProcess } from './support/record.mjs'
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
  test(`WHAT[work-record-010] actual ${mode} returns the delivered value while scoped history stays bounded across cold replay`, async () => {
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
      assert.match(result, /CURRENT_FINAL/)
      assert.doesNotMatch(result, /PRIOR_|CURRENT_DETAIL|CURRENT_CHARGE|CROSSING_SUMMARY/)
      assert.equal(mode === 'sync' ? sync.childCount(runtime) : fork.childCount(runtime), 1)
      const cold = await withWriter(directory, async handle => {
        const finish = trace.headCursor(trace.snapshot(handle, session)).sequence
        return record.lifecycleWorkRecordBounded(handle, session, {
          StartInclusive: { Sequence: start }, EndExclusive: { Sequence: finish }, ProviderRun: 'current-run',
        })
      })
      assert.equal(typeof cold, 'string')
      assert.match(cold, /CURRENT_DETAIL/)
      assert.match(cold, /正文/)
      assert.match(cold, /CURRENT_FINAL/)
      assert.doesNotMatch(cold, /PRIOR_|CURRENT_CHARGE|CROSSING_SUMMARY/)
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
  test(`WHAT[work-record-010] actual Fork delivery and bounded history survive later originals and frames through ${delivery}`, async () => {
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
      fork.recordDeliveredValue(session, { summary: 'CURRENT_DELIVERED', files: 2 })
      assert.equal(await fork.settle(runtime, owner, 'CURRENT_FINAL', 'current-run'), true)
      const work = fork.workSnapshot(runtime, owner).find(cell => cell.lifecycle === 'CompletedAwaitingJoin')
      assert.ok(work?.completionRef)
      const completed = await fork.withJournal(runtime, handle => journal.JournalSurface_readPayload(handle, work.completionRef))
      assert.equal(completed.ok, true)
      assert.equal(createHash('sha256').update(completed.content).digest('hex'), work.completionDigest)
      const frozen = JSON.parse(completed.content).work_record
      assert.match(frozen, /summary = "CURRENT_DELIVERED"/)
      assert.match(frozen, /files = 2/)
      assert.doesNotMatch(frozen, /PRIOR_|CURRENT_CHARGE|CURRENT_DETAIL|CURRENT_SUMMARY|CURRENT_FINAL/)
      const range = await fork.withJournal(runtime, async handle => ({
        StartInclusive: { Sequence: start },
        EndExclusive: { Sequence: trace.headCursor(trace.snapshot(handle, session)).sequence },
        ProviderRun: 'current-run',
      }))
      const bounded = await fork.withJournal(runtime, handle => record.lifecycleWorkRecordBounded(handle, session, range))
      assert.match(bounded, /CURRENT_SUMMARY/)
      assert.match(bounded, /CURRENT_DETAIL\r\n正文/)
      assert.match(bounded, /CURRENT_FINAL/)
      assert.doesNotMatch(bounded, /PRIOR_|CURRENT_CHARGE/)
      await fork.withJournal(runtime, async handle => {
        await observeDetail(handle, session, 'future-detail', 'FUTURE_DETAIL')
        const through = trace.headCursor(trace.snapshot(handle, session)).sequence - 1
        await commitFrame(handle, session, { from: covered, through, body: 'FUTURE_SUMMARY', id: 'future' })
      })
      const derived = await fork.withJournal(runtime, handle => record.lifecycleWorkRecordBounded(handle, session, range))
      assert.equal(derived, bounded)
      assert.match(derived, /CURRENT_DETAIL\r\n正文/)
      assert.match(derived, /CURRENT_FINAL/)
      assert.doesNotMatch(derived, /PRIOR_|FUTURE_/)
      assert.match(await fork.withJournal(runtime, handle => record.lifecycleWorkRecord(handle, session, false)), /FUTURE_SUMMARY/)
      assert.deepEqual(await fork.withJournal(runtime, handle => journal.JournalSurface_readPayload(handle, work.completionRef)), completed)
      if (delivery === 'join') {
        const joined = await fork.executeJoin(runtime, owner)
        const expected = frozen.replace(/\r\n/g, '\n').split('\n').map(line => line ? `# ${line}` : '#').join('\n')
        assert.ok(joined.includes(expected), joined)
        assert.doesNotMatch(joined, /PRIOR_|FUTURE_|CURRENT_DETAIL|CURRENT_FINAL/)
        assert.equal(fork.workSnapshot(runtime, owner).find(cell => cell.root === work.root).lifecycle, 'Retired')
      } else {
        await fork.disposeRuntime(runtime)
        runtime = undefined
        const cold = await withWriter(directory, handle => journal.JournalSurface_readPayload(handle, work.completionRef))
        assert.deepEqual(cold, completed)
        assert.equal(await withWriter(directory, handle => record.lifecycleWorkRecordBounded(handle, session, range)), bounded)
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

for (const mode of ['sync', 'fork-join']) {
  test(`WHAT[work-record-010] actual ${mode} keeps delivery and the historical terminal frontier separate from later trace`, async () => {
    const directory = mkdtempSync(join(tmpdir(), `completed-frontier-${mode}-`))
    execFileSync('git', ['init', '--quiet', directory])
    const owner = `completed-frontier-${mode}-owner`
    const runtime = mode === 'sync'
      ? await sync.create(directory, [{ sessionId: owner, agent: 'manager' }])
      : await fork.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
    const observe = run => mode === 'sync' ? sync.withJournal(runtime, run) : fork.withJournal(runtime, run)
    const accept = async count => {
      if (mode === 'sync') {
        await sync.awaitPromptCount(runtime, owner, 'Engineer', count)
        assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', count - 1), true)
      } else {
        await fork.awaitPromptCount(runtime, count)
        assert.equal(fork.acceptPrompt(runtime, count - 1), true)
      }
    }
    try {
      const first = mode === 'sync'
        ? sync.invoke(runtime, owner, 'Engineer', 'PRIOR_CHARGE')
        : fork.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'PRIOR_CHARGE')
      await accept(1)
      if (mode === 'sync') {
        assert.equal(await sync.settle(runtime, owner, 'Engineer', 'PRIOR_FINAL', 'prior-run'), true)
        assert.match((await first).value, /PRIOR_FINAL/)
      } else {
        await first
        assert.equal(await fork.settle(runtime, owner, 'PRIOR_FINAL', 'prior-run'), true)
        assert.match(await fork.executeJoin(runtime, owner), /PRIOR_FINAL/)
      }
      const session = mode === 'sync' ? sync.child(runtime, owner, 'Engineer') : fork.child(runtime)
      const start = await observe(async handle => {
        await observeDetail(handle, session, 'prior-detail', 'PRIOR_DETAIL')
        return trace.headCursor(trace.snapshot(handle, session)).sequence
      })
      const second = mode === 'sync'
        ? sync.invoke(runtime, owner, 'Engineer', 'CURRENT_CHARGE')
        : fork.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'CURRENT_CHARGE')
      await accept(2)
      if (mode === 'fork-join') await second
      const covered = await observe(async handle => {
        await observeDetail(handle, session, 'current-detail', 'CURRENT_DETAIL\r\n正文')
        const through = trace.headCursor(trace.snapshot(handle, session)).sequence - 1
        await commitFrame(handle, session, { from: 0, through: through - 1, body: 'PRIOR_SUMMARY', id: 'prior' })
        await commitFrame(handle, session, { from: through - 1, through, body: 'CURRENT_SUMMARY', id: 'current' })
        return through
      })
      let deliver
      if (mode === 'sync') {
        sync.recordDeliveredValue(session, 'CURRENT_FINAL')
        await observe(handle => record.captureTerminalText(handle, session, 'CURRENT_FINAL', 'current-run'))
        deliver = () => sync.settle(runtime, owner, 'Engineer', 'CURRENT_FINAL', 'current-run')
      } else {
        deliver = await fork.prepareTerminalDelivery(runtime, owner, 'CURRENT_FINAL', 'current-run')
      }
      const range = await observe(async handle => ({
        StartInclusive: { Sequence: start },
        EndExclusive: { Sequence: trace.terminalEvidenceForProviderRun('current-run', trace.snapshot(handle, session)).frontier.sequence },
        ProviderRun: 'current-run',
      }))
      const expected = await observe(handle => record.lifecycleWorkRecordBounded(handle, session, range))
      assert.match(expected, /CURRENT_SUMMARY/)
      assert.match(expected, /CURRENT_DETAIL\r\n正文/)
      assert.match(expected, /CURRENT_FINAL/)
      await observe(async handle => {
        await observeDetail(handle, session, 'future-detail', 'FUTURE_DETAIL')
        const through = trace.headCursor(trace.snapshot(handle, session)).sequence - 1
        assert.ok(through >= range.EndExclusive.Sequence)
        await commitFrame(handle, session, { from: covered, through, body: 'FUTURE_SUMMARY', id: 'future' })
        await record.captureTerminalText(handle, session, 'FUTURE_FINAL', 'future-run')
        assert.match(await record.lifecycleWorkRecord(handle, session, false), /FUTURE_SUMMARY/)
      })
      await deliver()
      if (mode === 'sync') assert.equal((await second).value, 'CURRENT_FINAL')
      else {
        const work = fork.workSnapshot(runtime, owner).find(cell => cell.lifecycle === 'CompletedAwaitingJoin')
        const blob = await observe(handle => journal.JournalSurface_readPayload(handle, work.completionRef))
        assert.equal(blob.ok, true)
        assert.equal(JSON.parse(blob.content).work_record, 'CURRENT_FINAL')
        const joined = await fork.executeJoin(runtime, owner)
        assert.match(joined, /CURRENT_FINAL/)
        assert.doesNotMatch(joined, /CURRENT_DETAIL|CURRENT_SUMMARY|PRIOR_|FUTURE_/)
      }
      assert.doesNotMatch(expected, /PRIOR_|FUTURE_|CURRENT_CHARGE/)
      assert.equal(await observe(handle => record.lifecycleWorkRecordBounded(handle, session, range)), expected)
      assert.equal(readBoundedInNewProcess(directory, session, range), expected)
    } finally {
      if (mode === 'sync') sync.dispose(runtime)
      else await fork.disposeRuntime(runtime)
      rmSync(directory, { recursive: true, force: true })
    }
  })
}
