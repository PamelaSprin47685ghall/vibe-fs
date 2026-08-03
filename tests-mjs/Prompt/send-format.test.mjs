// tests-mjs/Prompt/send-format.test.mjs — PROMPT-006.
//
// spec/03 PROMPT-006 fixes the send-time options at every dispatch:
//
//   { Agent = Some effectiveAgent
//     Model = None
//     Directory = directory
//     Metadata = metadata }
//
// and forbids setting Model — the Host resolves it from
// `config.agent[effectiveAgent].model`. The construction lives inside the
// `PromptDispatcher.Runtime` send members (`PromptDispatcherSend.fs`), so the
// only way to observe it is to run a real send against a port that captures
// the `options` argument `SendPrompt` receives (layer 2, journal on disk).
//
// Both send members are covered: `SendAgentOwnerRoot` binds the caller's
// agent, `SendContinuation` binds the fallback cursor's `effectiveAgent`
// (FALLBACK-004) — the "Agent = Some effectiveAgent" half of the clause.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentJournal,
  attemptPlanner,
  continuationKind,
  idValue,
  isNone,
  isSome,
  promptDispatcher,
  transportReceipt,
} from '../domain.mjs'

/** The smallest ISessionHostPort: capture the send, admit it. */
const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session: idValue.session(session), text, options })
    return promptDispatcher.admittedWithReceipt(transportReceipt('accepted-006'))
  },
})

test('PROMPT_006_send_payload_carries_agent_and_no_model', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-send-format-'))
  try {
    const opened = agentJournal.create({ directory: base })
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const runtime = promptDispatcher.forJournal(opened.journal)
      const captured = []
      const port = capturingPort(captured)

      // PROMPT-002: a plugin-owned Authority Root, sent for an explicit agent.
      const ownerRoot = await promptDispatcher.sendAgentOwnerRoot(runtime, port, {
        session: 'ses_006',
        text: 'dispatch this',
        agent: 'fast-coder',
      })
      assert.equal(ownerRoot.ok, true, ownerRoot.ok ? '' : ownerRoot.error)
      assert.ok(isSome(ownerRoot.key))

      // PROMPT-003: a continuation inherits run and root and carries the
      // fallback cursor's current choice as its effective agent.
      const continuation = await promptDispatcher.sendContinuation(runtime, port, {
        session: 'ses_006',
        text: 'retry on the other side',
        continuation: continuationKind.of('ProviderRetryAttempt'),
        profile: attemptPlanner.authority({ session: 'ses_006' }),
        effectiveAgent: 'deep-coder',
      })
      assert.equal(continuation.ok, true, continuation.ok ? '' : continuation.error)

      assert.equal(captured.length, 2, 'both send members must reach SendPrompt')

      // The payload as a whole, not just the options record: the session and
      // text reach the port unchanged, so a renamed option cannot read
      // `undefined` and pass.
      assert.deepEqual(
        captured.map((c) => ({ session: c.session, text: c.text })),
        [
          { session: 'ses_006', text: 'dispatch this' },
          { session: 'ses_006', text: 'retry on the other side' },
        ],
      )

      // Agent is bound by construction: the owner-root send binds its agent
      // argument, the continuation binds its effectiveAgent (FALLBACK-004).
      assert.deepEqual(
        { agent: captured[0].options.Agent, model: captured[0].options.Model },
        { agent: 'fast-coder', model: undefined },
        'SendAgentOwnerRoot must carry Agent = Some agent and Model = None',
      )
      assert.deepEqual(
        { agent: captured[1].options.Agent, model: captured[1].options.Model },
        { agent: 'deep-coder', model: undefined },
        'SendContinuation must carry Agent = Some effectiveAgent and Model = None',
      )

      // PROMPT-006 requires Metadata at every send. `undefined` here would mean
      // the PromptKey anchor (PROMPT-011) was dropped on the way out.
      assert.ok(isSome(captured[0].options.Metadata), 'the owner-root send must carry Metadata')
      assert.ok(isSome(captured[1].options.Metadata), 'the continuation send must carry Metadata')

      // Directory is passed through untouched: `None` stays None (no default
      // workspace injected at the dispatcher), so an absent directory cannot
      // silently switch the send to another workspace.
      assert.ok(isNone(captured[0].options.Directory), 'no directory was given')
    } finally {
      opened.dispose()
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
