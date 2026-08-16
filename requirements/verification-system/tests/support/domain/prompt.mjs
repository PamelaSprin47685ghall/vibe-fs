// tests/unit/support/domain/prompt.mjs — prompt authority family.
// Request kind, attempt planner, authority/authorityRun, prompt dispatcher,
// session snapshot, prompt origin, prompt resources.

import {
  PrefixCandidateModule,
  AttemptPlannerModule,
  RecoverySlotModule,
  Authority,
  AuthorityRun,
  PromptDispatcherModule,
  PromptDispatcherSendModule,
  AgentJournalModule,
  SessionSnapshotPortModule,
  PromptResourcesModule,
  ProviderResourcesModule,
  ProviderLanguageModule,
  Outcome,
  unionCase,
  bind,
  member,
  caseOf,
  resultOf,
  unwrapOption,
  isNone,
  listItems,
  mapCount,
  toList,
} from './interop.mjs'
import {
  sessionId,
  logicalRunId,
  authorityRoot,
  physicalUser,
  providerRun,
  idValue,
} from './identity.mjs'
import { roles } from './context.mjs'
import { cursor } from './journal.mjs'
import { rootKind } from './enforcer.mjs'

/**
 * PROMPT-008: which physical request this is, and the two questions it answers.
 *
 * The kinds are built by case NAME. All four are payload-free, so an ordinal-based
 * construction would compile, run, and answer `clearsFailureCountOnSuccess` for the
 * wrong kind — the exact class of silent failure this facade exists to prevent.
 */
export const requestKind = (() => {
  const build = unionCase(PrefixCandidateModule.ProviderRequestKind, 'ProviderRequestKind')
  const m = bind(PrefixCandidateModule, 'ProviderRequestKind', [
    'label',
    'clearsFailureCountOnSuccess',
    'mayCarryProbe',
  ])

  const of = (name) => build(name, [])

  return {
    workMain: of('WorkMain'),
    bloggerMain: of('BloggerMain'),
    bloggerSquash: of('BloggerSquash'),
    interactionRepair: of('InteractionRepair'),
    all: ['WorkMain', 'BloggerMain', 'BloggerSquash', 'InteractionRepair'].map(of),

    of,
    nameOf: (kind) => caseOf(kind),
    label: (kind) => m.label(kind),
    clearsFailureCountOnSuccess: (kind) => m.clearsFailureCountOnSuccess(kind),
    mayCarryProbe: (kind) => m.mayCarryProbe(kind),
  }
})()

/**
 * PROMPT-008: the one call site of `buildAttemptExecutionProfile`.
 *
 * `mayRecover` is passed in rather than derived from a cursor, mirroring production:
 * arming is a control-flow fact of the caller's recovery sequence (FALLBACK-012), and a
 * planner that decided it from an offset would be the parked-cursor bug.
 */
export const attemptPlanner = (() => {
  const m = bind(AttemptPlannerModule, 'AttemptPlanner', ['plan', 'probeOf', 'promotableProbe'])
  const buildOutcome = unionCase(RecoverySlotModule.AttemptOutcome, 'AttemptOutcome')

  /** A complete AuthorityExecutionProfile. Every field is required — PROMPT-002 fixes them all. */
  const authority = ({
    session = 'ses_x',
    run = 'run-1',
    root = 'msg_root',
    kind = rootKind.human,
    selected = 'fast-coder',
    peer = 'deep-coder',
    role = 'Coder',
    tier = 'Fast',
  } = {}) => ({
    SessionId: sessionId(session),
    LogicalRunId: logicalRunId(run),
    AuthorityRootUserMessageId: authorityRoot(root),
    AuthorityKind: kind,
    SelectedAgent: selected,
    PeerAgent: peer,
    CanonicalRole: roles.of(role),
    SelectedTier: roles.tier(tier),
  })

  return {
    authority,

    plan: ({
      authorityProfile = authority(),
      cursor: cursorValue = cursor.initial,
      physical = 'msg_user',
      run = 'msg_assistant',
      origin = promptOrigin.authorityRoot(rootKind.human),
      kind,
      mayRecover = false,
      selectProbe = () => {
        throw new Error('selectProbe must not be called when the slot may not recover')
      },
    }) => {
      const plan = m.plan(
        authorityProfile,
        cursorValue,
        physicalUser(physical),
        providerRun(run),
        origin,
        kind,
        mayRecover,
        selectProbe,
      )

      const noProbeReason = unwrapOption(plan.NoProbeReason)
      const probe = unwrapOption(m.probeOf(plan))

      return {
        value: plan,
        requestKind: caseOf(plan.Profile.RequestKind),
        choice: caseOf(plan.Profile.ProjectionChoice),
        effectiveAgent: plan.Profile.EffectiveAgent,
        canonicalRole: caseOf(plan.Profile.Authority.CanonicalRole),
        toolCapabilities: [...plan.Profile.ToolCapabilitySet].map(caseOf).sort(),
        systemPromptId: idValue.systemPrompt(plan.Profile.SystemPromptId),
        noProbeReason: isNone(noProbeReason) ? undefined : caseOf(noProbeReason),
        probeId: isNone(probe) ? undefined : probe.ProbeId,
      }
    },

    /** CTX-012: `undefined` unless this attempt carried a probe AND produced a usable terminal. */
    promotableProbeId: (plan, outcome) => {
      const probe = unwrapOption(m.promotableProbe(plan.value, buildOutcome(outcome, [])))
      return isNone(probe) ? undefined : probe.ProbeId
    },
  }
})()

