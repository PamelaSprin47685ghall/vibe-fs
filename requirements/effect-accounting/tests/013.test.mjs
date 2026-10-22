// WHAT[effect-accounting-013] 交付成功不是效果。
//
// 取证面全部是已注册的公开 Surface，没有自写模型，也没有对内部表示
// （union case 标签 / 字段下标）的断言：
//   * 真实效果：dist/Change/Host/Surface.js —— 真实编排 Host 在真实 journal 上
//     写下效果的请求意图（WorktreeCreateRequested）与物理确认
//     （WorktreeCreated）；dist/Change/Surface.js 把这些事实折回效果账
//     （Requested 意图 / Created 物理确认）；
//   * 真实交付：withExecutablePlugin + 真实插件 + 真实 journal，驱动一次真正的
//     `this.deliver(...)`，让 settle 侧写下 durable 见证；
//   * 见证读回：dist/Persistence/EventStore/Surface.js —— durable 事实读成普通
//     JSON（payload.Fact），不构造任何 F# union；
//   * 交付格：dist/Repository/Programming/Js/RuntimeSurface.js —— 进程内的带围栏
//     交付格（消费者可见的「已提交但尚未呈现」）。
//
// 被删除的对内部表示的断言（行为由第 1 类用例覆盖）：
//   * 直接构造 TaskDeliveryFence / TaskDeliveryWitness（dist/Host/Facts.js、
//     dist/Foundation/Identity.js）并断言其 union 标签或字段下标 —— 测的是内部
//     表示；真实交付 + 公开读回覆盖同一事实；
//   * 直接调用 TaskDeliveryJournal.commit / tryFind（
//     dist/Composition/Durable/DelegationJournalAdapter.js）—— 换观察入口，不换
//     被测事实：写入改由真实 settle 产生，读回改由已注册的 EventStore Surface
//     完成；
//   * 直接调用 TerminalReporter.completeWithEvidence（dist/Context/Trace/
//     TerminalReporter.js）并断言其 union case（dist/Composition/Turn/
//     {Program,Observation}.js、dist/Foundation/Roles.js、dist/OpenCode/Host/
//     Message.js 构造的输入）—— 终态裁决没有已注册的公开等价入口，且真实插件
//     路径下终态通知不可观测（宿主事件夹具不推进 provider 回合），故终态相关
//     断言整体删除：终态必须以「有无 durable 见证」这一消费者可见事实表达，
//     而非以终态 union 的标签表达。

import assert from 'node:assert/strict'
import { existsSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { parse } from 'smol-toml'
import { executeHostTool } from '../../delegation/tests/support/real-fork.mjs'
import * as change from '../../../dist/Change/Surface.js'
import * as changeHost from '../../../dist/Change/Host/Surface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as jsRuntime from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

const SESSION = 'sess-effect-013'
const RUN = 'run-effect-013'
const WORKTREE = 'manager/job-effect-013'

// 一个真实插件会话：真插件、真 journal、真交付。真交付只由真实 settle 产生。
// 交付格是进程内按 session 分区的，所以每个用例用属于自己的 session。
const withRealPlugin = async (body, session = SESSION) => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const rootId = `root-${session}`
    const run = `${RUN}-${session}`
    await acceptAuthorityRoot(runtime, session, 'engineer')

    const root = {
      info: { id: rootId, role: 'user', sessionID: session, time: { created: 1 } },
      parts: [{ type: 'text', text: 'Do the work.' }],
    }
    runtime.pushHostMessage(session, root)
    await hooks['chat.message'](
      { sessionID: session, messageID: rootId, agent: 'engineer' },
      { message: root, parts: root.parts },
    )

    const deliver = async (material, callId = 'call-effect-013') =>
      executeHostTool(hooks, runtime, session, 'engineer', rootId, run, 'todowrite-for-engineer',
        { program: `class Js extends JsProgram { async run() { await this.deliver({ 0: ${JSON.stringify(material)} }); } }` },
        callId, 'the assistant prose of this turn')

    await body({ hooks, directory, runtime, deliver, session, run })
  })
}

