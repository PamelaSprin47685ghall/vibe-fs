import assert from 'node:assert/strict'
import test from 'node:test'

import {
  acceptAuthorityRoot,
  acceptChildAgentOwnerRoot,
  withExecutablePlugin,
} from '../../../verification-system/tests/support/plugin-fixture.mjs'
import { fallbackProjection, fold, idValue } from '../../../verification-system/tests/support/domain.mjs'
import { AgentJournalModule_snapshot } from '../../../../dist/Persistence/Journal/AgentJournal.js'

const message = (sessionID, id, role, text, completed = false) => ({
  info: {
    id,
    role,
    sessionID,
    ...(completed ? { time: { completed: Date.now() } } : {}),
  },
  parts: [{ type: 'text', text }],
})

const promptKeyOf = (prompt) => {
  const textPart = prompt?.body?.parts?.find((part) => part?.type === 'text')
  return (
    prompt?.body?.metadata?.wanxiangshu_prompt_key ??
    textPart?.metadata?.wanxiangshu_prompt_key
  )
}

const promptTextOf = (prompt) =>
  prompt?.body?.parts?.find((part) => part?.type === 'text')?.text ?? ''

test('REPRO_blogger_pure_prose_terminal_sends_interaction_nudge_through_real_plugin_transform', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const mainSessionId = 'ses-repro-main'
    await acceptAuthorityRoot(runtime, mainSessionId, 'fast-coder')

    const mainView = {
      messages: [message(mainSessionId, 'msg-main-user', 'user', 'Please inspect this work.')],
    }

    await hooks['experimental.chat.messages.transform']({}, mainView)

    assert.equal(createdIds.length, 1, 'main material should create exactly one companion Blogger')
    const bloggerSessionId = createdIds[0]
    assert.equal(runtime.prompts.length, 1, 'Blogger should receive its initial work prompt')

    const initialPrompt = runtime.prompts[0]
    const initialPromptKey = promptKeyOf(initialPrompt)
    assert.ok(initialPromptKey, 'initial Blogger prompt must carry a PromptKey')
    await acceptChildAgentOwnerRoot(runtime, bloggerSessionId, initialPromptKey)

    const sendsBeforePureProse = runtime.prompts.length
    const bloggerView = {
      messages: [
        message(bloggerSessionId, 'msg-blogger-user', 'user', promptTextOf(initialPrompt)),
        message(
          bloggerSessionId,
          'asst-blogger-prose-only',
          'assistant',
          'I inspected the work and found several issues, but I am returning prose instead of calling chronicle.',
          true,
        ),
      ],
    }

    await hooks['experimental.chat.messages.transform']({}, bloggerView)

    assert.equal(
      runtime.prompts.length,
      sendsBeforePureProse + 1,
      'pure-prose Blogger terminal must physically send one interaction repair nudge',
    )

    const nudge = runtime.prompts.at(-1)
    assert.equal(nudge?.path?.id, bloggerSessionId)
    assert.match(promptTextOf(nudge), /chronicle tool exactly once|Protocol repair/i)
  })
})

