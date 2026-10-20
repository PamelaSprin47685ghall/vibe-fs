// WHAT[effect-accounting-013] 交付成功不是效果。
//
// 取证面（全部是真实实现，没有自写模型）：
//   * 效果账：Change 的分型记账（Requested 意图 / Created 物理确认）——
//     dist/Change/Surface.js；
//   * 交付见证：真实的 durable journal（Host 家族 TaskDeliveryCommitted）——
//     dist/Composition/Durable/DelegationJournalAdapter.js；
//   * 终态：真实的终态发布（Published / RejectedUndelivered）——
//     dist/Context/Trace/TerminalReporter.js；
//   * 交付格：进程内的带围栏交付格 —— dist/Repository/Programming/Js/RuntimeSurface.js。
//
// 断言全部打在可观测行为上：见证被提交 / 被读取 / 被拒绝，终态是否发布，
// 效果账上的分型状态，以及账上事实的家族与用例名。

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import * as deliveryJournal from '../../../dist/Composition/Durable/DelegationJournalAdapter.js'
import * as hostSurface from '../../../dist/Change/Host/Surface.js'
import { ReconciledTurn } from '../../../dist/Composition/Turn/Observation.js'
import { TurnOutcome } from '../../../dist/Composition/Turn/Program.js'
import { XTraceTerminalCompletion, TerminalReporter_completeWithEvidence } from '../../../dist/Context/Trace/TerminalReporter.js'
import * as hostFacts from '../../../dist/Host/Facts.js'
import * as identity from '../../../dist/Foundation/Identity.js'
import { Role } from '../../../dist/Foundation/Roles.js'
import { MessagePart } from '../../../dist/OpenCode/Host/Message.js'
import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as jsRuntime from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const SESSION = 'sess-effect-013'
const RUN = 'run-effect-013'
const WRITER = 'writer-effect-013'
const WORKTREE = 'manager/job-effect-013'

// 一个真实的 journal：见证提交、终态裁决与恢复都读它。
const withJournal = async (scenario) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-effect013-'))
  const handles = []
  try {
    const opened = await journalSurface.JournalSurface_bootWithWriterId(
      directory,
      WRITER,
      'runtime-effect-013',
      4242,
      '9999-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true, JSON.stringify(opened.error))
    handles.push(opened.journal)
    await scenario({
      directory,
      handle: opened.journal,
      journal: journalSurface.JournalHandle__get_Journal(opened.journal),
    })
  } finally {
    for (const handle of handles) journalSurface.JournalSurface_dispose(handle)
    rmSync(directory, { recursive: true, force: true })
  }
}

// 一份真实的交付见证：围栏（session, providerRun, toolCallId）+ 交付正文 +
// view 声明 + 物化后的 view + 材料摘要。
const deliveryWitness = ({ session, run, call, view, material }) =>
  new hostFacts.TaskDeliveryWitness(
    new hostFacts.TaskDeliveryFence(
      identity.SessionIdModule_create(session),
      identity.LogicalRunIdModule_create('logical-run-effect-013'),
      identity.AuthorityRootUserMessageIdModule_create('authority-root-effect-013'),
      identity.PhysicalUserMessageIdModule_create('physical-effect-013'),
      identity.ProviderRunIdentityModule_create(run),
      identity.ToolCallIdModule_create(call),
      new hostFacts.TaskDeliveryTask(0, ['handle-effect-013']),
    ),
    'the delivered formal prose',
    '{"prefixUntil":0}',
    view,
    material ?? createHash('sha256').update(view).digest('hex'),
    undefined,
  )

// 交付是否成立，只由 durable 见证决定 —— 终态裁决读的就是它。
const deliveryEstablished = (journal, session, run) =>
  deliveryJournal.TaskDeliveryJournal_tryFind(
    journal,
    identity.SessionIdModule_create(session),
    identity.ProviderRunIdentityModule_create(run),
  ) !== undefined

// journal 里的事实家族（payload.Fact = ["Agent", [family, [case, payload]]]）。
const journalFacts = (directory, writer) => {
  const eventsDir = join(directory, 'wanxiangshu', 'events')
  if (!existsSync(eventsDir)) return []
  return readdirSync(eventsDir)
    .filter((file) => file.startsWith(writer))
    .flatMap((file) => readFileSync(join(eventsDir, file), 'utf8').split('\n').filter(Boolean))
    .map((line) => JSON.parse(line).payload.Fact)
}

// 效果事实与交付事实分属不同家族：交付落在 Host 家族，效果落在 Orchestrator
// 家族。交付不是效果，所以两者互不替代。
const effectFactsOf = (facts) =>
  facts.filter((fact) => fact[1][0] === 'Orchestrator').map((fact) => fact[1][1][0])
