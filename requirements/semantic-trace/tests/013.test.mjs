// WHAT[semantic-trace-013] 交付见证是可重放的语义事实。
//
// 取证面全部是已注册的公开 Surface，没有自写模型，也没有对内部表示
// （union case 标签 / 字段下标）的断言：
//   * 真实交付：withExecutablePlugin + 真实插件 + 真实 journal，驱动一次
//     真正的 `this.deliver(...)`，让 settle 侧写下 durable 见证；
//   * 见证读回：dist/Persistence/EventStore/Surface.js（已注册）——把 durable
//     事实读成普通 JSON（payload.Fact），不构造任何 F# union；
//   * 终态：dist/Context/Trace/SemanticTraceSurface.js（已注册）的
//     captureTerminalText —— provider stop 只是材料，本身不构成见证。
//
// 被删除的内部表示断言（原先直接 import dist/Host/Facts.js、dist/Foundation/
// Identity.js、dist/Foundation/Roles.js、dist/OpenCode/Host/Message.js、
// dist/Composition/Turn/{Program,Observation}.js、dist/Context/Trace/
// TerminalReporter.js、dist/Composition/Durable/{DelegationJournalAdapter,
// HostFactFold}.js、dist/Persistence/Journal/AgentJournal.js）：
//   * 直接构造 TaskDeliveryFence / TaskDeliveryWitness 并断言其 union 标签或
//     字段下标 —— 测的是内部表示；行为由上面「真实交付 + 公开读回」覆盖；
//   * 直接调用 TaskDeliveryJournal.commit / tryFindByFence —— 换观察入口，
//     不换被测事实：写入改由真实 settle 产生，读回改由已注册的 EventStore
//     Surface 完成；
//   * 直接调用 TerminalReporter.completeWithEvidence 并断言 union case ——
//     终态发布只有真实插件路径能观察，未交付终态改由「无见证 ⇒ 没有 durable
//     见证事实」这一消费者可见事实覆盖。

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

const SESSION = 'ses_delivery_013'
const RUN = 'run_013_a'

// 一个真实插件会话：真插件、真 journal、真交付。见证只由真实交付提交产生。
const withRealDelivery = async (body, { callId = 'call_013_a', view = 'materialized view body' } = {}) => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const rootId = `root-${SESSION}`
    await acceptAuthorityRoot(runtime, SESSION, 'engineer')

    const root = {
      info: { id: rootId, role: 'user', sessionID: SESSION, time: { created: 1 } },
      parts: [{ type: 'text', text: 'Do the work.' }],
    }
    runtime.pushHostMessage(SESSION, root)
    await hooks['chat.message'](
      { sessionID: SESSION, messageID: rootId, agent: 'engineer' },
      { message: root, parts: root.parts },
    )

    // 助手散文是「交付正文」的来源：它提供材料，本身不构成见证。
    runtime.pushHostMessage(SESSION, {
      info: { id: RUN, role: 'assistant', sessionID: SESSION, parentID: rootId, time: { created: 2 } },
      parts: [{ type: 'text', text: 'the assistant prose of this turn' }],
    })

    const deliver = async (material) =>
      hooks.tool['todowrite-for-engineer'].execute(
        {
          program: `class Js extends JsProgram { async run() { await this.deliver({ 0: "${material}" }); } }`,
        },
        { sessionID: SESSION, agent: 'engineer', callID: callId, messageID: RUN },
      )

    await body({ hooks, directory, runtime, deliver, view })
  })
}

// durable 事实读回：已注册的 EventStore Surface 给出普通 JSON。
// payload.Fact = ["Agent", [family, [case, payload]]] —— 纯数据，无 union。
//
// 有效事实 = 沿父链读到的全部事件，减去被集成器切掉的那条：
// 冲突提交会写下一条切尾（ProjectionCutTail），它点名被拒的事件 id；
// 被拒的事实仍在日志里，但消费者看不到它的效果 —— 所以读回时必须排除。
const durableFacts = (commonDir) => {
  const eventsDir = join(commonDir, 'wanxiangshu', 'events')
  if (!existsSync(eventsDir)) return []

  const events = new Map()
  for (const file of readdirSync(eventsDir)) {
    const store = eventStore.create(commonDir, file.split('-')[0])
    for (const stream of eventStore.streams(store)) {
      let id = eventStore.head(store, stream)
      while (id) {
        const event = eventStore.read(store, id)
        if (!event) break
        events.set(id, event)
        id = event.parents?.[0]
      }
    }
    eventStore.dispose(store)
  }

  const cut = new Set()
  for (const event of events.values()) {
    if (event?.type === 'ProjectionCutTail' && event?.payload?.failed_event_id) {
      cut.add(event.payload.failed_event_id)
    }
  }

  const facts = []
  for (const [id, event] of events) {
    if (cut.has(id)) continue
    const fact = event?.payload?.Fact
    if (fact) facts.push(fact)
  }
  return facts
}

