// Shared OrchestratorHost integration harness (cutover Wave 2a).
//
// Used by requirements/change-integration/tests/host.test.mjs and
// requirements/review-assurance/tests/host-reverify.test.mjs (split from
// tests/unit/orchestrator/host.test.mjs; ≥2 target packages → support/).
//
// A REAL OrchestratorHost (real HostForkRuntime, real journal, real engine)
// over fake GitPort/ManagerPort-shaped seams. The engine is either pre-seeded
// (host.engineInstance = real engine built on fakes — member-level branches)
// or left to the host's own lazy initializeEngine (fake gitPort injected onto
// the host — init/sweep/caching branches). Fable compiles members to module
// functions, so engine behavior is varied through the real engine's ports,
// never through stubbed engine objects.

import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal,
  commitHash,
  hostEventPort,
  physicalUser,
  resultOf,
  sessionId,
  targetRef,
  toList,
  worktreePath,
} from './domain.mjs'

const hostModule = await import(new URL('../../../../dist/Change/Host/Host.js', import.meta.url).pathname)
const { OrchestratorHost } = hostModule
const rawJoinPublishedAvailable = Object.entries(hostModule).find(([k]) => k.startsWith('OrchestratorHost__JoinPublishedAvailable_'))?.[1]
const member = (name) =>
  Object.entries(hostModule).find(
    ([exportName, value]) => exportName.includes(`OrchestratorHost__${name}_`) && typeof value === 'function',
  )?.[1]
const rawForkManagerJob = member('ForkManagerJob')
const rawContinueManagerJob = member('ContinueManagerJob')
for (const [name, value] of [
  ['ForkManagerJob', rawForkManagerJob],
  ['ContinueManagerJob', rawContinueManagerJob],
]) {
  if (typeof value !== 'function') throw new Error(`${name} export must be discoverable without pinning Fable hash`)
}
const { OrchestratorHostDeps } = await import('../../../../dist/Change/Host/Types.js')
const RuntimeModule = await import('../../../../dist/Change/Runtime.js')
const createOrchestrator = Object.entries(RuntimeModule).find(([k]) => k.startsWith('Orchestrator_$ctor'))?.[1] ?? ((...args) => new RuntimeModule.Orchestrator(...args))

// Fable Results are {tag, fields}; resultOf restores the {ok, value, error} surface.
export const forkManagerJob = async (host, ...args) => resultOf(await rawForkManagerJob(host, ...args))
export const continueManagerJob = async (host, ...args) => resultOf(await rawContinueManagerJob(host, ...args))
export const joinPublishedAvailable = async (host, ...args) => resultOf(await rawJoinPublishedAvailable(host, ...args))

export const fakeGitPort = (behaviour = {}) => ({
  IsDirty: async () => !!behaviour.dirty,
  CreateWorktree: async (jobId) => ({
    tag: 0,
    fields: [{ fields: [`manager/${jobId.fields?.[0] ?? jobId}`], tag: 0, cases: () => ['WorktreeIdentity'] }],
  }),
  FreezeTargetBranch: async () =>
    behaviour.freezeError ? { tag: 1, fields: [behaviour.freezeError] } : { tag: 0, fields: [targetRef('main')] },
  Rebase: async () => ({ tag: 0, fields: [] }),
  ConflictedFiles: async () => ({ tag: 0, fields: [toList([])] }),
  FfMerge: async () => ({ tag: 0, fields: [commitHash('cafe01')] }),
  RemoveWorktree: async () => ({ tag: 0, fields: [] }),
  HasRebaseHead: async () => false,
  ListWorktrees: async () =>
    behaviour.listError ? { tag: 1, fields: [behaviour.listError] } : { tag: 0, fields: [toList([])] },
  ListManagerBranches: async () => ({ tag: 0, fields: [toList([])] }),
  DeleteBranch: async () => ({ tag: 0, fields: [] }),
  ReadHead: async () => ({ tag: 0, fields: [commitHash('beef02')] }),
  GetTargetHead: async () => ({ tag: 0, fields: [commitHash('beef02')] }),
})