const deliveryFactsOf = (facts) =>
  facts.filter((fact) => fact[1][0] === 'Host' && fact[1][1][0] === 'TaskDeliveryCommitted')

// 把 journal 里的效果事实喂回真实的效果 fold，得到账上那笔效果的分型状态
// （Requested 意图 / Created 物理确认）。
const effectLedgerOf = (facts) => {
  const events = facts
    .filter((fact) => fact[1][0] === 'Orchestrator' && fact[1][1][0].startsWith('Worktree'))
    .map((fact) => ({
      kind: fact[1][1][0],
      payload: {
        jobId: fact[1][1][1].ManagerJobId?.[1],
        worktreeIdentity: fact[1][1][1].WorktreeIdentity?.[1],
        worktreePath: fact[1][1][1].WorktreePath?.[1],
      },
    }))
  const folded = change.fold(events)
  return { ok: folded.ok, error: folded.error, ledger: change.unwrapFold(folded) }
}

// 真实的编排面：它在 journal 上写下效果的请求意图（WorktreeCreateRequested）
// 与物理确认（WorktreeCreated）——效果账因此真的有一笔「已确认」。
const gitPortStub = () => ({
  IsDirty: async () => ({ ok: true, value: false }),
  FreezeTargetBranch: async () => ({ ok: true, value: 'refs/heads/main' }),
  ListManagerBranches: async () => ({ ok: true, value: [] }),
  DeleteBranch: async () => ({ ok: true }),
  HasRebaseHead: async () => false,
  ReadHead: async () => ({ ok: true, value: 'head' }),
  GetTargetHead: async () => ({ ok: true, value: 'target-head' }),
  CreateWorktree: async () => ({ ok: true, value: WORKTREE }),
  ListWorktrees: async () => ({ ok: true, value: [] }),
  RemoveWorktree: async () => ({ ok: true }),
})

const sessionPortStub = () => ({
  SubscribeTerminal: () => ({ Dispose() {} }),
  SubscribeFutureTerminal: () => ({ Dispose() {} }),
  CreateChildSession: async () => ({ ok: false, error: 'manager session host unavailable' }),
})

const recordRealEffect = async (handle, directory) => {
  const host = hostSurface.create({
    sessions: sessionPortStub(),
    journal: handle,
    gitPort: gitPortStub(),
    repoPath: directory,
    targetBranch: 'refs/heads/main',
    orchestratorId: 'ses_orchestrator_effect_013',
  })
  try {
    await hostSurface.forkManagerJob(host, 'job-effect-013', 'manager', 'root request')
  } catch {
    // The manager stage of the fork may fail; the worktree evidence chain is
    // what this test observes, and it is written before that stage.
  }
}

// 一次 provider 停止：助手散文 + stop。
const terminalTurn = (session, run, prose) =>
  new ReconciledTurn(
    identity.SessionIdModule_create(session),
    identity.PhysicalUserMessageIdModule_create('physical-effect-013'),
    identity.AuthorityRootUserMessageIdModule_create('authority-root-effect-013'),
    identity.ProviderRunIdentityModule_create(run),
    Role.Engineer,
    '/workspace',
    [new MessagePart(0, [prose])],
    'stop',
    undefined,
    undefined,
    TurnOutcome.TurnCompleted,
    undefined,
  )

// 终态观察口：只有真实发布才会通知。
const terminalObserver = () => {
  const eventPort = eventsSurface.create()
  const notified = []
  eventPort.SubscribeTerminalListener((sid, outcome) => notified.push({ sid, outcome }))
  return { eventPort, notified }
}