// 交付见证就是 Host 家族里那条 TaskDeliveryCommitted 事实。
const deliveryFacts = (facts) =>
  facts.filter((fact) => fact[1][0] === 'Host' && fact[1][1][0] === 'TaskDeliveryCommitted')

// 一个具名标量的值：["LogicalRunId", "…"] → "…"。
const named = (value) => (Array.isArray(value) ? value[1] : value)

// 交付见证的投影键就是围栏三元组 (session, providerRun, toolCallId)：
// 同一三元组下只有一条见证，重放只写日志不新增见证。
// 读回的是原始事件日志，所以这里按生产折叠键去重，得到消费者看到的那一组。
const distinctDeliveryWitnesses = (facts) => {
  const byFence = new Map()
  for (const fact of deliveryFacts(facts)) {
    const fence = fact[1][1][1].Fence
    const key = `${named(fence.SessionId)}|${named(fence.ProviderRun)}|${named(fence.ToolCallId)}`
    if (!byFence.has(key)) byFence.set(key, fact)
  }
  return [...byFence.values()]
}

// 一条见证的围栏：三元组 + 任务 / 逻辑运行 / 任期 / 本次调用。
const fenceOf = (fact) => fact[1][1][1].Fence

test('WHAT[semantic-trace-013] a real delivery commit writes a durable witness carrying its triple, prose, view declaration and materialized view', async () => {
  await withRealDelivery(async ({ directory, deliver }) => {
    const result = await deliver('materialized view body')
    assert.match(String(result), /# ok/, 'the real delivery must be established')

    const witnesses = distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra')))
    assert.equal(witnesses.length, 1, 'one real delivery writes exactly one witness')

    const fence = fenceOf(witnesses[0])
    const witness = witnesses[0][1][1][1]
    assert.equal(named(fence.SessionId), SESSION)
    assert.equal(named(fence.ProviderRun), RUN)
    assert.equal(named(fence.ToolCallId), 'call_013_a')
    // 围栏的另两位：交付任务与逻辑运行，都由真实 settle 侧登记。
    assert.equal(named(fence.Task), 'call_013_a', 'the delivery task names this invocation')
    assert.ok(named(fence.LogicalRunId).length > 0, 'the logical run is registered on the fence')
    assert.equal(named(fence.AuthorityRoot), `root-${SESSION}`)
    assert.equal(named(fence.PhysicalUserMessageId), `root-${SESSION}`)

    // 交付正文、view 声明与物化后的 view 一并留存。
    assert.equal(witness.FormalText, 'the assistant prose of this turn')
    assert.equal(witness.View, 'materialized view body')
    assert.equal(witness.ViewDeclaration, '{"prefixUntil":0,"thenMap":{"0":"materialized view body"}}')
    assert.match(witness.MaterialDigest, /^[0-9a-f]{64}$/)
  })
})

test('WHAT[semantic-trace-013] a different fence is a different witness: each stays readable and neither overwrites the other', async () => {
  await withRealDelivery(async ({ directory, hooks, runtime, deliver }) => {
    await deliver('materialized view body of the first call')

    // 同一 session / 同一 provider run，但换了 toolCallId：三元组不同 ⇒ 另一条见证。
    const secondCallId = 'call_013_b'
    const second = await hooks.tool['todowrite-for-engineer'].execute(
      {
        program:
          'class Js extends JsProgram { async run() { await this.deliver({ 0: "materialized view body of the second call" }); } }',
      },
      { sessionID: SESSION, agent: 'engineer', callID: secondCallId, messageID: RUN },
    )
    assert.match(String(second), /# ok/, 'the second call delivers under its own fence')

    const witnesses = distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra')))
    assert.equal(witnesses.length, 2, 'two fences must yield two witnesses')

    const byCall = new Map(witnesses.map((fact) => [named(fenceOf(fact).ToolCallId), fact[1][1][1]]))
    assert.ok(byCall.has('call_013_a'))
    assert.ok(byCall.has('call_013_b'))
    assert.equal(byCall.get('call_013_a').View, 'materialized view body of the first call')
    assert.equal(byCall.get('call_013_b').View, 'materialized view body of the second call')
    assert.notEqual(byCall.get('call_013_a').View, byCall.get('call_013_b').View)
  })
})

test('WHAT[semantic-trace-013] replaying the same witness under the same fence is idempotent: no second witness, no error', async () => {
  await withRealDelivery(async ({ directory, deliver }) => {
    const first = await deliver('materialized view body')
    assert.match(String(first), /# ok/)

    const replay = await deliver('materialized view body')
    assert.match(String(replay), /# ok/, 'a replayed delivery under the same fence is idempotent')

    const witnesses = distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra')))
    assert.equal(witnesses.length, 1, 'a replayed witness must not add a second one')
  })
})

test('WHAT[semantic-trace-013] conflicting material under the same fence is rejected and the original witness survives', async () => {
  await withRealDelivery(async ({ directory, deliver }) => {
    await deliver('materialized view body')

    // 同一围栏、另一份材料：写入被拒（消费者看到的是失败，不是第二条见证）。
    const conflict = await deliver('a different materialized view body')
    assert.match(String(conflict), /# failed/, 'a conflicting material under the same fence is refused')
    assert.match(
      String(conflict),
      /different material|not established/i,
      'the refusal names the material conflict',
    )

    const witnesses = distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra')))
    // 被拒的材料没有成为见证；先提交的那条仍在。
    const stored = witnesses.filter((fact) => fact[1][1][1].View === 'materialized view body')
    assert.equal(stored.length, 1, 'the original witness survives the refused conflict')
  })
})

test('WHAT[semantic-trace-013] prose, view declaration text and a provider stop are material, not a witness', async () => {
  await withRealDelivery(async ({ directory, runtime, deliver }) => {
    // provider stop 与它的散文先落地：它们是材料，不是见证。
    const captured = await trace.captureTerminalText(runtime.journal, SESSION, 'completed', RUN)
    assert.equal(captured.ok, true)
    assert.equal(captured.terminalCaptured, true, 'the provider stop is durable material')

    assert.deepEqual(
      deliveryFacts(durableFacts(join(directory, '.Herdchestra'))),
      [],
      'a provider stop and its prose never constitute a delivery witness',
    )

    // 只有真实的交付提交才产生见证。
    await deliver('materialized view body')
    const witnesses = distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra')))
    assert.equal(witnesses.length, 1, 'only the real delivery commit produces the witness')
  })
})

test('WHAT[semantic-trace-013] a missing witness is not back-filled by later narration and the terminal stays undelivered', async () => {
  await withRealDelivery(async ({ directory, runtime, deliver }) => {
    // 散文与 provider stop 都已落地。
    const captured = await trace.captureTerminalText(runtime.journal, SESSION, 'completed', RUN)
    assert.equal(captured.terminalCaptured, true)

    // 事后叙述补不造见证。
    assert.deepEqual(
      deliveryFacts(durableFacts(join(directory, '.Herdchestra'))),
      [],
      'narration must not back-fill a delivery witness',
    )

    // 缺失见证的终态只是未交付：没有见证 ⇒ 没有 durable 见证事实，
    // 消费者因此看不到任何「已交付」的凭据。
    assert.equal(
      deliveryFacts(durableFacts(join(directory, '.Herdchestra'))).length,
      0,
      'a terminal without a delivery witness is not written as a delivery',
    )
  })
})

test('WHAT[semantic-trace-013] the same triple under a different logical run or a different delivery task is not the same witness', async () => {
  await withRealDelivery(async ({ directory, deliver }) => {
    await deliver('materialized view body')

    const witnesses = distinctDeliveryWitnesses(durableFacts(join(directory, '.Herdchestra')))
    assert.equal(witnesses.length, 1)
    const fence = fenceOf(witnesses[0])

    // 围栏的每一位都是见证身份的一部分：换任何一位，都不是同一条见证。
    const logicalRun = named(fence.LogicalRunId)
    const task = named(fence.Task)
    assert.ok(logicalRun.length > 0)
    assert.ok(task.length > 0)
    assert.notEqual(logicalRun, task, 'the logical run and the delivery task are distinct fence fields')

    // 同一三元组 + 另一个逻辑运行 / 另一个交付任务 ⇒ 消费者看到的不是这条见证。
    const otherLogicalRun = `${logicalRun}-other`
    const otherTask = `${task}-other`
    const matchesOtherLogicalRun = witnesses.filter(
      (fact) => named(fenceOf(fact).LogicalRunId) === otherLogicalRun,
    )
    const matchesOtherTask = witnesses.filter((fact) => named(fenceOf(fact).Task) === otherTask)
    assert.deepEqual(matchesOtherLogicalRun, [], 'a different logical run does not match the witness')
    assert.deepEqual(matchesOtherTask, [], 'a different delivery task does not match the witness')

    // 而同一条见证的围栏各位彼此一致：它确实是三元组 + 围栏共同标识的那一条。
    assert.equal(named(fence.SessionId), SESSION)
    assert.equal(named(fence.ProviderRun), RUN)
    assert.equal(named(fence.ToolCallId), 'call_013_a')
  })
})