export const fakeSessions = (behaviour = {}) => {
  const calls = []
  let childSeq = 0
  const terminalListeners = new Map()
  const stickyTerminals = new Map()

  const sessionKey = (value) => value?.fields?.[0] ?? value
  const notifyTerminal = (session, outcome) => {
    const key = sessionKey(session)
    stickyTerminals.set(key, outcome)
    for (const callback of terminalListeners.get(key) ?? []) callback(session, outcome)
  }

  return {
    calls,
    CreateChildSession: async (parentId, options) => {
      childSeq += 1
      calls.push(['CreateChildSession', options])
      if (behaviour.createError) return { tag: 1, fields: [behaviour.createError] }
      return { tag: 0, fields: [sessionId(`child-${childSeq}`)] }
    },
    AbortSession: async (id) => {
      calls.push(['AbortSession', id.fields?.[0] ?? id])
      return { tag: 0, fields: [] }
    },
    SendPrompt: async (...args) => {
      calls.push(['SendPrompt', ...args])
      behaviour.onSendPrompt?.(...args)
      if (behaviour.sendPromptError) return { tag: 4, fields: [behaviour.sendPromptError] }
      if (behaviour.terminalAfterSend) {
        queueMicrotask(() => notifyTerminal(args[0], hostEventPort.failed(behaviour.terminalAfterSend)))
      }
      return { tag: 1, fields: [physicalUser('msg_fake_prompt')] }
    },
    SendPromptAsync: async (...args) => {
      calls.push(['SendPromptAsync', ...args])
      return { tag: 0, fields: [] }
    },
    SubscribeTerminal: (childId, callback) => {
      calls.push(['SubscribeTerminal', childId])
      const key = sessionKey(childId)
      if (!terminalListeners.has(key)) terminalListeners.set(key, new Set())
      terminalListeners.get(key).add(callback)
      if (stickyTerminals.has(key)) {
        queueMicrotask(() => callback(childId, stickyTerminals.get(key)))
      }
      return {
        Dispose: () => {
          terminalListeners.get(key)?.delete(callback)
        },
      }
    },
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
}

export const fakeManagerPort = (calls) => ({
  StartManager: async (start) => {
    calls.push(['StartManager', start])
    return { tag: 0, fields: [sessionId('manager-ses-1')] }
  },
  AwaitManager: async () => ({ tag: 0, fields: [] }),
  Reverify: async () => ({ tag: 0, fields: [] }),
  ResumeManager: async (jobId, worktree, prompt) => {
    calls.push(['ResumeManager', prompt])
    return { tag: 0, fields: [] }
  },
  TerminateChildren: async () => {},
})

/** A real git repo with one empty commit; gitCommonDir/init stay hermetic. */
export const gitDir = (label) => {
  const dir = mkdtempSync(join(tmpdir(), `wxs-host-${label}-`))
  execFileSync('git', ['init', '-b', 'main', dir], { stdio: 'ignore' })
  execFileSync(
    'git',
    ['-C', dir, '-c', 'user.email=t@t', '-c', 'user.name=t', 'commit', '--allow-empty', '-m', 'init'],
    { stdio: 'ignore' },
  )
  return dir
}

/**
 * Real OrchestratorHost over a real journal + real repo. When `seedEngine` is
 * true (default) the host carries a REAL engine built on the fake git port, so
 * member-level branches run without initializeEngine; when false the host
 * initializes its own engine lazily through host.gitPort (init/sweep/caching).
 */
export const liveOrchestrator = async (options = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hostcov-'))
  const repoDir = join(dir, 'repo')
  mkdirSync(repoDir)
  execFileSync('git', ['init', '-b', 'main', repoDir], { stdio: 'ignore' })
  execFileSync(
    'git',
    ['-C', repoDir, '-c', 'user.email=t@t', '-c', 'user.name=t', 'commit', '--allow-empty', '-m', 'init'],
    { stdio: 'ignore' },
  )
  const opened = options.journal === false ? null : await agentJournal.create({ directory: dir })
  if (opened) {
    const ok = opened.ok === true
    if (!ok) throw new Error('journal must open')
  }

  const sessions = fakeSessions(options.sessionBehaviour)
  const deps = new OrchestratorHostDeps(
    sessions,
    opened?.journal,
    undefined,
    () => {},
    () => {},
    options.registerReviewerTree ?? (() => {}),
    () => {},
    options.repoPath ?? repoDir,
    options.targetBranch ?? '',
    async () => undefined,
    async () => undefined,
  )
  const host = new OrchestratorHost(deps, sessionId('ses_orphost'))
  host.gitPort = options.gitPort ?? fakeGitPort()

  if (options.seedEngine !== false) {
    const managerCalls = []
    const engine = createOrchestrator(
      host.gitPort,
      fakeManagerPort(managerCalls),
      repoDir,
      targetRef('main'),
      {
        AppendFact: async (streamId, factValue) => {
          const appended = await agentJournal.appendAgent(streamId, undefined, factValue, opened.journal)
          return appended.ok ? { tag: 0, fields: [appended.value] } : { tag: 1, fields: ['append failed'] }
        },
        Snapshot: () => agentJournal.snapshot(opened.journal),
      },
      repoDir,
    )
    host.engineInstance = engine
    host.__managerCalls = managerCalls
  }

  return {
    host,
    sessions,
    journal: opened?.journal,
    cleanup: () => {
      try {
        opened?.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

export const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