test('REPRO_blogger_pure_prose_terminal_idle_should_nudge_without_another_transform', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const mainSessionId = 'ses-repro-idle-main'
    await acceptAuthorityRoot(runtime, mainSessionId, 'fast-coder')

    const mainView = {
      messages: [message(mainSessionId, 'msg-idle-main-user', 'user', 'Please inspect this work.')],
    }
    await hooks['experimental.chat.messages.transform']({}, mainView)

    assert.equal(createdIds.length, 1)
    const bloggerSessionId = createdIds[0]
    assert.equal(runtime.prompts.length, 1)

    const initialPrompt = runtime.prompts[0]
    const initialPromptKey = promptKeyOf(initialPrompt)
    assert.ok(initialPromptKey)

    const physicalPrompt = runtime.messages.find(
      (candidate) => candidate?.metadata?.wanxiangshu_prompt_key === initialPromptKey,
    )
    assert.ok(physicalPrompt, 'fixture must expose the physical Blogger prompt message')

    // Real Host acceptance: chat.message promotes the pending AgentOwnerRoot
    // using the actual physical user message id and binds that same id into the
    // turn reconciler. The synthetic acceptChild helper is insufficient here.
    await hooks['chat.message'](
      { sessionID: bloggerSessionId, agent: 'fast-blogger' },
      {
        message: {
          id: physicalPrompt.id,
          role: 'user',
          sessionID: bloggerSessionId,
          agent: 'fast-blogger',
        },
        parts: physicalPrompt.parts,
      },
    )

    const bloggerProjection = fold.session(AgentJournalModule_snapshot(runtime.journal), bloggerSessionId)
    assert.ok(bloggerProjection?.PromptAuthority?.ActiveLogicalRun, 'chat.message must accept the Blogger AgentOwnerRoot')
    assert.equal(
      idValue.authorityRoot(bloggerProjection.PromptAuthority.ActiveLogicalRun.AuthorityRootUserMessageId),
      physicalPrompt.id,
      'idle reconcile must be bound to the actual physical Blogger prompt message',
    )

    // Real Host sequence: the Blogger's initial provider request runs the messages
    // transform before the LLM responds. There is no second transform after the
    // prose terminal.
    const initialBloggerView = {
      messages: [
        {
          info: { id: physicalPrompt.id, role: 'user', sessionID: bloggerSessionId, agent: 'fast-blogger' },
          parts: physicalPrompt.parts,
        },
      ],
    }
    await hooks['experimental.chat.messages.transform']({}, initialBloggerView)

    runtime.pushHostMessage(
      bloggerSessionId,
      message(
        bloggerSessionId,
        'asst-blogger-idle-prose-only',
        'assistant',
        'I inspected the work and found several issues, but I am returning prose instead of calling chronicle.',
        true,
      ),
    )

    const sendsBeforeIdle = runtime.prompts.length
    hooks.event({ type: 'session.idle', properties: { sessionID: bloggerSessionId } })

    // Scheduler is fire-and-forget from the Host event hook; give the bounded
    // reconcile/reread pass a short deterministic observation window.
    await new Promise((resolve) => setTimeout(resolve, 250))

    assert.equal(
      runtime.prompts.length,
      sendsBeforeIdle + 1,
      'BUG reproduced: idle after a prose-only Blogger terminal sends no repair nudge, and no later provider transform exists to do it',
    )

    const idleNudge = runtime.prompts.at(-1)
    assert.equal(idleNudge?.path?.id, bloggerSessionId)
    assert.match(promptTextOf(idleNudge), /chronicle tool exactly once|Protocol repair/i)
  })
})

