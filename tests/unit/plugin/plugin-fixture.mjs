// tests/unit/Plugin/plugin-fixture.mjs — shared layer-2 plugin fixture (VERIFY-008).
//
// Deliberately NOT named `*.test.mjs`. `tests/unit/runner.mjs:98` discovers tests with
// `walk('tests/unit', ['.test.mjs'])`, so a helper carrying that suffix would be run as
// a test file (zero tests inside it, but its top-level import cost paid twice).
// `scripts/architecture-gate.mjs` scans `['.mjs']`, so this file is still gated.

import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const { initSpikePlugin } = await import('../../../dist/Infrastructure/OpenCode/Plugin/SpikePlugin.js')

// ── the layer-3 executable fixture (EXEC-002, AGENT-007 layer two) ───────────
//
// `hooks.tool.*.execute` needs two things the layer-2 fixture deliberately does
// not have: a session transport under `client.session` (without one
// `Sessions.fs` SendPrompt fails closed with "No Host transport" — the old
// fabricated `"test output"` completion was removed), and an accepted Authority
// Root per calling session (PROMPT-002: without one the role is unresolved and
// every tool returns `{"error":"...no Authority Root..."}`).
//
// HOST-009 keeps both internal ports OFF the hooks object by design, so this
// layer reaches them the way production itself does — through the two
// process-local shared owners keyed by the same workspace runtime path
// (`RuntimePath.forWorkspace`): `SharedAgentJournal.acquire` and
// `SharedTerminalBus.acquire` return the exact journal instance and exact
// HostEventPort `initSpikePlugin` wired. No production export or visibility was
// widened for this (VERIFY-008); the same-import precedent is this file's own
// `initSpikePlugin` line.
const { forWorkspace } = await import('../../../dist/Journal/RuntimePath.js')
const { acquire: acquireJournal, release: releaseJournal } = await import('../../../dist/Journal/SharedAgentJournal.js')
const { acquire: acquireTerminalBus } = await import('../../../dist/Infrastructure/OpenCode/Host/SharedTerminalBus.js')
const { AgentJournalModule_runtimeId } = await import('../../../dist/Journal/AgentJournal.js')
const { forJournal, Runtime__AcceptHumanRoot } = await import('../../../dist/Application/Prompting/PromptDispatcher.js')
const { SessionIdModule_create, PhysicalUserMessageIdModule_create } = await import(
  '../../../dist/Kernel/Identity.js'
)
const { TerminalOutcome } = await import('../../../dist/Infrastructure/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Kernel/Outcome.js')
const {
  HandleController_recordCompletion: recordCompletion,
  HandleController_agentHandle: agentHandle,
} = await import('../../../dist/Session/HandleController.js')
const ChildRecovery = await import('../../../dist/Domain/ChildRecovery.js')
const terminalEvidenceCompleted =
  ChildRecovery.TerminalEvidenceModule_completed ?? ChildRecovery.TerminalEvidence_completed
const terminalEvidenceFailed =
  ChildRecovery.TerminalEvidenceModule_failed ?? ChildRecovery.TerminalEvidence_failed
const tryFromProvenTerminal =
  ChildRecovery.JoinableCompletionModule_tryFromProvenTerminal ??
  ChildRecovery.JoinableCompletion_tryFromProvenTerminal

/**
 * The smallest SDK client double: mint child ids, accept prompts. The id list is
 * handed back so tests can address the child a fork actually created.
 */
const promptedWaiters = new Map()

const stubClient = (createdIds, prompts, messages) => {
  let counter = 0
  return {
    session: {
      create: async () => {
        counter += 1
        const id = `host-child-${counter}`
        createdIds.push(id)
        return { data: { id } }
      },
      messages: async () => ({ data: messages }),
      promptAsync: async (args) => {
        prompts.push(args)
        // 生产在 terminal 订阅安装之后才发 prompt（OneShotAgentTool.fs:115 → send），
        // 故此调用即「可以安全 NotifyTerminal」的就绪信号。
        const sessionId = args?.sessionID ?? args?.sessionId ?? createdIds[createdIds.length - 1]
        const waiter = promptedWaiters.get(sessionId)
        if (waiter) {
          promptedWaiters.delete(sessionId)
          waiter()
        } else {
          promptedWaiters.set(sessionId, null) // 已就绪：后来的 awaitPrompted 立即返回
        }
        return {}
      },
      delete: async () => ({}),
      abort: async () => ({}),
    },
  }
}

