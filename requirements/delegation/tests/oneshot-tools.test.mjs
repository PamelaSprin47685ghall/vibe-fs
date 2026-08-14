// tests/unit/tools/oneshot-tools.test.mjs — residual OneShotAgentTool lifecycle
// (EXEC-028 path A): create → subscribe-before-send → await one terminal →
// physically abort/dispose. Not CoderTool/InspectorTool SyncDelegate surfaces.
//
// Real AgentJournal (PROMPT-005 dispatch claims the prompt) + fake
// ISessionHostPort. The terminal subscription is installed BEFORE the prompt
// send (production order), so the fake fires the terminal from inside
// SendPrompt — the same interleaving the Host produces.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentJournal,
  resultOf,
  sessionId,
  uncurry2,
  lifecycleWorkRecordProjection,
  xTraceCapture,
} from '../../verification-system/tests/support/domain.mjs'

const { HostToolContext, ToolHostCodec_digest: digest } = await import(
  '../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js'
)
const {
  Request,
  run,
} = await import('../../../dist/Infrastructure/OpenCode/Tools/OneShotAgentTool.js')
const { ManagedAgentModule_peer: peerOf } = await import(
  '../../../dist/Infrastructure/OpenCode/Tools/ManagedAgent.js'
)
const { coderToolNames, wireTierLabel } = await import('../../../dist/Domain/ManagedAgentCatalog.js')
const {
  ToolRuntimeScope,
  ToolRuntimeScope__DirectoryFor_Z721C83C5: directoryFor,
} = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { TerminalOutcome } = await import('../../../dist/Infrastructure/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Kernel/Outcome.js')

const context = (session = 'ses-call', attachAbort) =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, attachAbort ?? (() => () => {}))

const request = (agent, prompt) => new Request(agent, prompt)

/** Invoke OneShotAgentTool.run with coder expectedNames / roleLabel by default. */
const runOneShot = (scope, ctx, agent, prompt, { expectedNames = coderToolNames, roleLabel = 'Coder' } = {}) =>
  run(scope, ctx, request(agent, prompt), expectedNames, roleLabel)

/** Fake ISessionHostPort capturing the terminal subscription and every call.
 * The Host's SubscribeTerminal is a multi-listener bus: the PromptDispatcher
 * installs a NoOp listener per send, so the fake must keep ALL callbacks. */
const fakeSessions = ({ createError } = {}) => {
  const calls = { create: 0, abort: 0, prompt: [], disposedSub: 0 }
  const terminals = new Set()
  return {
    calls,
    fireTerminal: (outcome) => {
      for (const callback of terminals) callback(sessionId('child-1'), outcome)
    },
    CreateChildSession: async (_parentId, _options) => {
      calls.create += 1
      if (createError) return { tag: 1, fields: [createError] }
      return { tag: 0, fields: [sessionId('child-1')] }
    },
    AbortSession: async () => {
      calls.abort += 1
      return { tag: 0, fields: [] }
    },
    SendPrompt: async (...args) => {
      calls.prompt.push(args)
      return { tag: 0, fields: [] }
    },
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return {
        Dispose: () => {
          terminals.delete(callback)
          calls.disposedSub += 1
        },
      }
    },
  }
}

const completedTerminal = (formalText) =>
  new TerminalOutcome(0, [
    new AgentRunResult(
      sessionId('child-1'),
      undefined,
      undefined,
      { tag: 2, fields: [] },
      undefined,
      'session-wide text',
      formalText,
    ),
  ])

/** Wait until the child prompt has been sent (terminal subscription is up). */
const waitForPrompt = async (sessions) => {
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  assert.equal(sessions.calls.prompt.length, 1, 'the child prompt must be sent')
}

/** { scope, sessions, journal, cleanup } — real journal + fake host.
 *  `childWorkRecord` / `parentWorkRecord` may be a string or `(sessionId) => string|undefined`. */