test('REPRO_blogger_second_prose_terminal_idle_spends_aabb_not_second_nudge', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const mainSessionId = 'ses-repro-idle-aabb-main'
    await acceptAuthorityRoot(runtime, mainSessionId, 'fast-coder')

    await hooks['experimental.chat.messages.transform']({}, {
      messages: [message(mainSessionId, 'msg-aabb-main-user', 'user', 'Please inspect this work.')],
    })

    const bloggerSessionId = createdIds[0]
    const initialPrompt = runtime.prompts[0]
    const initialPromptKey = promptKeyOf(initialPrompt)
    const physicalPrompt = runtime.messages.find(
      (candidate) => candidate?.metadata?.wanxiangshu_prompt_key === initialPromptKey,
    )
    assert.ok(physicalPrompt)

    const acceptPrompt = async (promptMessage, agent) => {
      await hooks['chat.message'](
        { sessionID: bloggerSessionId, agent },
        {
          message: {
            id: promptMessage.id,
            role: 'user',
            sessionID: bloggerSessionId,
            agent,
          },
          parts: promptMessage.parts,
        },
      )
      await hooks['experimental.chat.messages.transform']({}, {
        messages: [{
          info: { id: promptMessage.id, role: 'user', sessionID: bloggerSessionId, agent },
          parts: promptMessage.parts,
        }],
      })
    }

    await acceptPrompt(physicalPrompt, 'fast-blogger')
    runtime.pushHostMessage(
      bloggerSessionId,
      message(bloggerSessionId, 'asst-aabb-p1', 'assistant', 'first prose-only response', true),
    )
    hooks.event({ type: 'session.idle', properties: { sessionID: bloggerSessionId } })
    await new Promise((resolve) => setTimeout(resolve, 250))

    assert.equal(runtime.prompts.length, 2, 'first invalid terminal sends exactly one nudge')
    const firstNudge = runtime.prompts[1]
    const firstNudgeKey = promptKeyOf(firstNudge)
    assert.ok(firstNudgeKey)
    const firstNudgeMessage = runtime.messages.find(
      (candidate) => candidate?.metadata?.wanxiangshu_prompt_key === firstNudgeKey,
    )
    assert.ok(firstNudgeMessage)
    await acceptPrompt(firstNudgeMessage, 'fast-blogger')

    runtime.pushHostMessage(
      bloggerSessionId,
      message(bloggerSessionId, 'asst-aabb-p2', 'assistant', 'still prose-only after nudge', true),
    )
    hooks.event({ type: 'session.idle', properties: { sessionID: bloggerSessionId } })
    await new Promise((resolve) => setTimeout(resolve, 250))

    assert.equal(runtime.prompts.length, 3, 'second invalid terminal sends one AABB continuation, not another nudge')
    const aabb = runtime.prompts[2]
    assert.equal(aabb?.path?.id, bloggerSessionId)
    assert.match(promptTextOf(aabb), /chronicle tool exactly once|Protocol repair/i)

    const afterAabb = fold.session(AgentJournalModule_snapshot(runtime.journal), bloggerSessionId)
    const fallback = fallbackProjection.read(afterAabb.Fallback)
    assert.equal(fallback.offset, 1, 'AABB advances the A/A/B/B cursor exactly once')
    assert.equal(fallback.failures, 1, 'AABB consumes exactly one fallback failure unit')

    const duplicateAabbTerminal = new Promise((resolve) => {
      runtime.terminalPort.SubscribeTerminalListener((sid, outcome) => {
        if (sid?.fields?.[0] === bloggerSessionId) resolve(outcome)
      })
    })
    hooks.event({ type: 'session.idle', properties: { sessionID: bloggerSessionId } })
    const duplicateAabbOutcome = await Promise.race([
      duplicateAabbTerminal,
      new Promise((resolve) => setTimeout(() => resolve('no-terminal'), 150)),
    ])
    assert.equal(
      duplicateAabbOutcome,
      'no-terminal',
      'same invalid terminal re-entry after AABB claim must be idempotent, not exhaust repair',
    )
    assert.equal(runtime.prompts.length, 3, 'same terminal re-entry must not send another automatic prompt')

    const aabbKey = promptKeyOf(aabb)
    const aabbMessage = runtime.messages.find(
      (candidate) => candidate?.metadata?.wanxiangshu_prompt_key === aabbKey,
    )
    assert.ok(aabbMessage)
    await acceptPrompt(aabbMessage, aabb?.body?.agent ?? 'fast-blogger')

    const exhaustedTerminal = new Promise((resolve) => {
      runtime.terminalPort.SubscribeTerminalListener((sid, outcome) => {
        if (sid?.fields?.[0] === bloggerSessionId) resolve(outcome)
      })
    })

    runtime.pushHostMessage(
      bloggerSessionId,
      message(bloggerSessionId, 'asst-aabb-p3', 'assistant', 'third prose-only response', true),
    )
    hooks.event({ type: 'session.idle', properties: { sessionID: bloggerSessionId } })

    const exhausted = await Promise.race([
      exhaustedTerminal,
      new Promise((resolve) => setTimeout(() => resolve('no-terminal'), 250)),
    ])
    assert.notEqual(exhausted, 'no-terminal', 'AABB-exhausted Blogger must terminate instead of stalling')
    assert.equal(runtime.prompts.length, 3, 'AABB-exhausted Blogger must not send another automatic prompt')
  })
})
