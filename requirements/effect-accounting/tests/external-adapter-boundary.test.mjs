import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as pluginHooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as recovery from '../../../dist/Interaction/Dispatch/RecoverySurface.js'
import * as todoHost from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'
import * as todoMembrane from '../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

const withJournal = async (prefix, writer, runtime, body) => {
  const directory = mkdtempSync(join(tmpdir(), prefix))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, writer, runtime, 4242, '2026-08-30T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    return await body(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[EFFECT-ACCOUNTING-008] Adapter Blogger Coordinator submits one receipt and recovers exact physical acceptance without resend', async () => {
  await withJournal('wxs-blogger-coordinator-', 'writer-blogger-coordinator', 'rt-blogger-coordinator', async (handle) => {
    const bloggerSession = 'ses-blogger-effect-008'
    const physicalUserMessageId = 'msg-blogger-effect-008'
    const submissions = []
    let childListCalls = 0
    const childCreates = []
    const host = {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async (session, text, options) => {
        submissions.push({ session, text, options })
        return dispatch.admittedWithReceipt('accepted-host-blogger-008')
      },
      AbortSession: async () => ({ ok: true }),
      InterruptAttempt: async () => ({ ok: true }),
      IsManagedChild: () => true,
      AbortChildren: async () => {},
      CreateSiblingSession: async () => ({ ok: false, error: 'unexpected sibling creation' }),
      TryGetParentSession: async () => ({ ok: true, value: undefined }),
      CreateChildSession: async (parent, options) => {
        childCreates.push({ parent, options })
        return { ok: true, value: bloggerSession }
      },
      ListChildren: async () => {
        childListCalls += 1
        return dispatch.acceptedChild(bloggerSession, 'fast-blogger', 'fast-blogger')
      },
      FamilyRootOf: () => 'ses-main-effect-008',
    }

    const decision = await pluginHooks.coordinateBloggerUnresolvedTwice(
      host,
      handle,
      'ses-main-effect-008',
      bloggerSession,
      'request-blogger-effect-008',
    )
    assert.equal(pluginHooks.firstBloggerEffect(decision), 'StartedSquash')
    assert.equal(pluginHooks.secondBloggerEffect(decision), 'SkippedInFlight')
    assert.equal(childListCalls, 2, 'SatelliteRuntime must inspect root and owner child listings')
    assert.equal(childCreates.length, 0, 'the exact restored Blogger child must be reused')
    assert.equal(submissions.length, 1)
    assert.equal(submissions[0].session, bloggerSession)

    const submitted = dispatch.projectionObservation(handle, bloggerSession)
    assert.equal(submitted.pendingClaims.length, 1)
    const claim = submitted.pendingClaims[0]
    assert.equal(claim.promptKey, submissions[0].options.Metadata.wanxiangshu_prompt_key)
    assert.equal(claim.receipt, `accepted-detached-${claim.promptKey}`)

    const unresolved = await recovery.reconcile(handle, [])
    assert.deepEqual(unresolved.map(({ promptKey, outcome }) => ({ promptKey, outcome })), [
      { promptKey: claim.promptKey, outcome: 'StillPending' },
    ])
    assert.equal(submissions.length, 1)

    const proven = await recovery.reconcile(handle, [{
      id: physicalUserMessageId,
      role: 'user',
      metadata: { wanxiangshu_prompt_key: claim.promptKey },
    }])
    assert.deepEqual(proven.map(({ promptKey, outcome, physicalMessageId }) => ({ promptKey, outcome, physicalMessageId })), [
      { promptKey: claim.promptKey, outcome: 'Proven', physicalMessageId: physicalUserMessageId },
    ])
    assert.equal(dispatch.projectionObservation(handle, bloggerSession).pendingClaims.length, 0)
    assert.equal(submissions.length, 1)
  })
})

test('WHAT[EFFECT-ACCOUNTING-011] Adapter Todo Host Before executor After commits exact checkpoint and recovery does not repeat mutation', async () => {
  await withJournal('wxs-todo-host-adapter-', 'writer-todo-host', 'rt-todo-host', async (handle) => {
    const session = 'ses-todo-effect-011'
    const life = 'life-todo-effect-011'
    const call = 'call-todo-effect-011'
    const args = {
      planComplete: true,
      workingOn: 'verify-adapter',
      obligations: [{
        name: 'verify-adapter',
        horizon: 'near',
        work: 'Prove the physical Todo Host workflow reaches durable acceptance.',
      }],
    }
    const canonicalInput = todoHost.canonicalInput(args)
    const inputDigest = todoHost.canonicalInputDigest(sha256Hex, args)
    const messages = [
      {
        info: { id: 'msg-opening', role: 'user' },
        parts: [{ type: 'text', text: 'Open the durable Todo workflow.' }],
      },
      {
      info: { id: 'msg-provider-todo-011', role: 'assistant' },
      parts: [{
        type: 'tool',
        id: 'part-todo-011',
        callID: call,
        tool: 'todowrite',
        state: { status: 'pending', input: args },
      }],
    }]

    const openedLife = await todoMembrane.MagicTodoMembraneSurface_openLife(handle, session, life)
    assert.equal(openedLife.ok, true, openedLife.ok ? '' : openedLife.error)

    let executorCalls = 0
    const physicalOutput = 'physical-todo-success-011'
    const execution = await todoMembrane.MagicTodoMembraneSurface_executeHostSuccess(
      handle,
      messages,
      session,
      life,
      call,
      args,
      (compatibilityArgs) => {
        executorCalls += 1
        assert.equal(compatibilityArgs.todos.length, 1, 'real Before must adapt provider obligations for the builtin Host executor')
        return { title: '1 todos', output: physicalOutput, metadata: { todos: compatibilityArgs.todos } }
      },
    )

    assert.equal(executorCalls, 1)
    assert.equal(execution.life.checkpoints.length, 1)
    const checkpoint = execution.life.checkpoints[0]
    assert.deepEqual({
      toolCallId: checkpoint.toolCallId,
      accepted: checkpoint.accepted,
      inputDigest: checkpoint.inputDigest,
      outputDigest: checkpoint.outputDigest,
    }, {
      toolCallId: call,
      accepted: true,
      inputDigest,
      outputDigest: sha256Hex(JSON.stringify(physicalOutput)),
    })

    const replay = await todoMembrane.MagicTodoMembraneSurface_prepare(
      handle,
      session,
      call,
      canonicalInput,
      inputDigest,
      true,
      args.obligations,
      1,
    )
    assert.equal(replay.ok, true, replay.ok ? '' : JSON.stringify(replay.error))
    assert.equal(replay.value.prepared.todoWriteId, checkpoint.todoWriteId)
    assert.equal(executorCalls, 1, 'journal recovery must not blindly invoke the physical builtin mutation again')
    assert.equal(todoMembrane.MagicTodoMembraneSurface_snapshot(handle, life).checkpoints.length, 1)
  })
})