/** 等待生产对某个子会话发出首个 prompt——即其 terminal 订阅已安装的就绪信号。 */
export const awaitPrompted = (sessionId) => {
  const state = promptedWaiters.get(sessionId)
  if (state === null) {
    promptedWaiters.delete(sessionId)
    return Promise.resolve()
  }
  return new Promise((resolve) => promptedWaiters.set(sessionId, resolve))
}

/**
 * A plugin instance whose tools can actually execute (EXEC-002, layer 3).
 *
 * `runtime` passed to the body is { journal, runtimeId, terminalPort,
 * recordFork } — the shared production instances, resolved AFTER
 * `initSpikePlugin` so `acquire` returns the already-registered entries rather
 * than booting second owners. `recordFork(parentSessionId, agentId,
 * childSessionId)` registers a forked handle so its terminal completion also
 * claims the EXEC-004 durable cell (see below).
 */
export const withExecutablePlugin = async (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-plugin-exec-'))
  try {
    execFileSync('git', ['init', '--quiet', directory])
    const createdIds = []
    const prompts = []
    const messages = []
    const hooks = await initSpikePlugin({
      client: stubClient(createdIds, prompts, messages),
      directory,
      events: { listen: () => () => {} },
    })
    let runtime
    try {
      const runtimePath = forWorkspace(directory)
      const journalResult = acquireJournal(runtimePath, process.pid, new Date())
      // Fable Result: tag 0 is Ok, and `fields` is the fields ARRAY — the
      // journal itself is fields[0].
      if (journalResult.tag !== 0) throw new Error(`journal acquire rejected: ${journalResult.fields?.[0]?.Reason}`)
      runtime = {
        journal: journalResult.fields[0],
        runtimeId: AgentJournalModule_runtimeId(journalResult.fields[0]).fields[0],
        terminalPort: acquireTerminalBus(runtimePath),
        // Filled in by forkHandle below; the recorder needs the parent session's
        // handle ids, which only a fork return value reveals.
        handles: new Map(),
        prompts,
        messages,
      }
      // EXEC-004's completion cell is claimed by `HostForkRuntime.Complete`
      // (Session/HostForkRuntime.fs:121-145), which lives behind the private
      // ToolRuntimeScope — NOT on the SharedTerminalBus fan-out a fixture can
      // reach. Delivering a bare NotifyTerminal without it leaves the handle
      // Active, so join's retire is rejected (NotCompleted) and poisons the
      // journal. The fixture claims the cell through the same single writer
      // (HandleController.recordCompletion) on the same shared journal, from a
      // terminal listener keyed by the parent session id — byte-identical to
      // the fact production appends, ahead of join's retire.
      const recorderByParent = new Map()
      runtime.terminalPort.SubscribeTerminalListener((sessionIdUnion, outcome) => {
        const sessionId = sessionIdUnion.fields[0]
        for (const [parentSessionId, byChild] of recorderByParent) {
          const agentId = byChild.get(sessionId)
          if (agentId !== undefined) {
            // P0-RECOVERY-JOIN-001: mint JoinableCompletion via Domain proof only.
            // EXEC-009: body is the durable join payload; fixture writes a minimal
            // completed/failed blob so projection-first join can consume after restart.
            // outcome.tag 0 = Completed; other tags are proven Failed (not raw Aborted).
            if (typeof terminalEvidenceCompleted !== 'function' || typeof tryFromProvenTerminal !== 'function') {
              throw new Error('ChildRecovery TerminalEvidence / JoinableCompletion not exported')
            }
            const body =
              outcome.tag === 0
                ? JSON.stringify({
                    status: 'completed',
                    run_id: `run-${agentId}`,
                    work_record: 'fixture-work-record',
                    child_session_id: sessionId,
                    authority_root: '',
                    provider_run: '',
                    directory: '',
                  })
                : JSON.stringify({
                    status: 'failed',
                    run_id: `run-${agentId}`,
                    code: 'ERROR',
                    message: 'fixture terminal failure',
                    child_session_id: sessionId,
                  })
            const handle = agentHandle(agentId)
            const child = SessionIdModule_create(sessionId)
            const evidence =
              outcome.tag === 0
                ? terminalEvidenceCompleted(agentId, handle, child, body)
                : terminalEvidenceFailed(agentId, handle, child, body)
            const proof = tryFromProvenTerminal(evidence)
            if (proof.tag !== 0) {
              throw new Error(`JoinableCompletion(${agentId}) rejected: ${proof.fields?.[0]}`)
            }
            const recorded = recordCompletion(runtime.journal, SessionIdModule_create(parentSessionId), proof.fields[0])
            if (recorded.tag !== 0) {
              throw new Error(`HandleCompleted(${agentId}) rejected: ${recorded.fields?.[0]}`)
            }
            byChild.delete(sessionId)
          }
        }
      })
      runtime.recordFork = (parentSessionId, agentId, childSessionId) => {
        if (!recorderByParent.has(parentSessionId)) recorderByParent.set(parentSessionId, new Map())
        recorderByParent.get(parentSessionId).set(childSessionId, agentId)
      }
      await body(hooks, directory, createdIds, runtime)
    } finally {
      // Release the fixture's extra journal reference before the plugin releases
      // its owning reference and closes the writer.
      if (runtime !== undefined) releaseJournal(runtime.journal)
      await hooks.dispose()
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

/** AGENT-007 layer one: a durable HumanRoot names the session's managed agent. */
export const acceptAuthorityRoot = (runtime, sessionId, agent) => {
  const result = Runtime__AcceptHumanRoot(
    forJournal(runtime.journal),
    SessionIdModule_create(sessionId),
    PhysicalUserMessageIdModule_create(`root-${sessionId}`),
    agent,
  )
  if (result.tag !== 0) {
    throw new Error(`AcceptHumanRoot(${sessionId}, ${agent}) rejected: ${result.fields?.[0]}`)
  }
}

/**
 * Deliver a real terminal completion for one child session, through the same
 * HostEventPort the plugin subscribed to.
 *
 * `sessionWideText` maps to AgentRunResult.TerminalText (EXEC-006 IsValid).
 * Join's work_record is the materialised LifecycleWorkRecord from the durable
 * XTrace (opening + frames + gap + terminal), not this string alone — fixture
 * completions without an Opening capture therefore yield an empty work_record.
 * `turnFormalText` is what a one-shot tool reports as output (COMPANION-005).
 *
 * AgentRunResult field order (Kernel/Outcome.fs): SessionId,
 * AuthorityRootUserMessageId, ProviderRun, Role, Directory, TerminalText,
 * TurnFormalText. `Role.Coder` is Fable union case tag 2 (Kernel/Roles.fs);
 * the runner pins it so a Role-order edit fails loudly instead of mislabeling.
 */
export const notifyCompleted = (runtime, childSessionId, sessionWideText, turnFormalText, roleCaseTag = 2) => {
  runtime.terminalPort.NotifyTerminal(
    SessionIdModule_create(childSessionId),
    // Fable union: case tag 0 is `Completed`, and `fields` is the fields ARRAY —
    // `new TerminalOutcome(0, result)` would wrap the record as fields[0..n]=undefined.
    new TerminalOutcome(
      0,
      [
        new AgentRunResult(
          SessionIdModule_create(childSessionId),
          null,
          null,
          { tag: roleCaseTag, fields: [] },
          undefined,
          sessionWideText,
          turnFormalText,
        ),
      ],
    ),
  )
}

/**
 * A plugin instance over a throwaway Git repo.
 *
 * The journal lives under the Git common directory (PERSIST-006), so `git init` is
 * what makes the runtime addressable at all. `events.listen` is the smallest port
 * that satisfies the signal source; no scenario, no HTTP, no mock provider.
 *
 * Measured cost per call: 275-331ms wall, almost entirely `git init` plus
 * `initSpikePlugin`. Callers under the 1000ms per-test bound (`runner.mjs:27`) must
 * count calls, not assertions.
 */
export const withPluginClient = async (client, body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-plugin-'))
  try {
    execFileSync('git', ['init', '--quiet', directory])
    const hooks = await initSpikePlugin({
      client,
      directory,
      events: { listen: () => () => {} },
    })
    try {
      await body(hooks, directory)
    } finally {
      await hooks.dispose()
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

export const withPlugin = async (body) => withPluginClient({}, body)