// durable 事实读回：已注册的 EventStore Surface 给出普通 JSON。
// payload.Fact = ["Agent", [family, [case, payload]]] —— 纯数据，无 union。
//
// 有效事实 = 沿父链读到的全部事件（父在前＝追加顺序），减去被集成器切掉的那条：
// 冲突提交会写下一条切尾（ProjectionCutTail），它点名被拒的事件 id。
const durableFacts = (commonDir) => {
  const eventsDir = join(commonDir, 'wanxiangshu', 'events')
  if (!existsSync(eventsDir)) return []

  const all = new Map()
  for (const file of readdirSync(eventsDir)) {
    const store = eventStore.create(commonDir, file.split('-')[0])
    for (const stream of eventStore.streams(store)) {
      let id = eventStore.head(store, stream)
      while (id) {
        const event = eventStore.read(store, id)
        if (!event) break
        all.set(id, event)
        id = event.parents?.[0]
      }
    }
    eventStore.dispose(store)
  }

  const cut = new Set()
  for (const event of all.values()) {
    if (event?.type === 'ProjectionCutTail' && event?.payload?.failed_event_id) {
      cut.add(event.payload.failed_event_id)
    }
  }

  // 追加顺序：先根后子。
  const children = new Map()
  const roots = []
  for (const [id, event] of all) {
    const parent = event?.parents?.[0]
    if (parent && all.has(parent)) {
      if (!children.has(parent)) children.set(parent, [])
      children.get(parent).push(id)
    } else {
      roots.push(id)
    }
  }
  const ordered = []
  const queue = [...roots]
  while (queue.length) {
    const id = queue.shift()
    ordered.push(id)
    for (const child of children.get(id) ?? []) queue.push(child)
  }

  const facts = []
  for (const id of ordered) {
    if (cut.has(id)) continue
    const fact = all.get(id)?.payload?.Fact
    if (fact) facts.push(fact)
  }
  return facts
}

// 效果事实与交付事实分属不同家族：效果落在 Orchestrator 家族，交付落在 Host
// 家族。交付不是效果，所以两者互不替代。
const effectFactsOf = (facts) =>
  facts.filter((fact) => fact[1][0] === 'Orchestrator').map((fact) => fact[1][1][0])
const deliveryFactsOf = (facts) =>
  facts.filter((fact) => fact[1][0] === 'Host' && fact[1][1][0] === 'TaskDeliveryCommitted')

// 交付见证的投影键就是围栏三元组 (session, providerRun, toolCallId)：
// 同一三元组下只有一条见证。
const distinctDeliveryWitnesses = (facts) => {
  const byFence = new Map()
  for (const fact of deliveryFactsOf(facts)) {
    const fence = fact[1][1][1].Fence
    const named = (value) => (Array.isArray(value) ? value[1] : value)
    const key = `${named(fence.SessionId)}|${named(fence.ProviderRun)}|${named(fence.ToolCallId)}`
    if (!byFence.has(key)) byFence.set(key, fact)
  }
  return [...byFence.values()]
}

