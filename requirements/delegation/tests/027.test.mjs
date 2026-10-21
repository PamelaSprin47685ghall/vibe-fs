import assert from 'node:assert/strict'
import test from 'node:test'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import {
  agentIdOf,
  childTurn,
  forkCall,
  joinCall,
  observeTerminals,
  resumeCall,
  workLifecycleOf,
  withRealFork,
} from './support/real-fork.mjs'
import { injectAcceptedAssessment } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

// 真实插件路径下的旧 root 停机信号：生产把带 root 归属的停机终态发到共享
// 终端端口（`EventsSurface.notifyForAuthority`）。测试只发布这一条生产
// 终态，不写任何 durable 见证，也不等待一个没有见证的终态完成。
const stopForRoot = async (port, child, kind) => {
  const published = eventsSurface.notifyForAuthority(port, child, kind, 'another-work-root', 'old work stop')
  assert.equal(published, true, 'the stop terminal must reach the shared port')
}

for (const kind of ['Failed', 'Aborted']) {
  test('WHAT[delegation-027] root affinity also rejects old ' + kind + ' without a prepared handoff', async () => {
    const owner = 'unprepared-affinity-' + kind
    await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
      const port = await runtime.terminalPort
      const terminals = await observeTerminals(runtime)
      try {
      const started = await forkCall(hooks, owner, 'Ada', 'WORK-WITHOUT-HANDOFF')
      assert.match(started, /Ada/)
      const child = createdIds[0]
      const agentId = agentIdOf(started)
      const root = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id

      const before = workLifecycleOf(runtime, owner, agentId, child, root)
      assert.equal(before, 'Active')

      // The old root's stop does not belong to this work: the work unit keeps
      // its own lifecycle.
      await stopForRoot(port, child, kind)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, root), before)

      // The current work still completes through a real delivery.
      await childTurn(hooks, runtime, child, `run-${child}-1`, 'CURRENT-WORK-PROSE', 'CURRENT-WORK-ANSWER', terminals.awaitRun)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, root), 'CompletedAwaitingJoin')

      const result = await joinCall(hooks, owner)
      assert.match(result, /CURRENT-WORK-ANSWER/)
      assert.doesNotMatch(result, /old work stop/)
      } finally {
        terminals.dispose()
      }
    })
  })
}

test('WHAT[delegation-027] busy guidance preserves the original work and completion before an idle resume starts new work', async () => {
  const owner = 'owner-busy-road'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const first = await forkCall(hooks, owner, 'Ada', 'FIRST')
      assert.match(first, /Ada/)
      const child = createdIds[0]
      const agentId = agentIdOf(first)
      const root = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id

      // The child turn is running: the assistant message is there, but nothing
      // is delivered and no terminal exists yet.
      const physical = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1)
      const assistant = {
        info: {
          id: `run-${child}-1`,
          role: 'assistant',
          sessionID: child,
          parentID: physical.id,
          agent: 'engineer',
          providerID: 'provider',
          modelID: 'engineer-model',
          time: { created: 2 },
        },
        parts: [{ type: 'text', text: 'WORK IN PROGRESS' }],
      }
      runtime.pushHostMessage(child, assistant)
      await hooks['experimental.chat.messages.transform'](
        { sessionID: child },
        {
          messages: [
            {
              info: {
                id: physical.id,
                role: 'user',
                sessionID: child,
                agent: 'engineer',
                model: { providerID: 'provider', modelID: 'engineer-model' },
                time: { created: 1 },
              },
              parts: physical.parts ?? [],
            },
          ],
        },
      )

      const promptsBefore = runtime.prompts.length
      const guided = await resumeCall(hooks, owner, 'Ada', 'GUIDANCE-WHILE-BUSY')
      assert.match(guided, /Ada/)
      assert.equal(runtime.prompts.length, promptsBefore + 1, guided)
      const guidancePrompt = runtime.prompts.at(-1)?.body?.parts?.[0]?.text ?? ''
      assert.match(guidancePrompt, /GUIDANCE-WHILE-BUSY/)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, root), 'Active')
      assert.equal(
        terminals.seen.filter((entry) => entry.sessionId === child).length,
        0,
        'a busy guidance never publishes a terminal for the running child',
      )

      // The original work completes through its own real delivery.
      await hooks.tool['todowrite-for-engineer'].execute(
        { program: 'class Js extends JsProgram { async run() { await this.deliver({ 0: "FIRST-DONE" }); } }' },
        { sessionID: child, agent: 'engineer', callID: `call-run-${child}-1`, messageID: `run-${child}-1` },
      )
      assistant.info.time.completed = 3
      assistant.info.finish = 'stop'
      await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await terminals.awaitRun(child, `run-${child}-1`)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, root), 'CompletedAwaitingJoin')

      // An idle resume starts new work.
      const next = await resumeCall(hooks, owner, 'Ada', 'SECOND-AFTER-COMPLETION')
      assert.match(next, /Ada/)
      assert.equal(createdIds.length, 1, 'the same Byname reuses the same physical child')
      const secondRoot = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id
      assert.notEqual(secondRoot, root, 'the new work roots a new physical input')
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, secondRoot), 'Active')

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'SECOND PROSE', 'SECOND-DONE', terminals.awaitRun)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, secondRoot), 'CompletedAwaitingJoin')

      const joined = await joinCall(hooks, owner)
      assert.match(joined, /FIRST-DONE/)
      assert.match(joined, /SECOND-DONE/)
    } finally {
      terminals.dispose()
    }
  })
})

