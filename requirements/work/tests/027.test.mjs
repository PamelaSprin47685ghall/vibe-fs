import assert from 'node:assert/strict'
import test from 'node:test'
import * as completedTurn from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'
import * as managerWorkflow from '../../../dist/Mission/Manager/WorkflowSurface.js'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import { existsSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { withReview } from './support/plugin.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

// 交付见证的观察口：已注册的 EventStore Surface 把 durable 事实读成普通 JSON
// （payload.Fact = ["Agent", [family, [case, payload]]]），不构造任何 F# union。
// 交付见证就是 Host 家族里那条 TaskDeliveryCommitted 事实。
const deliveryWitnesses = (commonDir) => {
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
  const witnesses = []
  for (const event of all.values()) {
    const fact = event?.payload?.Fact
    if (fact?.[1]?.[0] === 'Host' && fact[1][1][0] === 'TaskDeliveryCommitted') witnesses.push(fact)
  }
  return witnesses
}

test('WHAT[work-027] Plan empty-stop cannot impersonate an explicit user wait', () => {
  assert.ok(
    typeof completedTurn.needsInteractionRepair === 'function',
    'CompletedTurnSurface.needsInteractionRepair must be exported',
  )

  // Empty prose carries no durable question or completion witness.
  assert.equal(
    completedTurn.needsInteractionRepair('plan', true, 'stop', []),
    true,
    'Plan no longer has a natural-stop exemption',
  )
})

test('WHAT[work-027] other roles keep their empty-stop repair behavior unchanged', () => {
  // The change does not make malformed output acceptable for other roles.
  assert.equal(
    completedTurn.needsInteractionRepair('engineer', true, 'stop', []),
    true,
    'engineer empty-stop must keep requiring interaction repair',
  )
  assert.equal(
    completedTurn.needsInteractionRepair('manager', true, 'stop', []),
    true,
    'manager empty-stop must keep requiring interaction repair',
  )
})

test('WHAT[work-027] provider failures and length stops are not natural stops for plan', () => {
  // Provider errors remain failures; length still needs continuation.
  for (const finish of ['error', 'length']) {
    const planNeeds = completedTurn.needsInteractionRepair('plan', true, finish, [])
    const engineerNeeds = completedTurn.needsInteractionRepair('engineer', true, finish, [])
    assert.equal(
      planNeeds,
      engineerNeeds,
      `finish=${finish}: plan must share the existing recovery classification with other roles`,
    )
  }
})

test('WHAT[work-027] a completed Manager reply with unfinished work receives an explanatory same-run nudge', async () => {
  await withReview(async ({ runtime, session, directory }) => {
    const sent = []
    const port = {
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async (_session, text) => {
        sent.push(text)
        return dispatch.admittedWithPhysicalMessage(`manager-nudge-${sent.length}`)
      },
    }
    const gate = quiescence.create()
    const observe = async run => {
      quiescence.beginAttempt(gate, session)
      const idle = quiescence.observeIdle(gate, session)
      await managerWorkflow.observeIdle(port, runtime.journal, gate, idle,
        session, 'user-root', 'user-root', run, directory)
    }
    const before = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
    // The production boundary provides TurnCompleted, not merely malformed text.
    await observe('manager-natural-stop-1')
    assert.equal(sent.length, 1)
    assert.match(sent[0], /not been completed|not end that obligation/i)
    assert.match(sent[0], /join/)
    assert.doesNotMatch(sent[0], /\bask\b/i)
    await observe('manager-natural-stop-1')
    assert.equal(sent.length, 1, 'the same terminal occasion cannot send twice')
    await observe('manager-natural-stop-2')
    assert.equal(sent.length, 2, 'a new natural stop cannot spend a permanent one-nudge budget')
    assert.deepEqual(dispatch.projectionObservation(runtime.journal, session).activeLogicalRun, before)
  }, { session: 'manager-long-work-natural-stop' })
})

test('WHAT[work-027] a missing lifecycle projection is not evidence that the Manager goal is complete', async () => {
  await withReview(async ({ runtime, directory }) => {
    const session = 'manager-without-work-projection'
    await acceptAuthorityRoot(runtime, session, 'manager', 'missing-work-root')
    const gate = quiescence.create()
    quiescence.beginAttempt(gate, session)
    let sent = 0
    await managerWorkflow.observeIdle({
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async (_session, text) => {
        sent += 1
        assert.match(text, /current user task/i)
        return dispatch.admittedWithPhysicalMessage('missing-work-nudge')
      },
    }, runtime.journal, gate, quiescence.observeIdle(gate, session),
    session, 'missing-work-root', 'missing-work-root', 'missing-work-stop', directory)
    assert.equal(sent, 1)
  }, { session: 'manager-missing-projection-fixture' })
})

test('WHAT[work-027] Engineer/DevOps/Orchestrator progress stop cannot count as complete without TaskDelivery witness', async () => {
  // 合法进度 stop 不能当完成：无 witness 的 TurnCompleted 不写完成事实且 logical run 保持活跃。
  //
  // 被删除的对内部表示的断言（原先直接 import dist/Context/Trace/TerminalReporter.js、
  // dist/Composition/Turn/{Observation,Program}.js、dist/Foundation/{Identity,Roles}.js、
  // dist/OpenCode/Host/Message.js，并断言终态 union 的 case 标签）：终态裁决没有已
  // 注册的公开等价入口（没有任何已注册的 Surface 包装
  // TerminalReporter.completeWithEvidence），且它断言的只是内部 union 的标签。
  // 同一事实改由消费者可见的两条证据表达：
  //   * 「无交付 ⇒ 没有 durable 交付见证事实」—— 经已注册的 EventStore Surface
  //     读回真实 journal 的普通 JSON 事实；
  //   * 「无交付 ⇒ logical run 仍活跃」—— 经已注册的 DispatchSurface
  //     projectionObservation 读回活跃逻辑运行。
  // 真实交付（this.deliver）作为对照：它写下唯一一条见证。
  const nonPlanExecutors = ['engineer', 'devops', 'orchestrator']

  for (const name of nonPlanExecutors) {
    for (const delivered of [false, true]) {
      await withExecutablePlugin(async (hooks, directory, created, runtime) => {
        const session = `${name}-progress-stop-${delivered ? 'with' : 'without'}-witness`
        const physical = `phys-${session}`
        await acceptAuthorityRoot(runtime, session, name, physical)

        const user = {
          info: {
            id: physical,
            role: 'user',
            sessionID: session,
            agent: name,
            model: { providerID: 'provider', modelID: `${name}-model` },
            time: { created: 1 },
          },
          parts: [{ type: 'text', text: 'Do the work.' }],
        }
        runtime.pushHostMessage(session, user)
        await hooks['chat.message'](
          { sessionID: session, messageID: physical, agent: name },
          { message: user, parts: user.parts },
        )

        const run = `run-${session}`
        runtime.pushHostMessage(session, {
          info: {
            id: run,
            role: 'assistant',
            sessionID: session,
            parentID: physical,
            agent: name,
            providerID: 'provider',
            modelID: `${name}-model`,
            time: { created: 2, completed: 3 },
            finish: 'stop',
          },
          parts: [
            {
              type: 'text',
              text: `${name} progress report: implementation stepped forward, no deliver called.`,
            },
          ],
        })

        const before = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.ok(before !== null, `${name} logical run must start active`)

        const program = delivered
          ? 'class Js extends JsProgram { async run() { await this.deliver({ 0: "the delivered view" }); } }'
          : 'class Js extends JsProgram { async run() { return 1; } }'
        await hooks.tool[`todowrite-for-${name}`].execute(
          { program },
          { sessionID: session, agent: name, callID: `call-${session}`, messageID: run },
        )

        const witnesses = deliveryWitnesses(join(directory, '.Herdchestra'))
        if (delivered) {
          assert.equal(witnesses.length, 1, `${name} a real delivery writes exactly one witness`)
          const fence = witnesses[0][1][1][1].Fence
          const named = (value) => (Array.isArray(value) ? value[1] : value)
          assert.equal(
            named(fence.LogicalRunId),
            before.logicalRun,
            `${name} the witness is bound to this logical run`,
          )
        } else {
          assert.deepEqual(
            witnesses,
            [],
            `${name} an undelivered progress stop writes no delivery witness`,
          )
        }

        const after = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.deepEqual(after, before, `${name} logical run must not close without delivery witness`)
      })
    }
  }
})
