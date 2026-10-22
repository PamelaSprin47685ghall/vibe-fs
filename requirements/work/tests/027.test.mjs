import assert from 'node:assert/strict'
import test from 'node:test'
import * as completedTurn from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'
import * as managerWorkflow from '../../../dist/Mission/Manager/WorkflowSurface.js'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as executionStatus from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import { execFileSync } from 'node:child_process'
import { existsSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { withReview } from './support/plugin.mjs'
import { acceptAuthorityRoot, withExecutablePlugin, withRestartablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

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
  const { completeWithoutDelivery } = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  for (const name of ['engineer', 'devops', 'orchestrator']) {
    await withReview(async ({ runtime, session }) => {
      const before = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
      assert.ok(before !== null, `${name} logical run must start active`)

      const outcome = await completeWithoutDelivery(name, session, 'user-root', `${name}-run-1`, `${name} progress report without delivery`)
      assert.equal(outcome.caseName, 'RejectedUndelivered')
      assert.equal(outcome.terminalNotifications, 0)
      const after = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
      assert.deepEqual(after, before, `${name} logical run must not close without delivery witness`)
    }, { session: `${name}-progress-stop-without-witness` })
  }

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
        const program = delivered
          ? 'class Js extends JsProgram { async run() { await this.deliver({ 0: "the delivered view" }); } }'
          : 'class Js extends JsProgram { async run() { return 1; } }'
        const assistant = {
          info: {
            id: run,
            role: 'assistant',
            sessionID: session,
            parentID: physical,
            agent: name,
            providerID: 'provider',
            modelID: `${name}-model`,
            time: { created: 2 },
          },
          parts: [
            {
              type: 'text',
              text: `${name} progress report: implementation stepped forward, no deliver called.`,
            },
            { type: 'tool', tool: `todowrite-for-${name}`, callID: `call-${session}`, state: { status: 'pending', input: { program } } },
          ],
        }
        runtime.pushHostMessage(session, assistant)
        await hooks['experimental.chat.messages.transform']({ sessionID: session }, { messages: [user] })

        const before = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.ok(before !== null, `${name} logical run must start active`)

        const result = await hooks.tool[`todowrite-for-${name}`].execute(
          { program },
          { sessionID: session, agent: name, callID: `call-${session}`, messageID: run },
        )
        assert.match(String(result), /# ok/)
        assistant.parts.at(-1).state = { status: 'completed', input: { program }, output: String(result) }
        assistant.info.time.completed = 3
        assistant.info.finish = 'stop'
        await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
        await hooks.event({ event: { type: 'session.idle', properties: { sessionID: session } } })

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

const life = lifecycle.LifecycleSurface ?? lifecycle

for (const delivered of [true, false]) {
  test(`WHAT[work-027] a second worktree plugin scope ${delivered ? 'does not revive Delivered work' : 'still nudges unfinished work'} from its durable authority`, async () => {
    await withRestartablePlugin(async (start, directory, control) => {
      await start()
      execFileSync('git', ['-C', directory, '-c', 'user.email=test@example.com', '-c', 'user.name=test', 'commit', '--quiet', '--allow-empty', '-m', 'initial'])
      const worktreePath = join(directory, 'manager-worktree')
      execFileSync('git', ['-C', directory, 'worktree', 'add', '--quiet', '-b', 'manager-work', worktreePath])
      const worktree = execFileSync('git', ['-C', worktreePath, 'rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim()
      const commonDir = await journalSurface.JournalSurface_initializeStateRoot(worktree)
      const session = `manager-second-scope-${delivered ? 'delivered' : 'unfinished'}`
      const physical = `physical-${session}`
      const run = `provider-${session}`
      await control.withRuntime(async runtime => {
        await acceptAuthorityRoot(runtime, session, 'manager', physical)
        const execute = (requestId, command) => {
          const receipt = life.execute(commonDir, session, requestId, command)
          assert.ok(['Committed', 'Replayed', 'Observed'].includes(receipt.Status), JSON.stringify(receipt))
          return receipt
        }
        execute('open', { type: 'Open', root: worktree, sequence: ['S4'], ownerId: session, openingCursor: 0 })
        const user = {
          info: { id: physical, sessionID: session, role: 'user', agent: 'manager', time: { created: 1 } },
          parts: [{ type: 'text', text: 'Complete this independent work.' }],
        }
        control.pushHostMessage(session, user)
        const second = await start(worktree)
        await second['chat.message']({ sessionID: session, messageID: physical, agent: 'manager' }, {
          message: user.info, parts: user.parts,
        })
        const assistant = {
          info: {
            id: run, sessionID: session, parentID: physical, role: 'assistant', agent: 'manager',
            providerID: 'fixture', modelID: 'manager-model', time: { created: 2 },
          },
          parts: [{ type: 'text', text: 'This is a progress report from the existing work.' }],
        }
        control.pushHostMessage(session, assistant)
        await second.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
        const output = { messages: [structuredClone(user)] }
        await second['experimental.chat.messages.transform']({ sessionID: session }, output)
        assert.ok(output.messages.length > 0, 'the real provider boundary admits this unfinished work before its stop')
        await second['chat.params']({ sessionID: session, messageID: physical, agent: 'manager' }, { temperature: 0, options: {} })
        assert.deepEqual(executionStatus.query(runtime.journal, session, physical), {
          accepted: true, providerStarted: true, terminal: false, disposition: null,
        })
        const before = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.equal(before.authorityRoot, physical)
        if (delivered) {
          const active = life.inspected(commonDir, session).ActiveId
          execute('review', { type: 'Review', incumbencyId: active, reviewerId: 'reviewer', findings: [] })
          const finish = (requestId, call, cursor) => execute(requestId, {
            type: 'Deliver', incumbencyId: active, providerRunId: run, toolCallId: call, cursor, blockers: [], nextOwnerId: session,
          })
          assert.equal(finish('confirm', 'confirm-call', 1).Kind, 'Confirmed')
          assert.equal(finish('deliver', 'deliver-call', 2).Kind, 'Delivered')
        }
        assert.equal(life.inspected(commonDir, session).Delivered, delivered)
        assistant.info.finish = 'stop'
        assistant.info.time.completed = 3
        await second.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
        const idleRevisitRead = Promise.withResolvers()
        const nudgeObserved = Promise.withResolvers()
        const readMessages = control.client.session.messages
        const sendPrompt = control.client.session.promptAsync
        let reads = 0
        control.client.session.messages = async args => {
          const result = await readMessages(args)
          if (args?.path?.id === session) {
            reads += 1
            if (reads === 3) idleRevisitRead.resolve()
          }
          return result
        }
        control.client.session.promptAsync = async args => {
          const result = await sendPrompt(args)
          if (args?.path?.id === session) nudgeObserved.resolve()
          return result
        }
        try {
          await second.event({ event: { type: 'session.idle', properties: { sessionID: session } } })
          // Coarse reconcile and idle proof read first; the next read belongs to
          // the resulting idle revisit. Drain that real pass before asserting silence.
          await idleRevisitRead.promise
          if (!delivered) await nudgeObserved.promise
          await control.awaitIngress()
          await control.stop(second)
        } finally {
          control.client.session.messages = readMessages
          control.client.session.promptAsync = sendPrompt
        }
        const prompts = control.prompts.filter(prompt => prompt.path?.id === session)
        assert.equal(prompts.length, delivered ? 0 : 1)
        if (!delivered) {
          assert.match(prompts[0].body.parts.find(part => part.type === 'text').text, /current user task has not been completed/i)
        }
        const after = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.equal(after.authorityRoot, before.authorityRoot)
        assert.equal(after.logicalRun, before.logicalRun)
        assert.equal(life.inspected(commonDir, session).Delivered, delivered)
        assert.equal(life.inspected(commonDir, session).ActiveId !== undefined, !delivered)
      })
    })
  })
}
