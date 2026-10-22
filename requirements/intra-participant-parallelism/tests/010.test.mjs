import assert from 'node:assert/strict'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { acceptAuthorityRoot, openIncumbency, withRestartablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { factPayloads, journalFactTail, readJournal } from '../../verification-system/tests/e2e/support/journal-observer.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as events from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as jsRuntime from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

test('WHAT[intra-participant-parallelism-010] restart voids the interrupted work and explicit resume admits new work for the same participant through delivery and join', async () => {
  const hostObservers = new Set()
  const emit = async (hooks, event) => {
    for (const observe of hostObservers) observe(event)
    await hooks.event(event)
  }
  await withRestartablePlugin(async (start, directory, { stop, withRuntime, client, createdIds, messages, prompts, pushHostMessage }) => {
    const hooks1 = await start()
    const managerSession = 'ses-fission-restart-manager'
    await withRuntime(async (runtime) => {
      await acceptAuthorityRoot(runtime, managerSession, 'manager')
      await openIncumbency(runtime, managerSession)
    })

    const forked = await hooks1.tool['todowrite-for-manager'].execute(
      { program: 'class Js extends JsProgram { async run() { return await this.fork({ name: "ruling-materials", charge: "collect seven materials" }, {}); } }' },
      { sessionID: managerSession, agent: 'manager' },
    )
    assert.match(forked, /ok = true/)
    assert.match(forked, /outcome = "created"/)
    const engineerSession = createdIds[0]

    const fissioned = await hooks1.tool.fission.execute(
      { prompts: ['lane 1', 'lane 2'] },
      { sessionID: engineerSession, agent: 'engineer', callID: 'call-fission-orphan', messageID: 'run-pre-crash' },
    )
    assert.match(fissioned, /status\s*=\s*"fissioned"/i)

    const [admitted] = factPayloads(directory, 'FissionAdmitted')
    assert.equal(admitted.LaneCount, 2)

    const originalAuthority = await withRuntime(async runtime =>
      journalSurface.JournalSurface_snapshot(runtime.journal).sessionProjections[engineerSession].promptAuthority.activeLogicalRun,
    )
    await stop(hooks1)
    const sentBeforeRestart = prompts.length
    const hooks2 = await start()
    await hooks2['chat.message'](
      { sessionID: 'restart-observer', messageID: 'restart-observer-user', agent: 'manager' },
      { message: { id: 'restart-observer-user', role: 'user' }, parts: [{ type: 'text', text: 'observe the restarted Host' }] },
    )
    const deadline = Date.now() + 4000
    while (readJournal(directory, 'FissionFailed').named === 0) {
      assert.ok(Date.now() < deadline, 'Host activation must close the interrupted Fission group')
      await new Promise(resolve => setImmediate(resolve))
    }
    assert.equal(prompts.length, sentBeforeRestart, 'restart must not replay a prompt')
    const [failed] = factPayloads(directory, 'FissionFailed')
    assert.equal(failed.GroupId, admitted.GroupId)
    assert.equal(failed.Reason, 'process_interrupted')
    assert.ok(readJournal(directory, 'ChildWorkVoided').named > 0, 'lost child runs are voided without completion material')
    const retainedAuthority = await withRuntime(async runtime =>
      journalSurface.JournalSurface_snapshot(runtime.journal).sessionProjections[engineerSession].promptAuthority.activeLogicalRun,
    )
    assert.equal(retainedAuthority, null, 'voiding the lost run clears its active authority without completing it')
    assert.equal(readJournal(directory, 'TaskDeliveryCommitted').named, 0)
    const interrupted = await hooks2.tool.join.execute({}, { sessionID: managerSession, agent: 'manager' })
    assert.match(interrupted, /ruling-materials was voided/)
    assert.doesNotMatch(interrupted, /status = "completed"|Complete 9-item ruling materials report/)

    const continuationAccepted = Promise.withResolvers()
    const sendPrompt = client.session.promptAsync
    client.session.promptAsync = async args => {
      try {
        return await sendPrompt(args)
      } finally {
        continuationAccepted.resolve()
      }
    }

    const resumed = await hooks2.tool['todowrite-for-manager'].execute(
      { program: 'class Js extends JsProgram { async run() { return await this.resume({ name: "ruling-materials", charge: "continue the existing materials collection after restart" }, {}); } }' },
      { sessionID: managerSession, agent: 'manager' },
    )
    assert.match(resumed, /ok = true/)
    await continuationAccepted.promise
    client.session.promptAsync = sendPrompt
    const resumedAuthority = await withRuntime(async runtime =>
      journalSurface.JournalSurface_snapshot(runtime.journal).sessionProjections[engineerSession].promptAuthority.activeLogicalRun,
    )
    assert.equal(resumedAuthority.session, engineerSession)
    assert.notEqual(resumedAuthority.logicalRun, originalAuthority.logicalRun, 'explicit resume admits a new run instead of reviving voided work')
    assert.equal(createdIds.filter(id => id === engineerSession).length, 1, 'the original participant binding is reused')
    const terminals = []
    const terminalSubscription = await withRuntime(async runtime =>
      events.subscribeFuture(runtime.terminalPort, (sessionId, outcome) => terminals.push({ sessionId, ...outcome })),
    )

    const engineerUserMessages = messages.filter(
      m => m.id?.startsWith(`msg-${engineerSession}-`) && m.role === 'user',
    )
    const latestPhysicalId = engineerUserMessages[engineerUserMessages.length - 1].id

    const assistantMsg = {
      info: {
        id: `assistant-post-restart-${engineerSession}`,
        sessionID: engineerSession,
        parentID: latestPhysicalId,
        role: 'assistant',
        agent: 'engineer',
        providerID: 'provider',
        modelID: 'engineer-model',
        time: { created: 10 },
      },
      parts: [],
    }
    pushHostMessage(engineerSession, assistantMsg)

    await hooks2['experimental.chat.messages.transform'](
      { sessionID: engineerSession },
      {
        messages: [
          {
            info: {
              id: latestPhysicalId,
              sessionID: engineerSession,
              role: 'user',
              agent: 'engineer',
              time: { created: 9 },
            },
            parts: engineerUserMessages[engineerUserMessages.length - 1].parts,
          },
        ],
      },
    )

    await emit(hooks2, {
      event: {
        type: 'message.updated',
        properties: { info: assistantMsg.info },
      },
    })

    const toolContext = {
      sessionID: engineerSession,
      agent: 'engineer',
      messageID: assistantMsg.info.id,
      providerRunID: assistantMsg.info.id,
    }
    writeFileSync(join(directory, 'resume-material.txt'), 'resume-resource-ok')
    const executed = await hooks2.tool['todowrite-for-engineer'].execute(
      { program: 'class Js extends JsProgram { async run() { return await this.cat("resume-material.txt") } }' },
      { ...toolContext, callID: 'read-after-restart' },
    )
    assert.match(executed, /^# ok\b/m)
    assert.match(executed, /resume-resource-ok/)

    assistantMsg.parts = [
      { id: 'part-pre-delivery-report', type: 'text', text: 'Complete 9-item ruling materials report' },
      { id: 'part-delivery-call', type: 'tool', callID: 'delivery-after-restart', tool: 'todowrite-for-engineer',
        state: { status: 'running', input: {} } },
    ]
    const delivered = await hooks2.tool['todowrite-for-engineer'].execute(
      { program: 'class Js extends JsProgram { async run() { await this.deliver({ 0: "Complete 9-item ruling materials report" }) } }' },
      { ...toolContext, callID: 'delivery-after-restart' },
    )
    assert.match(delivered, /^# ok\b/m)
    assert.equal(readJournal(directory, 'TaskDeliveryCommitted').named, 1)
    const [delivery] = factPayloads(directory, 'TaskDeliveryCommitted')
    assert.match(delivery.View, /Complete 9-item ruling materials report/)
    assert.equal(delivery.FormalText, 'Complete 9-item ruling materials report')

    assistantMsg.info.finish = 'tool-calls'
    assistantMsg.info.time.completed = 11
    const promptsBeforeStop = prompts.length
    const finalMessage = {
      info: { ...assistantMsg.info, id: 'later-provider-stop', finish: 'stop', time: { created: 12, completed: 13 } },
      parts: [{ id: 'part-final-report', type: 'text', text: 'LATE_FINAL_TEXT_NOT_THE_DELIVERED_VIEW' }],
    }
    pushHostMessage(engineerSession, finalMessage)

    await emit(hooks2, {
      event: {
        type: 'message.updated',
        properties: {
          info: finalMessage.info,
        },
      },
    })
    await emit(hooks2, {
      event: {
        type: 'session.status',
        properties: {
          sessionID: engineerSession,
          status: { type: 'idle' },
        },
      },
    })

    let joinTimer
    const joined = await Promise.race([
      hooks2.tool.join.execute({}, { sessionID: managerSession, agent: 'manager' }),
      new Promise((_, reject) => {
        joinTimer = setTimeout(() => reject(new Error(
          'join hung waiting for resumed fission owner: ' + journalFactTail(directory, 12).join('\n'),
        )), 4000)
      }),
    ]).finally(() => {
      clearTimeout(joinTimer)
      events.dispose(terminalSubscription)
    })
    assert.match(joined, /Complete 9-item ruling materials report/)
    assert.doesNotMatch(joined, /LATE_FINAL_TEXT_NOT_THE_DELIVERED_VIEW|was voided/)
    assert.ok(jsRuntime.deliveredReturned(engineerSession).some(key => key.startsWith('prose:')))
    assert.ok(jsRuntime.deliveredReturned(engineerSession).every(key => !key.includes('LATE_FINAL_TEXT')))
    assert.equal(terminals.filter(outcome =>
      outcome.sessionId === engineerSession && outcome.kind === 'Completed',
    ).length, 1)
    assert.deepEqual(terminals.filter(outcome =>
      outcome.sessionId === engineerSession && outcome.kind === 'Completed',
    ).map(outcome => outcome.providerRun), [finalMessage.info.id])
    await stop(hooks2)
    assert.equal(prompts.length, promptsBeforeStop, 'a later provider step must not revive the delivered exchange')
  }, { events: { listen(observe) {
    hostObservers.add(observe)
    return () => hostObservers.delete(observe)
  } } })
})

test.todo('WHAT[intra-participant-parallelism-010] actual durable Fission facts reconstruct group membership and replacement after restart, recording interruption without automatically restarting lanes or guessing similar Host sessions (GAP-158)')