test('WHAT[effect-accounting-013] a committed delivery is not an effect confirmation: the effect ledger is unchanged and the effect keeps its own confirmation', async () => {
  await withJournal(async ({ directory, handle, journal }) => {
    // 效果账先真的记下一笔效果：请求意图在前，物理确认在后。
    await recordRealEffect(handle, directory)
    const effectsBefore = effectFactsOf(journalFacts(directory, WRITER))
    assert.deepEqual(
      effectsBefore,
      ['WorktreeCreateRequested', 'WorktreeCreated'],
      'the effect is accounted with its own intent and its own confirmation',
    )
    const ledgerBefore = effectLedgerOf(journalFacts(directory, WRITER))
    assert.equal(ledgerBefore.ok, true, ledgerBefore.error)
    assert.equal(
      change.worktreeEffect(ledgerBefore.ledger, WORKTREE),
      'Created',
      'the effect on the ledger is confirmed by its own physical confirmation',
    )

    // 交付成立：真实的 durable 见证被提交。
    const committed = await deliveryJournal.TaskDeliveryJournal_commit(
      journal,
      deliveryWitness({ session: SESSION, run: RUN, call: 'call-013', view: 'the delivered material' }),
    )
    assert.equal(committed.tag, 0, 'a valid delivery witness must commit')
    assert.equal(deliveryEstablished(journal, SESSION, RUN), true)

    // 交付成立只说明工作已提交：它没有把任何效果变成「已确认」。
    const factsAfter = journalFacts(directory, WRITER)
    const ledgerAfter = effectLedgerOf(factsAfter)
    assert.equal(ledgerAfter.ok, true, ledgerAfter.error)
    assert.equal(
      change.worktreeEffect(ledgerAfter.ledger, WORKTREE),
      'Created',
      'a committed delivery must not change the effect on the ledger',
    )
    assert.equal(
      effectFactsOf(factsAfter).filter((name) => name === 'WorktreeCreated').length,
      1,
      'the confirmed effect count must not grow because of a delivery',
    )
    assert.deepEqual(
      effectFactsOf(factsAfter),
      effectsBefore,
      'a delivery adds no effect fact to the ledger',
    )
    assert.equal(
      deliveryFactsOf(factsAfter).length,
      1,
      'the delivery is recorded as a delivery fact, never as an effect',
    )

    // 效果另需自己的确认证据：[001] 的分型账上，只有单独的物理确认才是
    // 「已确认」。
    const intentOnly = change.requestWorktree(change.empty(), 'worktree-013', '/repo/worktree-013', 'job-013')
    assert.equal(change.worktreeEffect(intentOnly, 'worktree-013'), 'Requested')
    const confirmed = change.acceptWorktree(intentOnly, 'worktree-013', '/repo/worktree-013', 'job-013')
    assert.equal(change.worktreeEffect(confirmed, 'worktree-013'), 'Created')
  })
})

test('WHAT[effect-accounting-013] a refused delivery leaves no successful effect and is never summarised as confirmed', async () => {
  await withJournal(async ({ directory, journal }) => {
    const ledger = change.requestWorktree(change.empty(), 'worktree-013', '/repo/worktree-013', 'job-013')

    // 交付未成立：真实的提交入口拒绝这份见证（材料摘要与 view 不符）。
    const refused = await deliveryJournal.TaskDeliveryJournal_commit(
      journal,
      deliveryWitness({
        session: SESSION,
        run: RUN,
        call: 'call-013',
        view: 'the delivered material',
        material: 'not-the-material-digest',
      }),
    )
    assert.equal(refused.tag, 1, 'a delivery whose material does not match its view must be refused')
    assert.match(String(refused.fields[0]), /material digest mismatch/)

    // 见证没有落账：交付未成立。
    assert.equal(deliveryEstablished(journal, SESSION, RUN), false)

    // 终态不得记成完成。
    const { eventPort, notified } = terminalObserver()
    const outcome = await TerminalReporter_completeWithEvidence(
      eventPort,
      undefined,
      terminalTurn(SESSION, RUN, 'The task is delivered and everything is finished.'),
      deliveryEstablished(journal, SESSION, RUN),
    )
    assert.equal(
      outcome.tag,
      XTraceTerminalCompletion.RejectedUndelivered.tag,
      'an undelivered stop is not a completion',
    )
    assert.equal(notified.length, 0, 'no terminal may be published for an undelivered turn')

    // 效果账上没有成功效果，也没有被任何摘要记成已确认。
    assert.equal(change.worktreeEffect(ledger, 'worktree-013'), 'Requested')
    const facts = journalFacts(directory, WRITER)
    assert.deepEqual(deliveryFactsOf(facts), [], 'a refused delivery leaves no witness behind')
    assert.deepEqual(effectFactsOf(facts), [], 'a delivery that did not happen writes no effect fact')
  })
})