const liveScope = async ({ sessions = fakeSessions(), parentWorkRecord, childWorkRecord, directories } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-oneshot-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')

  const scope = new ToolRuntimeScope(
    sessions,
    opened.journal,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    directories ?? new Map(),
    undefined,
    parentWorkRecord
      ? async (sessionId) =>
          typeof parentWorkRecord === 'function' ? await parentWorkRecord(sessionId) : parentWorkRecord
      : undefined,
    childWorkRecord
      ? async (sessionId) =>
          typeof childWorkRecord === 'function' ? await childWorkRecord(sessionId) : childWorkRecord
      : undefined,
    undefined,
    undefined,
  )
  return {
    scope,
    sessions,
    journal: opened.journal,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

/** Scope without a journal: the one-shot prompt cannot be claimed. */
const bareScope = ({ sessions = fakeSessions() } = {}) =>
  new ToolRuntimeScope(
    sessions,
    undefined,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )

const SAMPLE_LWR = [
  'Chronicle',
  'historic frame content',
  'Recent work',
  'gap content',
].join('\n')

// ── early refusal (no spawn) ─────────────────────────────────────────────────

test('ONESHOT_blank_session_is_refused_before_spawn', async () => {
  const sessions = fakeSessions()
  const settled = resultOf(await runOneShot(bareScope({ sessions }), context(''), 'fast-coder', 'work'))
  assert.equal(settled.ok, false)
  assert.equal(settled.error, 'Missing sessionID')
  assert.equal(sessions.calls.create, 0)
})

test('ONESHOT_missing_prompt_is_refused_before_spawn', async () => {
  const sessions = fakeSessions()
  const settled = resultOf(await runOneShot(bareScope({ sessions }), context(), 'fast-coder', ''))
  assert.equal(settled.ok, false)
  assert.equal(settled.error, 'coder prompt required')
  assert.equal(sessions.calls.create, 0)
})

test('ONESHOT_create_session_failure_surfaces_host_error', async () => {
  const live = await liveScope({ sessions: fakeSessions({ createError: 'host refused' }) })
  try {
    const settled = resultOf(await runOneShot(live.scope, context(), 'fast-coder', 'work'))
    assert.equal(settled.ok, false)
    assert.equal(settled.error, 'host refused')
  } finally {
    live.cleanup()
  }
})

// ── full lifecycle ───────────────────────────────────────────────────────────

test('ONESHOT_success_reports_outcome_and_disposes_the_child', async () => {
  const sessions = fakeSessions()
  const live = await liveScope({ sessions, childWorkRecord: SAMPLE_LWR })

  try {
    const pending = runOneShot(live.scope, context(), 'fast-coder', 'implement it')
    await waitForPrompt(sessions)
    sessions.fireTerminal(completedTerminal('the formal report'))

    const settled = resultOf(await pending)
    assert.equal(settled.ok, true, settled.ok ? '' : settled.error)
    const outcome = settled.value
    assert.equal(outcome.ChildId, 'child-1')
    assert.equal(outcome.Managed.Name, 'fast-coder')
    assert.equal(wireTierLabel(outcome.Managed.Tier), 'fast')
    assert.equal(peerOf(outcome.Managed).Name, 'deep-coder')
    assert.equal(outcome.ParentBackgroundDigest, undefined)
    // COMPANION-005: Output is turn-formal text, not session-wide text.
    assert.equal(outcome.Output, 'the formal report')
    // EXEC-028: WorkRecord is the child LWR (includeOpening=false path).
    assert.equal(outcome.WorkRecord, SAMPLE_LWR)
    assert.doesNotMatch(outcome.WorkRecord, /Opening task/)
    assert.doesNotMatch(outcome.WorkRecord, /^Opening\n/m)
    assert.equal(sessions.calls.abort, 1, 'the child is physically aborted after the terminal')
    // PromptDispatcher installs and disposes its own NoOp terminal listener per
    // send, so the count covers both the tool's subscription and the dispatcher's.
    assert.ok(sessions.calls.disposedSub >= 1, 'the terminal subscription is disposed')
  } finally {
    live.cleanup()
  }
})

test('ONESHOT_completed_without_lifecycle_work_record_fails_closed', async () => {
  // EXEC-028: Completed with formal text but missing LWR must not soft-omit to
  // formal-only Ok — surface as Result.Error.
  const sessions = fakeSessions()
  const live = await liveScope({ sessions })

  try {
    const pending = runOneShot(live.scope, context(), 'fast-coder', 'work')
    await waitForPrompt(sessions)
    sessions.fireTerminal(completedTerminal('formal only without LWR'))

    const settled = resultOf(await pending)
    assert.equal(settled.ok, false)
    assert.match(settled.error ?? '', /EXEC-028|LifecycleWorkRecord|WorkRecord/i)
  } finally {
    live.cleanup()
  }
})

test('ONESHOT_completed_materializes_lifecycle_work_record_from_real_journal', async () => {
  // EXEC-028: prove Opening→LWR via real journal (fails if captureOpening is removed).
  // Stubbed childWorkRecord strings cannot catch that production break.
  const sessions = fakeSessions()
  let journal
  const live = await liveScope({
    sessions,
    childWorkRecord: (sid) => lifecycleWorkRecordProjection.lifecycleWorkRecord(journal, sessionId(sid), false),
  })
  journal = live.journal

  try {
    const pending = runOneShot(live.scope, context(), 'fast-coder', 'implement it')
    await waitForPrompt(sessions)
    // Immediate openingEnd skips the first XTrace part (the user charge).
    // The assistant answer must be a later part or Recent work is empty.
    await xTraceCapture.captureProjection(
      journal,
      sessionId('child-1'),
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('implement it')] },
          { role: 'assistant', parts: [xTraceCapture.text('the formal report')] },
        ],
      }),
    )
    sessions.fireTerminal(completedTerminal('the formal report'))

    const settled = resultOf(await pending)
    assert.equal(settled.ok, true, settled.ok ? '' : settled.error)
    const outcome = settled.value
    assert.equal(outcome.Output, 'the formal report')
    assert.ok(outcome.WorkRecord, 'LWR must materialize from the journal')
    assert.match(outcome.WorkRecord, /Recent work/)
    assert.match(outcome.WorkRecord, /the formal report/)
    assert.doesNotMatch(outcome.WorkRecord, /Closing report/)
    assert.doesNotMatch(outcome.WorkRecord, /# # /)
    assert.doesNotMatch(outcome.WorkRecord, /Opening task/)
    assert.doesNotMatch(outcome.WorkRecord, /Work log|Uncompressed tail|Final output/)
  } finally {
    live.cleanup()
  }
})