// ── prompt authority (docs/what/prompt.md) ───────────────────────────────────────────────

export const authority = {
  empty: Authority.empty,
  originLabel: (origin) => Authority.originLabel(origin),
  tryParseContinuationKind: (name) => unwrapOption(Authority.tryParseContinuationKind(name)),
  roleLabel: (role) => Authority.roleLabel(role),
  tryParseRole: (name) => unwrapOption(Authority.tryParseRole(name)),
  tierLabel: (tier) => Authority.tierLabel(tier),
  tryParseTier: (name) => unwrapOption(Authority.tryParseTier(name)),

  /** AGENT-002/003. Typed rejection form; `caseOf` the error to name it. */
  parseAgentName: (name) => resultOf(Authority.parseAgentNameTyped(name)),

  stableLogicalRunId: (sha256, runtime, session, root) => Authority.stableLogicalRunId(sha256, runtime, session, root),
  agentPair: (profile) => Authority.agentPair(profile),
  effectiveAgentFor: (profile, value) => Authority.effectiveAgentFor(profile, value),
  /**
   * PROMPT-011 claim scope. NOT hashed — it is a `\u001f`-joined string, so a test
   * can read the four components it names. Takes no `sha256`; only `derivePromptKey`
   * hashes.
   */
  claimScopeDigest: (session, runId, origin, payloadDigest) =>
    Authority.claimScopeDigest(session, runId, origin, payloadDigest),
  nextClaimSequence: (scope, projection) => Authority.nextClaimSequence(scope, projection),
  derivePromptKey: (...args) => Authority.derivePromptKey(...args),
  repairPayloadDigest: (run, kind) => Authority.repairPayloadDigest(run, kind),
  repairAlreadyClaimed: (...args) => Authority.repairAlreadyClaimed(...args),
  systemPromptIdFor: (role) => Authority.systemPromptIdFor(role),
  buildAttemptExecutionProfile: (...args) => Authority.buildAttemptExecutionProfile(...args),

  allowsTool: (permission, profile) => Authority.allowsTool(permission, profile),

  /** PROMPT-011 physical evidence window; restart-count abandonment is retired. */
  recoveryTailWindow: Authority.RecoveryTailWindow,
}

export const authorityRun = {
  createAuthorityRoot: (sha256, runtime, session, kind, physical, agent) =>
    resultOf(AuthorityRun.createAuthorityRoot(sha256, runtime, session, kind, physical, agent)),
  claimAgentOwnerRoot: (key, session, payloadDigest, agent) =>
    resultOf(AuthorityRun.claimAgentOwnerRoot(key, session, payloadDigest, agent)),
  claimContinuation: (key, session, kind, profile, effectiveAgent, payloadDigest) =>
    AuthorityRun.claimContinuation(key, session, kind, profile, effectiveAgent, payloadDigest),
  registerAuthority: (profile, projection) => AuthorityRun.registerAuthority(profile, projection),
  registerClaim: (claim, projection) => AuthorityRun.registerClaim(claim, projection),
  submitClaim: (key, receipt, projection) => AuthorityRun.submitClaim(key, receipt, projection),
  acceptClaim: (key, physical, projection) => AuthorityRun.acceptClaim(key, physical, projection),
  abandonClaim: (key, projection) => AuthorityRun.abandonClaim(key, projection),
  resolveKnownOrigin: (physical, key, hostCompact, projection) =>
    caseOf(AuthorityRun.resolveKnownOrigin(physical, key, hostCompact, projection)),
}

/**
 * PROMPT-006/007: the send-time `SessionPromptOptions` construction and AwaitMode,
 * as the Host sees them.
 *
 * `SendAgentOwnerRoot` and `SendContinuation` build `{ Agent = Some …;
 * Model = None; … }` inside the send body, so the only way to observe them is
 * to run a send against a port and read the `options` argument `SendPrompt`
 * receives. The Fable extension-member names carry the whole
 * `Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_` prefix, so
 * they are absorbed here rather than at the call site (VERIFY-008).
 */