// 把 journal 里的效果事实喂回真实的效果 fold，得到账上那笔效果的分型状态
// （Requested 意图 / Created 物理确认）。
const effectLedgerOf = (facts) => {
  const events = facts
    .filter((fact) => fact[1][0] === 'Orchestrator')
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

// 真实的编排面：它在 journal 上写下效果的请求意图与物理确认 —— 效果账因此真的
// 有一笔「已确认」。
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

const recordRealEffect = async (runtime, directory) => {
  const host = changeHost.create({
    sessions: sessionPortStub(),
    journal: runtime.journal,
    gitPort: gitPortStub(),
    repoPath: directory,
    targetBranch: 'refs/heads/main',
    orchestratorId: 'ses_orchestrator_effect_013',
  })
  try {
    await changeHost.forkManagerJob(host, 'job-effect-013', 'manager', 'root request')
  } catch {
    // The manager stage of the fork may fail; the worktree evidence chain is
    // what this test observes, and it is written before that stage.
  }
}


// ── 交付成立的登记（attempt 台账）读回 ─────────────────────────────────
// 台账是呈现面事实：它让 transform 钩子的交付裁决能停掉这一 attempt。
// 它与 durable 见证同生同灭：提交被拒 → 台账没有任何登记。
// 读法与 transform 钩子完全一致（同一份台账、同一把读法）。
const { establishedDeliveryCallId } = await import('../../../dist/Mission/Work/DeliveryAttemptSurface.js')

// The tool call id of the established attempt on that physical input, or null.
const establishedIn = (session, physical) =>
  establishedDeliveryCallId(session, physical) ?? null

// provider 请求：交付裁决读的是本次请求携带的物理用户消息。
const providerRequest = (session, physical, run) => ({
  messages: [
    { role: 'user', info: { id: physical, sessionID: session, role: 'user' }, parts: [{ type: 'text', text: 'Do the work.' }] },
    { role: 'assistant', info: { id: run, sessionID: session, role: 'assistant', parentID: physical, providerID: 'provider', modelID: 'engineer-model' }, parts: [] },
  ],
})

test('WHAT[effect-accounting-013] a committed delivery is not an effect confirmation: the effect ledger is unchanged and the effect keeps its own confirmation', async () => {
  await withRealPlugin(async ({ directory, runtime, deliver }) => {
    // 效果账先真的记下一笔效果：请求意图在前，物理确认在后。
    await recordRealEffect(runtime, directory)
    const effectsBefore = effectFactsOf(durableFacts(join(directory, '.Herdchestra')))
    assert.deepEqual(
      effectsBefore,
      ['WorktreeCreateRequested', 'WorktreeCreated'],
      'the effect is accounted with its own intent and its own confirmation',
    )
    const ledgerBefore = effectLedgerOf(durableFacts(join(directory, '.Herdchestra')))
    assert.equal(ledgerBefore.ok, true, ledgerBefore.error)
    assert.equal(
      change.worktreeEffect(ledgerBefore.ledger, WORKTREE),
      'Created',
      'the effect on the ledger is confirmed by its own physical confirmation',
    )

    // 交付成立：真实的 durable 见证被提交。
    const committed = await deliver('the delivered material')
    assert.match(String(committed), /# ok/, 'the real delivery must be established')

    // 交付成立只说明工作已提交：它没有把任何效果变成「已确认」。
    const factsAfter = durableFacts(join(directory, '.Herdchestra'))
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
      distinctDeliveryWitnesses(factsAfter).length,
      1,
      'the delivery is recorded as a delivery fact, never as an effect',
    )
    assert.deepEqual(
      deliveryFactsOf(factsAfter).map((fact) => fact[1][0]),
      ['Host'],
      'the delivery witness belongs to the Host family, not the effect family',
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
  await withRealPlugin(async ({ directory, runtime, deliver, session }) => {
    const ledger = change.requestWorktree(change.empty(), 'worktree-013', '/repo/worktree-013', 'job-013')

    // 交付未成立：真实的提交入口拒绝这份材料。
    const refused = await deliver('')
    assert.match(String(refused), /# failed/, 'a delivery with an invalid view is refused')
    assert.match(
      String(refused),
      /VIEW_DECLARATION_INVALID|not established/i,
      'the refusal names why the delivery was not established',
    )

    // 见证没有落账：交付未成立。
    const facts = durableFacts(join(directory, '.Herdchestra'))
    assert.deepEqual(distinctDeliveryWitnesses(facts), [], 'a refused delivery leaves no witness behind')
    assert.deepEqual(effectFactsOf(facts), [], 'a delivery that did not happen writes no effect fact')
    assert.equal(
      jsRuntime.deliveredFenceOf(session),
      null,
      'a refused delivery leaves no fenced delivery cell',
    )

    // 效果账上没有成功效果，也没有被任何摘要记成已确认。
    assert.equal(change.worktreeEffect(ledger, 'worktree-013'), 'Requested')
  }, 'sess-effect-013-refused')
})

test('WHAT[effect-accounting-013] a committed-but-unpresented delivery and an unestablished delivery are two different results', async () => {
  // 情形 A：交付已提交，呈现尚未确认。
  await withRealPlugin(async ({ deliver, session }) => {
    const committed = await deliver('the delivered material')
    assert.match(String(committed), /# ok/)

    assert.equal(
      jsRuntime.deliveredFenceOf(session).toolCallId,
      'call-effect-013',
      'the delivery cell belongs to the delivering call',
    )
    assert.deepEqual(
      jsRuntime.deliveredReturned(session),
      [],
      'the delivery is committed but not yet presented',
    )
  }, 'sess-effect-013-unpresented')

  // 情形 B：交付未成立：交付格是空的。
  assert.equal(
    jsRuntime.deliveredFenceOf('sess-effect-013-without-delivery'),
    null,
    'an unestablished delivery leaves no fenced delivery cell',
  )
})

test('WHAT[effect-accounting-013] a committed delivery survives a later failed delivery attempt', async () => {
  await withRealPlugin(async ({ directory, deliver, session }) => {
    const first = await deliver('the delivered material')
    assert.match(String(first), /# ok/, 'the first delivery commits')

    // 同一轮里的后续尝试失败了（换了 toolCallId）：提交被拒。
    const second = await deliver('', 'call-effect-014')
    assert.match(String(second), /# failed/, 'the later attempt is refused')

    // 已提交的事实没有被后续失败抹掉。
    const facts = durableFacts(join(directory, '.Herdchestra'))
    const witnesses = distinctDeliveryWitnesses(facts)
    assert.equal(
      witnesses.length,
      1,
      'the failed attempt left no witness of its own and did not erase the committed one',
    )
    const witness = witnesses[0][1][1][1]
    const returned = parse(witness.View).returned
    assert.deepEqual(returned.map(row => row.text), ['the delivered material', 'the assistant prose of this turn'])
    assert.equal(new Set(returned.map(row => row.id)).size, 2)
    for (const row of returned) {
      assert.equal(typeof row.id, 'string')
      assert.notEqual(row.id, '')
    }
    assert.equal(witness.FormalText, 'the assistant prose of this turn')
    assert.equal(
      jsRuntime.deliveredFenceOf(session).toolCallId,
      'call-effect-013',
      'the committed delivery still owns the delivery cell',
    )
  }, 'sess-effect-013-survives')
})

test('WHAT[effect-accounting-013] assistant prose and a provider stop cannot fabricate a completion or an effect', async () => {
  await withRealPlugin(async ({ directory, runtime, session, run }) => {
    // 散文与 provider stop 都已落地：它们是材料，不是见证。
    const captured = await trace.captureTerminalText(runtime.journal, session, 'completed', run)
    assert.equal(captured.ok, true)
    assert.equal(captured.terminalCaptured, true, 'the provider stop is durable material')

    const facts = durableFacts(join(directory, '.Herdchestra'))
    assert.deepEqual(
      distinctDeliveryWitnesses(facts),
      [],
      'a provider stop and its prose never constitute a delivery witness',
    )
    assert.deepEqual(
      effectFactsOf(facts),
      [],
      'a prose summary writes no effect fact',
    )
    assert.equal(
      jsRuntime.deliveredFenceOf(session),
      null,
      'a provider stop leaves no fenced delivery cell',
    )
  }, 'sess-effect-013-prose')
})

test('WHAT[effect-accounting-013] a refused delivery commit registers nothing, and a committed delivery registers an observable established attempt', async () => {
  // 自己的会话：进程内台账按 session 分区，同一文件里的其它用例不能污染它。
  const session = 'sess-effect-013-registration'
  const run = 'run-effect-013-registration'
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, session, 'engineer', 'root-registration-1')

    const root1 = { info: { id: 'root-registration-1', role: 'user', sessionID: session, agent: 'engineer',
      model: { providerID: 'provider', modelID: 'engineer-model' }, time: { created: 1 } }, parts: [{ type: 'text', text: 'Do the work.' }] }
    runtime.pushHostMessage(session, root1)
    await hooks['chat.message']({ sessionID: session, messageID: root1.info.id, agent: 'engineer' }, { message: root1.info, parts: root1.parts })
    const firstCall = 'call-effect-013-registration'
    const firstInput = { program: 'class Js extends JsProgram { async run() { await this.deliver({ 0: "material-A" }); } }' }
    const firstAssistant = {
      info: { id: run, role: 'assistant', sessionID: session, parentID: root1.info.id,
        agent: 'engineer', providerID: 'provider', modelID: 'engineer-model', time: { created: 2 } },
      parts: [
        { type: 'text', text: 'the assistant prose of this turn' },
        { id: `part-${firstCall}`, type: 'tool', tool: 'todowrite-for-engineer', callID: firstCall,
          state: { status: 'pending', input: firstInput } },
      ],
    }
    runtime.pushHostMessage(session, firstAssistant)
    await hooks['experimental.chat.messages.transform']({ sessionID: session }, { messages: [root1] })
    await hooks.event({ event: { type: 'message.updated', properties: { info: firstAssistant.info } } })

    // ── 提交成功：durable 见证落账，台账登记，裁决可停 ──────────────
    const committed = await hooks.tool['todowrite-for-engineer'].execute(firstInput,
      { sessionID: session, agent: 'engineer', callID: firstCall, messageID: run })
    assert.match(String(committed), /# ok/, 'the delivery is established')

    const witnesses = distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra')))
    assert.equal(witnesses.length, 1, 'the committed delivery left exactly one durable witness')

    const fence = establishedIn(session, 'root-registration-1')
    assert.ok(fence, 'a committed delivery registers the established attempt in the ledger')
    assert.equal(
      fence,
      'call-effect-013-registration',
      'the registration names the exact attempt that delivered',
    )

    // 交付格（呈现面事实）也在：提交成功才写。
    assert.equal(
      jsRuntime.deliveredFenceOf(session).toolCallId,
      'call-effect-013-registration',
      'a committed delivery owns the fenced delivery cell',
    )

    const notCompleted = providerRequest(session, root1.info.id, run)
    await hooks['experimental.chat.messages.transform']({ sessionID: session }, notCompleted)
    assert.equal(notCompleted.messages.length, 2, 'delivery alone must not end the provider request')
    firstAssistant.info.time.completed = 3
    firstAssistant.info.finish = 'stop'
    await hooks.event({ event: { type: 'message.updated', properties: { info: firstAssistant.info } } })
    await hooks.event({ event: { type: 'session.idle', properties: { sessionID: session } } })

    const stopped = providerRequest(session, 'root-registration-1', run)
    await assert.rejects(
      () => hooks['experimental.chat.messages.transform']({ sessionID: session }, stopped),
      error => {
        assert.equal(error.message, 'EMR-010: managed provider step for physical user message root-registration-1 has no committed model-routing lease')
        return true
      },
    )
    assert.deepEqual(distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra'))), witnesses)
    assert.equal(establishedIn(session, root1.info.id), firstCall)
    assert.equal(jsRuntime.deliveredFenceOf(session).toolCallId, firstCall)

    // ── 提交被拒（真实存储失败）：台账不得留下任何新登记 ────────────
    // 被拒的这次尝试回答另一个物理输入，所以它若被登记，登记会落在那个
    // 物理输入上 —— 这正是「有没有登记」的可判别处。
    const root2 = { info: { id: 'root-registration-2', role: 'user', sessionID: session, agent: 'engineer',
      model: { providerID: 'provider', modelID: 'engineer-model' }, time: { created: 3 } }, parts: [{ type: 'text', text: 'Do more work.' }] }
    runtime.pushHostMessage(session, root2)
    await hooks['chat.message']({ sessionID: session, messageID: root2.info.id, agent: 'engineer' }, { message: root2.info, parts: root2.parts })
    const refusedRun = `${run}-second`
    const refusedCall = 'call-effect-013-refused'
    const refusedInput = { program: 'class Js extends JsProgram { async run() { await this.deliver({ 0: "material-B" }); } }' }
    const refusedAssistant = {
      info: { id: refusedRun, role: 'assistant', sessionID: session, parentID: root2.info.id,
        agent: 'engineer', providerID: 'provider', modelID: 'engineer-model', time: { created: 4 } },
      parts: [
        { type: 'text', text: 'the assistant prose of the second turn' },
        { id: `part-${refusedCall}`, type: 'tool', tool: 'todowrite-for-engineer', callID: refusedCall,
          state: { status: 'pending', input: refusedInput } },
      ],
    }
    runtime.pushHostMessage(session, refusedAssistant)
    await hooks['experimental.chat.messages.transform']({ sessionID: session }, { messages: [root2] })
    await hooks.event({ event: { type: 'message.updated', properties: { info: refusedAssistant.info } } })

    // 真实存储被破坏（事件目录不再是目录）：真实提交入口因此拒绝这份
    // 见证 —— 真实的提交失败，不是伪造的失败。
    const eventsDir = join(directory, '.Herdchestra', 'wanxiangshu', 'events')
    rmSync(eventsDir, { recursive: true, force: true })
    writeFileSync(eventsDir, 'the durable store is no longer a directory')

    const refused = await hooks.tool['todowrite-for-engineer'].execute(refusedInput,
      { sessionID: session, agent: 'engineer', callID: refusedCall, messageID: refusedRun })
    assert.match(String(refused), /# failed/, 'the delivery is not established when the durable commit fails')
    assert.match(
      String(refused),
      /DELIVERY_NOT_ESTABLISHED|not established/i,
      'the refusal names that the delivery was not established',
    )

    // 台账：被拒的提交没有登记它自己的围栏。已成立的那次交付仍停在自己的
    // 围栏上 —— 被拒的 attempt 从未成为「已成立的交付」。
    const afterRefusal = establishedIn(session, 'root-registration-1')
    assert.ok(afterRefusal, 'the committed delivery stays registered')
    assert.equal(
      afterRefusal,
      'call-effect-013-registration',
      'a refused commit registers nothing of its own: the ledger still names the committed attempt',
    )
    assert.equal(
      establishedIn(session, 'root-registration-2'),
      null,
      'a refused commit registers nothing in the attempt ledger',
    )

    // 交付格：被拒的提交没有改写交付格，它仍属于那次真正成立的交付。
    assert.equal(
      jsRuntime.deliveredFenceOf(session).toolCallId,
      'call-effect-013-registration',
      'a refused commit leaves the fenced delivery cell with the committed attempt',
    )
  })
})