test('ONESHOT_parent_work_record_lands_in_the_digest_field', async () => {
  const sessions = fakeSessions()
  const parentBody = 'the parent background record'
  const live = await liveScope({ sessions, parentWorkRecord: parentBody, childWorkRecord: 'child LWR body' })

  try {
    const pending = runOneShot(live.scope, context(), 'fast-coder', 'work')
    await waitForPrompt(sessions)
    sessions.fireTerminal(completedTerminal('report'))

    const settled = resultOf(await pending)
    assert.equal(settled.ok, true, settled.ok ? '' : settled.error)
    assert.equal(settled.value.ParentBackgroundDigest, digest(parentBody))
  } finally {
    live.cleanup()
  }
})

test('ONESHOT_child_inherits_the_parent_directory', async () => {
  const sessions = fakeSessions()
  const live = await liveScope({
    sessions,
    childWorkRecord: SAMPLE_LWR,
    directories: new Map([['ses-call', '/tmp']]),
  })

  try {
    const pending = runOneShot(live.scope, context(), 'fast-coder', 'work')
    await waitForPrompt(sessions)
    sessions.fireTerminal(completedTerminal('report'))

    const settled = resultOf(await pending)
    assert.equal(settled.ok, true, settled.ok ? '' : settled.error)
    assert.ok(settled.value.ChildId.length > 0, 'completed child yields an id')
    assert.equal(directoryFor(live.scope, 'child-1'), '/tmp', 'the child directory is registered')
  } finally {
    live.cleanup()
  }
})

test('ONESHOT_send_failure_is_reported_as_output_not_thrown', async () => {
  const sessions = fakeSessions()
  // No journal → the prompt claim fails; run still completes the one-shot.
  const pending = runOneShot(bareScope({ sessions }), context(), 'fast-coder', 'work')
  for (let attempt = 0; attempt < 100 && sessions.calls.create === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }

  const settled = resultOf(await pending)
  assert.equal(settled.ok, true, settled.ok ? '' : settled.error)
  assert.match(settled.value.Output, /send failed: No journal/)
  assert.equal(settled.value.WorkRecord, undefined)
  assert.equal(sessions.calls.abort, 1, 'the child is still physically aborted')
})

test('ONESHOT_aborted_terminal_surfaces_an_error', async () => {
  const sessions = fakeSessions()
  const live = await liveScope({ sessions })

  try {
    const pending = runOneShot(live.scope, context(), 'fast-coder', 'work')
    await waitForPrompt(sessions)
    sessions.fireTerminal(new TerminalOutcome(1, ['operator killed it']))

    await assert.rejects(pending, /Coder aborted: operator killed it/)
  } finally {
    live.cleanup()
  }
})

test('ONESHOT_failed_terminal_surfaces_an_error', async () => {
  const sessions = fakeSessions()
  const live = await liveScope({ sessions })

  try {
    const pending = runOneShot(live.scope, context(), 'fast-coder', 'work')
    await waitForPrompt(sessions)
    sessions.fireTerminal(new TerminalOutcome(2, ['provider exploded']))

    await assert.rejects(pending, /Coder failed: provider exploded/)
  } finally {
    live.cleanup()
  }
})

test('ONESHOT_parent_abort_completes_as_cancelled_and_aborts_the_child', async () => {
  const sessions = fakeSessions()
  const live = await liveScope({ sessions })

  try {
    let cancelParent
    // HostToolContext.AttachAbort is compiled as an uncurried pair with a curry
    // lookup (decodeContext uses uncurry2); a plain function field would defer
    // registration to the detach call. uncurry2 makes the register path immediate.
    const attachAbort = uncurry2((cancel) => {
      cancelParent = cancel
      return () => {}
    })
    const pending = runOneShot(live.scope, context('ses-call', attachAbort), 'fast-coder', 'work')
    for (let attempt = 0; attempt < 100 && cancelParent === undefined; attempt += 1) {
      await new Promise((resolve) => setTimeout(resolve, 5))
    }
    cancelParent()

    const settled = resultOf(await pending)
    assert.equal(settled.ok, true, settled.ok ? '' : settled.error)
    assert.match(settled.value.Output, /aborted: parent cancelled/)
    assert.ok(sessions.calls.abort >= 1, 'the child session is aborted')
  } finally {
    live.cleanup()
  }
})