export const promptDispatcher = (() => {
  const memberOfSend = (name) => Object.entries(PromptDispatcherSendModule).find(
    ([k]) => k.includes(`_Runtime_${name}`) || k.endsWith(`_${name}`) || k === name,
  )?.[1]
  const sendAgentOwnerRoot = memberOfSend('SendAgentOwnerRoot')
  const sendContinuation = memberOfSend('SendContinuation')
  // Instance members on Runtime: Fable may emit
  //   Runtime__ProjectionFor
  //   Runtime__ProjectionFor_<hash>   (overload hash)
  //   Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_ProjectionFor
  // Pick the first matching function export; fail closed if none.
  const projectionForMember = (() => {
    const keys = Object.keys(PromptDispatcherModule)
    const candidates = [
      'Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_ProjectionFor',
      'Runtime__ProjectionFor',
      ...keys.filter((k) => /^Runtime__ProjectionFor(_|$)/.test(k) || /Runtime__Runtime_ProjectionFor/.test(k)),
    ]
    for (const key of candidates) {
      const value = PromptDispatcherModule[key]
      if (typeof value === 'function') return value
    }
    const near = keys.filter((k) => /Projection/i.test(k)).join(', ')
    throw new Error(`PromptDispatcher.Runtime.ProjectionFor missing. Near: ${near || '(none)'}`)
  })()
  const buildSendOutcome = unionCase(Outcome.Outcome_SendOutcome, 'Outcome.SendOutcome')
  // Nested DU under PromptDispatcher: Fable may emit AwaitMode or PromptDispatcher_AwaitMode.
  const AwaitModeClass =
    PromptDispatcherModule.AwaitMode
    ?? PromptDispatcherModule.PromptDispatcher_AwaitMode
  if (typeof AwaitModeClass !== 'function') {
    const near = Object.keys(PromptDispatcherModule).filter((k) => /Await/i.test(k)).join(', ')
    throw new Error(`PromptDispatcher.AwaitMode missing. Near: ${near || '(none)'}`)
  }
  const awaitModeOf = unionCase(AwaitModeClass, 'PromptDispatcher.AwaitMode')
  const journalSnapshot = member(AgentJournalModule, 'AgentJournal', 'snapshot')

  const decode = (result) => {
    const value = resultOf(result)
    return value.ok ? { ok: true, key: idValue.promptKey(value.value) } : value
  }

  /** PROMPT-007: default Detached (fire-and-forget) unless the test asks Await. */
  const resolveAwaitMode = (mode) => {
    if (mode === undefined || mode === null) return awaitModeOf('Detached')
    if (typeof mode === 'string') return awaitModeOf(mode)
    return mode
  }

  return {
    forJournal: (journal) => PromptDispatcherModule.forJournal(journal),

    /** PROMPT-007 AwaitMode constructors. */
    awaitMode: {
      await: () => awaitModeOf('Await'),
      detached: () => awaitModeOf('Detached'),
      of: (name) => awaitModeOf(name),
    },

    /** PROMPT-006: an `AdmittedWithReceipt` outcome for a stub port to return. */
    admittedWithReceipt: (receipt) => buildSendOutcome('AdmittedWithReceipt', [receipt]),
    /**
     * PROMPT-006: admission that also accepts the physical user message.
     * Seeds ActiveLogicalRun (needed by SyncDelegateRuntime.Return).
     */
    admittedWithPhysicalMessage: (physical) =>
      buildSendOutcome('AdmittedWithPhysicalMessage', [
        typeof physical === 'string' ? physicalUser(physical) : physical,
      ]),
    /** Explicit transport failure for InteractionRepair hard-fail paths. */
    retryable: (reason) => buildSendOutcome('Retryable', [reason]),
    fatal: (reason) => buildSendOutcome('Fatal', [reason]),

    /**
     * PROMPT-005/007: authority projection after a send.
     * Detached success still claims/submits; no PhysicalAccepted required for caller Ok.
     */
    projectionFor: (runtime, session) => projectionForMember(runtime, sessionId(session)),

    /** Integrated journal projection (PendingClaims live under session.PromptAuthority). */
    journalSnapshot: (journal) => journalSnapshot(journal),

    /** Pending claim count for one session after Detached/Await send. */
    pendingClaimCount: (runtime, session) => {
      const projection = projectionForMember(runtime, sessionId(session))
      return mapCount(projection.PendingClaims)
    },

    sendAgentOwnerRoot: async (runtime, port, { session, text, agent, directory, awaitMode, onAccepted }) =>
      decode(
        await sendAgentOwnerRoot(
          runtime,
          port,
          sessionId(session),
          text,
          agent,
          directory,
          resolveAwaitMode(awaitMode),
          onAccepted,
        ),
      ),

    sendContinuation: async (runtime, port, { session, text, continuation, profile, effectiveAgent, directory, awaitMode, onAccepted }) =>
      decode(
        await sendContinuation(
          runtime,
          port,
          sessionId(session),
          text,
          continuation,
          profile,
          effectiveAgent,
          directory,
          resolveAwaitMode(awaitMode),
          onAccepted,
        ),
      ),
  }
})()