for (const byname of ['Ada', 'devops']) {
  for (const replacement of ['active', 'finished', 'successor']) {
    test(`WHAT[delegation-027] prepared busy guidance for ${byname} stays bound to the original ${replacement} work`, async () => {
      const owner = `busy-guidance-${byname}-${replacement}`
      await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
        const terminals = await observeTerminals(runtime)
        try {
          // The attachment source delivers its own view for real.
          const source = await forkCall(hooks, owner, 'Source', 'ATTACHMENT-SOURCE', 'call-source')
          assert.match(source, /Source/)
          const sourceChild = createdIds[0]
          await childTurn(hooks, runtime, sourceChild, `run-${sourceChild}-1`, 'SOURCE PROSE', 'SOURCE-DONE', null)
          await terminals.awaitRun(sourceChild, `run-${sourceChild}-1`)

          if (byname === 'devops') {
            await injectAcceptedAssessment(runtime, owner)
            const original = await resumeCall(hooks, owner, byname, 'ORIGINAL-WORK', 'call-original')
            assert.match(original, /devops/)
            const originalChild = createdIds[createdIds.length - 1]
            const originalRoot = runtime.messages.filter((message) => message.id?.startsWith(`msg-${originalChild}-`)).at(-1).id
            const originalAgentId = agentIdOf(original)

            if (replacement !== 'active') {
              await childTurn(hooks, runtime, originalChild, `run-${originalChild}-1`, 'ORIGINAL PROSE', 'ORIGINAL-DONE', null, byname === 'devops' ? 'devops' : 'engineer')
              await terminals.awaitRun(originalChild, `run-${originalChild}-1`)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'CompletedAwaitingJoin',
              )
            }

            let successorRoot
            if (replacement === 'successor') {
              const successor = await resumeCall(hooks, owner, byname, 'SUCCESSOR-WORK', 'call-successor')
              assert.match(successor, /devops/)
              successorRoot = runtime.messages.filter((message) => message.id?.startsWith(`msg-${originalChild}-`)).at(-1).id
            }

            const beforeRelease = runtime.prompts.length
            const guidance = await resumeCall(hooks, owner, byname, 'ORIGINAL-GUIDANCE', 'call-guidance', 'Source')
            assert.match(guidance, /devops/)

            if (replacement === 'active') {
              assert.equal(runtime.prompts.length, beforeRelease + 1)
              const first = runtime.prompts.at(-1)?.body?.parts?.[0]?.text ?? ''
              assert.match(first, /ORIGINAL-GUIDANCE/)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'Active',
                'the guidance does not complete the original work',
              )
              const second = await resumeCall(hooks, owner, byname, 'ORIGINAL-GUIDANCE', 'call-guidance-2', 'Source')
              assert.match(second, /devops/)
              assert.equal(runtime.prompts.length, beforeRelease + 2, 'identical text is a real independent guidance send')
              assert.equal(runtime.prompts.at(-1)?.body?.parts?.[0]?.text, first)
              assert.notEqual(
                runtime.prompts.at(-1)?.body?.metadata?.wanxiangshu_prompt_key,
                runtime.prompts.at(-2)?.body?.metadata?.wanxiangshu_prompt_key,
              )
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'Active',
              )
              await childTurn(hooks, runtime, originalChild, `run-${originalChild}-1`, 'ORIGINAL PROSE', 'ORIGINAL-DONE', null, byname === 'devops' ? 'devops' : 'engineer')
              await terminals.awaitRun(originalChild, `run-${originalChild}-1`)
            } else {
              assert.equal(runtime.prompts.length, beforeRelease + 1)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'CompletedAwaitingJoin',
              )
            }

            const joined = await joinCall(hooks, owner)
            assert.match(joined, /ORIGINAL-DONE/)
            if (replacement === 'successor') {
              const successorChild = createdIds[createdIds.length - 1]
              await childTurn(hooks, runtime, successorChild, `run-${successorChild}-2`, 'SUCCESSOR PROSE', 'SUCCESSOR-DONE', null, byname === 'devops' ? 'devops' : 'engineer')
              await terminals.awaitRun(successorChild, `run-${successorChild}-2`)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, successorChild, successorRoot),
                'CompletedAwaitingJoin',
              )
              const afterSuccessor = await joinCall(hooks, owner, 'call-join-successor')
              assert.match(afterSuccessor, /SUCCESSOR-DONE/)
            }
            return
          } else {
            const original = await forkCall(hooks, owner, byname, 'ORIGINAL-WORK', 'call-original')
            assert.match(original, /Ada/)
            const originalChild = createdIds[createdIds.length - 1]
            const originalRoot = runtime.messages.filter((message) => message.id?.startsWith(`msg-${originalChild}-`)).at(-1).id
            const originalAgentId = agentIdOf(original)

            if (replacement !== 'active') {
              await childTurn(hooks, runtime, originalChild, `run-${originalChild}-1`, 'ORIGINAL PROSE', 'ORIGINAL-DONE', null, byname === 'devops' ? 'devops' : 'engineer')
              await terminals.awaitRun(originalChild, `run-${originalChild}-1`)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'CompletedAwaitingJoin',
              )
            }

            let successorRoot
            if (replacement === 'successor') {
              const successor = await resumeCall(hooks, owner, byname, 'SUCCESSOR-WORK', 'call-successor')
              assert.match(successor, /Ada/)
              // The successor work roots the physical input the successor
              // resume created — the guidance that follows roots its own.
              successorRoot = runtime.messages.filter((message) => message.id?.startsWith(`msg-${originalChild}-`)).at(-1).id
            }

            const beforeRelease = runtime.prompts.length
            const guidance = await resumeCall(hooks, owner, byname, 'ORIGINAL-GUIDANCE', 'call-guidance', 'Source')
            assert.match(guidance, /Ada/)

            if (replacement === 'active') {
              // The guidance reaches the Host as a real prompt, and the original
              // work keeps its own lifecycle.
              assert.equal(runtime.prompts.length, beforeRelease + 1)
              const first = runtime.prompts.at(-1)?.body?.parts?.[0]?.text ?? ''
              assert.match(first, /ORIGINAL-GUIDANCE/)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'Active',
                'the guidance does not complete the original work',
              )
              // The same text sent twice is two independent sends.
              const second = await resumeCall(hooks, owner, byname, 'ORIGINAL-GUIDANCE', 'call-guidance-2', 'Source')
              assert.match(second, /Ada/)
              assert.equal(runtime.prompts.length, beforeRelease + 2, 'identical text is a real independent guidance send')
              const again = runtime.prompts.at(-1)?.body?.parts?.[0]?.text ?? ''
              assert.equal(again, first)
              assert.notEqual(
                runtime.prompts.at(-1)?.body?.metadata?.wanxiangshu_prompt_key,
                runtime.prompts.at(-2)?.body?.metadata?.wanxiangshu_prompt_key,
              )
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'Active',
              )
              await childTurn(hooks, runtime, originalChild, `run-${originalChild}-1`, 'ORIGINAL PROSE', 'ORIGINAL-DONE', null, byname === 'devops' ? 'devops' : 'engineer')
              await terminals.awaitRun(originalChild, `run-${originalChild}-1`)
            } else {
              // The original work is already closed (or superseded): the
              // guidance is bound to that work and never becomes a new task.
              assert.equal(runtime.prompts.length, beforeRelease + 1)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, originalChild, originalRoot),
                'CompletedAwaitingJoin',
              )
            }

            const joined = await joinCall(hooks, owner)
            assert.match(joined, /ORIGINAL-DONE/)
            if (replacement === 'successor') {
              const successorChild = createdIds[createdIds.length - 1]
              await childTurn(hooks, runtime, successorChild, `run-${successorChild}-2`, 'SUCCESSOR PROSE', 'SUCCESSOR-DONE', null, byname === 'devops' ? 'devops' : 'engineer')
              await terminals.awaitRun(successorChild, `run-${successorChild}-2`)
              assert.equal(
                workLifecycleOf(runtime, owner, originalAgentId, successorChild, successorRoot),
                'CompletedAwaitingJoin',
              )
              const afterSuccessor = await joinCall(hooks, owner, 'call-join-successor')
              assert.match(afterSuccessor, /SUCCESSOR-DONE/)
            }
          }
        } finally {
          terminals.dispose()
        }
      })
    })
  }
}

for (const phase of ['GUIDANCE', 'GUIDANCE_STOP']) {
  integrationTest(`WHAT[delegation-027] installed Host releases Manager Join and appends guidance after the child ${phase === 'GUIDANCE_STOP' ? 'final output' : 'tool step'}`, () => {
    const runner = fileURLToPath(new URL('../../host-boundary/tests/support/run-user-input-canary.mjs', import.meta.url))
    const launched = spawnSync(process.execPath, [runner, '--phase', phase], {
      encoding: 'utf8', timeout: 120000,
    })
    assert.equal(launched.status, 0, `${launched.error ?? ''}\n${launched.stdout}\n${launched.stderr}`)
    assert.equal(JSON.parse(launched.stdout.trim()).physicalCleanup, true)
  })
}
