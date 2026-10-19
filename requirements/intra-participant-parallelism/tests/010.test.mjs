import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, openIncumbency, withRestartablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'

test('WHAT[intra-participant-parallelism-010] load settlement closes orphaned open Fission groups with FissionFailed so a resumed owner turn completes and delivers to join instead of being swallowed as replaced', async () => {
  await withRestartablePlugin(async (start, _directory, { stop, withRuntime, createdIds, messages, pushHostMessage }) => {
    const hooks1 = await start()
    const managerSession = 'ses-fission-restart-manager'
    await withRuntime(async (runtime) => {
      await acceptAuthorityRoot(runtime, managerSession, 'manager')
      await openIncumbency(runtime, managerSession)
    })

    const forked = await hooks1.tool.fork.execute(
      { name: 'ruling-materials', charge: 'collect seven materials' },
      { sessionID: managerSession, agent: 'manager' },
    )
    assert.match(forked, /现已接下这项托付|carries this charge now/i)
    const engineerSession = createdIds[0]

    const fissioned = await hooks1.tool.fission.execute(
      { prompts: ['lane 1', 'lane 2'] },
      { sessionID: engineerSession, agent: 'engineer', callID: 'call-fission-orphan', messageID: 'run-pre-crash' },
    )
    assert.match(fissioned, /status\s*=\s*"fissioned"/i)

    // Stop incarnation 1 and boot incarnation 2: load-phase recovery settles
    // orphaned child runs and orphaned open Fission groups.
    await stop(hooks1)
    const hooks2 = await start()
    await hooks2['chat.message'](
      { sessionID: 'restart-observer', messageID: 'restart-observer-user', agent: 'manager' },
      { message: { id: 'restart-observer-user', role: 'user' }, parts: [{ type: 'text', text: 'observe the restarted Host' }] },
    )
    await withRuntime(async runtime => {
      const deadline = Date.now() + 4000
      while (journalSurface.JournalSurface_snapshot(runtime.journal).sessionProjections[engineerSession]?.promptAuthority?.activeLogicalRun) {
        assert.ok(Date.now() < deadline, 'Host activation must durably settle the interrupted child before resume')
        await new Promise(resolve => setImmediate(resolve))
      }
    })

    // Now Manager resumes the same child handle after restart.
    const resumed = await hooks2.tool.resume.execute(
      { name: 'ruling-materials', charge: 're-commissioned after restart: collect materials' },
      { sessionID: managerSession, agent: 'manager' },
    )
    assert.match(resumed, /现已接下这项托付|carries this charge now/i)

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

    const delivered = await hooks2.tool['todowrite-for-engineer'].execute(
      { program: "class Js extends JsProgram { async run() { await this.deliver('Complete 9-item ruling materials report') } }" },
      { sessionID: engineerSession, agent: 'engineer' },
    )
    assert.match(delivered, /^# ok\b/m)

    assistantMsg.info.finish = 'stop'
    assistantMsg.info.time.completed = 11
    assistantMsg.parts = [{ id: 'part-final-report', type: 'text', text: 'Complete 9-item ruling materials report' }]

    await hooks2.event({
      event: {
        type: 'message.updated',
        properties: {
          info: {
            id: assistantMsg.info.id,
            sessionID: engineerSession,
            parentID: latestPhysicalId,
            role: 'assistant',
            finish: 'stop',
            time: { created: 10, completed: 11 },
          },
        },
      },
    })
    await hooks2.event({
      event: {
        type: 'session.status',
        properties: {
          sessionID: engineerSession,
          status: { type: 'idle' },
        },
      },
    })

    const joined = await Promise.race([
      hooks2.tool.join.execute({}, { sessionID: managerSession, agent: 'manager' }),
      new Promise((_, reject) => setTimeout(() => reject(new Error('join hung waiting for resumed fission owner')), 4000)),
    ])
    assert.match(joined, /Complete 9-item ruling materials report/)
  })
})

test.todo('WHAT[intra-participant-parallelism-010] actual durable Fission facts reconstruct group membership and replacement after restart, recording interruption without automatically restarting lanes or guessing similar Host sessions (GAP-158)')