/**
 * HOST-010: transform → ProviderRunIdentity binding (ReviewSeal.bindableRun).
 * Messages are Host-raw objects projected via SessionSnapshotPort.projectMessages.
 */
export const sessionSnapshot = (() => {
  const m = bind(SessionSnapshotPortModule, 'SessionSnapshotPort', ['projectMessages', 'locateToolCall'])

  return {
    projectMessages: (rawMessages) => listItems(m.projectMessages(rawMessages)),
    locateToolCall: (callId, messages) => resultOf(m.locateToolCall(callId, toList(messages))),
  }
})()

export const promptOrigin = (() => {
  const build = unionCase(Authority.PromptOrigin, 'PromptOrigin')
  return {
    authorityRoot: (kind) => build('AuthorityRoot', [kind]),
    continuation: (kind) => build('Continuation', [kind]),
    hostInternal: Authority.PromptOrigin.HostInternal,
    unknown: Authority.PromptOrigin.UnknownOrigin,
  }
})()

/** Explicit prompt catalog load (10 role system prompts). */
export const promptResources = (() => {
  const api = bind(PromptResourcesModule, 'PromptResources', [
    'load',
    'loadForLanguage',
    'loadForSession',
    'loadBookkeeperSystem',
    'loadBookkeeperSystemFor',
  ])
  return {
    load: () => api.load(),
    loadForLanguage: (lang) => api.loadForLanguage(lang),
    allForLanguage: (lang) => Object.values(api.loadForLanguage(lang)),
    loadForSession: (sessionId) => api.loadForSession(sessionId),
    loadBookkeeperSystem: () => api.loadBookkeeperSystem(),
    loadBookkeeperSystemFor: (lang) => api.loadBookkeeperSystemFor(lang),
  }
})()
/** PROMPT-017 / HOST-026: ProviderLanguage parse + session bind-once inherit. */
export const providerLanguage = (() => {
  const build = unionCase(ProviderLanguageModule.ProviderLanguage, 'ProviderLanguage')
  const lang = bind(ProviderLanguageModule, 'ProviderLanguage', [
    'resourceDirectory',
    'label',
    'tryParse',
    'parse',
    'inheritFrom',
  ])
  const session = bind(ProviderLanguageModule, 'SessionProviderLanguage', [
    'clearAllForTests',
    'tryGet',
    'drop',
    'bindOnce',
    'inheritFromOwner',
  ])

  const of = (name) => build(name, [])

  return {
    english: of('English'),
    simplifiedChinese: of('SimplifiedChinese'),
    of,
    nameOf: (value) => caseOf(value),
    resourceDirectory: (value) => lang.resourceDirectory(value),
    label: (value) => lang.label(value),
    tryParse: (raw) => unwrapOption(lang.tryParse(raw)),
    parse: (raw) => lang.parse(raw),
    inheritFrom: (owner) => lang.inheritFrom(owner),
    clearAllForTests: () => session.clearAllForTests(),
    tryGet: (id) => unwrapOption(session.tryGet(id)),
    drop: (id) => session.drop(id),
    bindOnce: (id, language) => resultOf(session.bindOnce(id, language)),
    inheritFromOwner: (ownerLanguage, childId) => resultOf(session.inheritFromOwner(ownerLanguage, childId)),
  }
})()

/** Phase 2 bilingual provider resource tree hooks. */
export const providerResources = (() => {
  const api = bind(ProviderResourcesModule, 'ProviderResources', [
    'relativePath',
    'exists',
    'readText',
    'tryReadText',
    'requireLanguagePair',
    'languageRootsPresent',
  ])
  return {
    relativePath: (lang, semanticPath) => api.relativePath(lang, semanticPath),
    exists: (lang, semanticPath) => api.exists(lang, semanticPath),
    readText: (lang, semanticPath) => api.readText(lang, semanticPath),
    tryReadText: (lang, semanticPath) => unwrapOption(api.tryReadText(lang, semanticPath)),
    requireLanguagePair: (semanticPath) => api.requireLanguagePair(semanticPath),
    languageRootsPresent: () => api.languageRootsPresent(),
  }
})()