test('WHAT[effect-accounting-013] a committed-but-unpresented delivery and an unestablished delivery are two different results', async () => {
  // 情形 A：交付已提交，呈现尚未确认。
  await withJournal(async ({ journal }) => {
    const committed = await deliveryJournal.TaskDeliveryJournal_commit(
      journal,
      deliveryWitness({ session: SESSION, run: RUN, call: 'call-013', view: 'the delivered material' }),
    )
    assert.equal(committed.tag, 0)
    jsRuntime.recordFencedDeliveredView(SESSION, RUN, 'call-013', {
      prefixUntil: 0,
      thenMap: { 0: 'the delivered material' },
    })
    try {
      const { eventPort, notified } = terminalObserver()
      const outcome = await TerminalReporter_completeWithEvidence(
        eventPort,
        undefined,
        terminalTurn(SESSION, RUN, 'Delivered.'),
        deliveryEstablished(journal, SESSION, RUN),
      )
      assert.notEqual(
        outcome.tag,
        XTraceTerminalCompletion.RejectedUndelivered.tag,
        'a committed delivery is not an undelivered stop',
      )
      assert.equal(notified.length, 1, 'the committed delivery publishes its terminal')
      assert.equal(
        jsRuntime.deliveredFenceOf(SESSION).toolCallId,
        'call-013',
        'the delivery cell belongs to the delivering call',
      )
      assert.deepEqual(
        jsRuntime.deliveredReturned(SESSION),
        [],
        'the delivery is committed but not yet presented',
      )
    } finally {
      jsRuntime.clearDeliveredValue(SESSION)
    }
  })

  // 情形 B：交付未成立。
  await withJournal(async ({ journal }) => {
    const { eventPort, notified } = terminalObserver()
    const outcome = await TerminalReporter_completeWithEvidence(
      eventPort,
      undefined,
      terminalTurn(SESSION, RUN, 'Delivered.'),
      deliveryEstablished(journal, SESSION, RUN),
    )
    assert.equal(
      outcome.tag,
      XTraceTerminalCompletion.RejectedUndelivered.tag,
      'an unestablished delivery is not a completion',
    )
    assert.equal(notified.length, 0, 'an unestablished delivery publishes no terminal')
    assert.equal(
      jsRuntime.deliveredFenceOf(SESSION),
      null,
      'an unestablished delivery leaves no fenced delivery cell',
    )
    assert.equal(deliveryEstablished(journal, SESSION, RUN), false)
  })
})

test('WHAT[effect-accounting-013] a committed delivery survives a later failed delivery attempt', async () => {
  await withJournal(async ({ directory, journal }) => {
    const first = deliveryWitness({
      session: SESSION,
      run: RUN,
      call: 'call-013',
      view: 'the delivered material',
    })
    assert.equal(
      (await deliveryJournal.TaskDeliveryJournal_commit(journal, first)).tag,
      0,
      'the first delivery commits',
    )

    // 同一轮里的后续尝试失败了（换了 toolCallId）：提交被拒。
    const second = deliveryWitness({
      session: SESSION,
      run: RUN,
      call: 'call-014',
      view: 'a different material',
      material: 'not-the-material-digest',
    })
    assert.equal(
      (await deliveryJournal.TaskDeliveryJournal_commit(journal, second)).tag,
      1,
      'the later attempt is refused',
    )

    // 已提交的事实没有被后续失败抹掉。
    const found = deliveryJournal.TaskDeliveryJournal_tryFind(
      journal,
      identity.SessionIdModule_create(SESSION),
      identity.ProviderRunIdentityModule_create(RUN),
    )
    assert.ok(found, 'the committed delivery must survive the later failed attempt')
    assert.equal(found.View, 'the delivered material')
    assert.deepEqual(found.Fence.ToolCallId, identity.ToolCallIdModule_create('call-013'))
    assert.equal(
      deliveryFactsOf(journalFacts(directory, WRITER)).length,
      1,
      'the failed attempt left no witness of its own and did not erase the committed one',
    )

    // 终态仍然认这份已提交的交付。
    const { eventPort, notified } = terminalObserver()
    const outcome = await TerminalReporter_completeWithEvidence(
      eventPort,
      undefined,
      terminalTurn(SESSION, RUN, 'Delivered.'),
      deliveryEstablished(journal, SESSION, RUN),
    )
    assert.notEqual(
      outcome.tag,
      XTraceTerminalCompletion.RejectedUndelivered.tag,
      'the committed delivery still completes the turn',
    )
    assert.equal(notified.length, 1, 'the committed delivery still publishes its terminal')
  })
})

test('WHAT[effect-accounting-013] assistant prose and a provider stop cannot fabricate a completion or an effect', async () => {
  await withJournal(async ({ directory, journal }) => {
    const ledger = change.requestWorktree(change.empty(), 'worktree-013', '/repo/worktree-013', 'job-013')
    const { eventPort, notified } = terminalObserver()

    // 散文声称交付完成，provider 也停了：没有真实交付就不许补成完成。
    const outcome = await TerminalReporter_completeWithEvidence(
      eventPort,
      undefined,
      terminalTurn(SESSION, RUN, 'The task is delivered; everything is finished.'),
      deliveryEstablished(journal, SESSION, RUN),
    )
    assert.equal(
      outcome.tag,
      XTraceTerminalCompletion.RejectedUndelivered.tag,
      'prose and a provider stop are not a delivery',
    )
    assert.equal(notified.length, 0, 'no terminal is published for a stop without a delivery')

    // 效果同样没有被补成已确认。
    assert.equal(change.worktreeEffect(ledger, 'worktree-013'), 'Requested')
    assert.deepEqual(
      effectFactsOf(journalFacts(directory, WRITER)),
      [],
      'a prose summary writes no effect fact',
    )
    assert.equal(deliveryEstablished(journal, SESSION, RUN), false, 'no witness was invented')
  })
})
