// tests/unit/support/domain/journal.mjs — facts + journal family.
// AgentFact construction/decoding, Fact wrapping, envelope/stream/fold,
// cursor + fallback projection, and the Journal/*Projection adapters.

import {
  FactModule,
  EnvelopeModule,
  FoldModule,
  FactCodec,
  Cursor,
  FallbackProj,
  BlogProj,
  EnforcementProj,
  PrefixProj,
  unionCase,
  caseNames,
  bind,
  caseOf,
  payloadOf,
  resultOf,
  toList,
  listItems,
  mapToObject,
  mapTryFind,
  unwrapOption,
  requireList,
  utcOffset,
  offsetOf,
  offsetValue,
} from './interop.mjs'
import {
  sessionId,
  localSeq,
  eventId,
  providerRun,
  runtimeId,
  idValue,
  frameEpochId,
  prefixEpochId,
  blobDigest,
  blobRef,
  toolCallId,
} from './identity.mjs'

const buildAgentFactDispatch = unionCase(FactModule.AgentFact, 'AgentFact')

// DSL-003: AgentFact is a dispatch union over per-bounded-context *FactCases
// families. The facade keeps the flat construction surface — a test
// names the business case, the family lookup wraps it — so no test learns the
// nesting, and the wire shape (case name + payload) is unchanged.
const AGENT_FACT_FAMILIES = [
  ['Prompt', FactModule.PromptFactCases],
  ['Fallback', FactModule.FallbackFactCases],
  ['Review', FactModule.ReviewFactCases],
  ['Execution', FactModule.ExecutionFactCases],
  ['Orchestrator', FactModule.OrchestratorFactCases],
  ['Companion', FactModule.CompanionFactCases],
  ['Context', FactModule.ContextFactCases],
  ['Host', FactModule.HostFactCases],
  ['Delegation', FactModule.DelegationFactCases],
]

const buildAgentFact = (() => {
  const familyBuilders = AGENT_FACT_FAMILIES.map(([dispatchCase, unionClass]) => {
    const build = unionCase(unionClass, `${dispatchCase}FactCases`)
    return [dispatchCase, caseNames(unionClass), build]
  })
  return (caseName, fields) => {
    for (const [dispatchCase, names, build] of familyBuilders) {
      if (names.includes(caseName)) return buildAgentFactDispatch(dispatchCase, [build(caseName, fields)])
    }
    throw new Error(
      `no AgentFact family has case '${caseName}'. Available: ${familyBuilders.flatMap(([, names]) => names).join(', ')}`,
    )
  }
})()
const buildFact = unionCase(FactModule.Fact, 'Fact')
const buildRuntimeFact = unionCase(FactModule.RuntimeFact, 'RuntimeFact')
const buildStream = unionCase(EnvelopeModule.StreamId, 'StreamId')
const buildVerdict = unionCase(FactModule.ReviewGuardVerdict, 'ReviewGuardVerdict')
const buildAbandonReason = unionCase(FactModule.PromptAbandonReason, 'PromptAbandonReason')
const buildCompletionKind = unionCase(FactModule.HandleCompletionKind, 'HandleCompletionKind')
const buildHandleAbandonReason = unionCase(FactModule.HandleAbandonReason, 'HandleAbandonReason')
const buildHandleOwnership = unionCase(FactModule.HandleOwnership, 'HandleOwnership')

/** Flat case-name catalogue across all AgentFact families (DSL-003). */
export const agentFactCaseNames = () => AGENT_FACT_FAMILIES.flatMap(([, unionClass]) => caseNames(unionClass))

// ── facts ────────────────────────────────────────────────────────────────────

export const verdict = {
  perfect: FactModule.ReviewGuardVerdict.Perfect,
  revise: FactModule.ReviewGuardVerdict.Revise,
  of: (name) => buildVerdict(name),
}

export const abandonReason = {
  sendFailed: (error) => buildAbandonReason('SendFailed', [error]),
  unresolvedAfterRecovery: () => buildAbandonReason('UnresolvedAfterRecovery'),
}

export const completionKind = {
  of: (name) => buildCompletionKind(name),
}

/** EXEC-009 HandleAbandoned reason (fieldless DU cases). */
export const handleAbandonReason = {
  of: (name) => buildHandleAbandonReason(name),
  parentCancelled: () => buildHandleAbandonReason('ParentCancelled'),
  deadlineExceeded: () => buildHandleAbandonReason('DeadlineExceeded'),
  hostSessionGone: () => buildHandleAbandonReason('HostSessionGone'),
}

/** HandleOwnership: which side of the boundary owns the physical resource. */
export const handleOwnership = {
  of: (name) => buildHandleOwnership(name),
  durableParentHandle: () => buildHandleOwnership('DurableParentHandle'),
  hostOwnedHidden: () => buildHandleOwnership('HostOwnedHidden'),
}

/** Build an AgentFact by case name with an anonymous-record payload. */
export const agentFact = (caseName, payload) => buildAgentFact(caseName, [payload])

/**
 * The business case name of an AgentFact, with the DSL-003 family dispatch
 * peeled. `agentFact('FallbackCursorAdvanced', ...)` round-trips to
 * 'FallbackCursorAdvanced' — tests never learn the family nesting.
 */
export const agentFactCaseOf = (value) => {
  const dispatch = caseOf(value)
  if (dispatch === undefined) return undefined
  const family = AGENT_FACT_FAMILIES.find(([name]) => name === dispatch)
  if (!family) throw new TypeError(`agentFactCaseOf: '${dispatch}' is not an AgentFact family dispatch`)
  return caseOf(payloadOf(value))
}

/** Wrap an AgentFact as the top-level Fact union. */
export const asFact = (inner) => buildFact('Agent', [inner])

/** Convenience: build and wrap in one step. */
export const fact = (caseName, payload) => asFact(agentFact(caseName, payload))

const buildManagerLifecycleFact = unionCase(FactModule.ManagerLifecycleFact, 'ManagerLifecycleFact')

/** Build a ManagerLifecycleFact by case name (GLORY-010). */
export const managerLifecycle = (caseName, payload) => buildManagerLifecycleFact(caseName, [payload])
export const managerLifecycleFact = (caseName, payload) =>
  buildFact('ManagerLifecycle', [managerLifecycle(caseName, payload)])

/** Canonical opaque-wire top-level fact for typed MagicTodoFact codec bytes. */
export const magicTodoFactEnvelope = (payload) => buildFact('MagicTodo', [payload])

/**
 * `RuntimeStarted`, wrapped as a top-level Fact.
 *
 * Its own helper because PROMPT-011 counts recovery attempts by folding this
 * fact, so a test needs to emit plugin starts without reaching for ordinals.
 */
export const runtimeStartedFact = ({ runtime = 'rt-test', pid = 1, startedAt = '2026-01-01T00:00:00Z' } = {}) =>
  buildFact('Runtime', [
    buildRuntimeFact('RuntimeStarted', [
      { RuntimeId: runtimeId(runtime), ProcessId: pid, StartedAt: utcOffset(startedAt) },
    ]),
  ])

export const stream = {
  workspace: () => buildStream('Workspace'),
  session: (id) => buildStream('Session', [id]),
  child: (id) => buildStream('Child', [id]),
  process: (id) => buildStream('Process', [id]),
}

// ── journal ──────────────────────────────────────────────────────────────────

/**
 * Build an envelope. `seq` counts from 1; `observedAt` is an ISO string so a
 * test never constructs a clock value by hand.
 *
 * `run` is the ProviderRunIdentity this fact was observed during, or omitted for
 * facts belonging to no run (HOST-010). It replaced `TurnId`, which was a third
 * name for the same thing.
 */
export const envelope = ({
  runtime = 'rt-test',
  seq = 1,
  observedAt = '2026-01-01T00:00:00Z',
  stream: streamId,
  run,
  fact: envelopeFact,
}) => ({
  RuntimeId: runtimeId(runtime),
  LocalSeq: localSeq(seq),
  ObservedAt: utcOffset(observedAt),
  EventId: eventId(`e${seq}`),
  Stream: streamId,
  ProviderRun: run === undefined ? undefined : providerRun(run),
  Fact: envelopeFact,
})

// `Envelope` and `Fold` each collide with a type in their own file, but only
// `Envelope` gained the `Module` infix — `Fold`'s members emit as `Fold_*`.
// Resolving both through `bind` means the facade never hard-codes which of the
// two spellings a given file happened to produce.
const Envelopes = bind(EnvelopeModule, 'Envelope', ['serialize', 'deserialize', 'compareSortKey'])
const Folds = bind(FoldModule, 'Fold', ['empty', 'foldEnvelope', 'foldAgentFact'])

const foldSequence = (projection, envelopes) => {
  let current = projection
  for (const env of envelopes) {
    const next = resultOf(Folds.foldEnvelope(current, env))
    if (!next.ok) return next
    current = next.value
  }
  return { ok: true, value: current }
}

export const journal = {
  serialize: (env) => Envelopes.serialize(env),
  deserialize: (line) => resultOf(Envelopes.deserialize(line)),
  serializeFact: (value) => FactCodec.serializeFact(value),
  deserializeFact: (json) => resultOf(FactCodec.deserializeFact(json)),
  containsLegacyFallbackFields: (json) => FactCodec.containsLegacyFallbackFields(json),
  pre050MigrationMessage: FactCodec.pre050MigrationMessage,
  /** ENFORCER-072: ScoreVectorRef-era BlogObservationCommitted (no max-score migration). */
  containsLegacyScoreVectorEntry: (json) => FactCodec.containsLegacyScoreVectorEntry(json),
  tipV2CleanBreakMessage: FactCodec.tipV2CleanBreakMessage,
  compareSortKey: (a, b) => Envelopes.compareSortKey(a, b),
}

export const fold = {
  empty: Folds.empty,

  /** Test-only oracle: repeatedly call the production single-envelope rule; no production full-history API exists. */
  apply: (projection, envelopes) => foldSequence(projection, listItems(requireList(toList(envelopes), 'fold.apply'))),

  one: (projection, env) => resultOf(Folds.foldEnvelope(projection, env)),

  /** Round-trip through NDJSON, then fold. Proves the persisted shape folds. */
  replay: (envelopes) => {
    const decoded = [...envelopes].map((env) => {
      const result = journal.deserialize(journal.serialize(env))
      if (!result.ok) throw new Error(`envelope did not survive a round trip: ${result.error}`)
      return result.value
    })
    return foldSequence(Folds.empty, decoded)
  },

  /** Sessions map of a folded projection, keyed by session id string. */
  sessions: (projection) => mapToObject(projection.AgentProjections.Sessions, idValue.session),

  /** One session's bounded projections, or undefined. */
  session: (projection, id) => mapTryFind(sessionId(id), projection.AgentProjections.Sessions),

  orchestrator: (projection) => projection.AgentProjections.Orchestrator,
}

/** PERSIST-004 merge order across runtime streams (Envelope.compareSortKey). */
export const kWayMerge = (streams) => {
  const merged = streams.flatMap((s) => (Array.isArray(s) ? s : listItems(s)))
  return merged
    .slice()
    .sort((a, b) => EnvelopeModule.EnvelopeModule_compareSortKey(a, b) | 0)
}

/** A cursor as the tests build it — plain object with a NUMERIC offset — or an
 * F# record — gets normalised to the F# record shape the domain expects.
 */
const cursorOf = (value) => ({
  Offset: offsetOf(value.Offset),
  ConsecutiveFailureCount: value.ConsecutiveFailureCount,
})

export const cursor = {
  initial: Cursor.initial,
  atOffset: (offset) => Cursor.atOffset(offsetOf(offset)),
  advance: (offset) => offsetValue(Cursor.advance(offsetOf(offset))),
  recordFailure: (value) => Cursor.recordFailure(cursorOf(value)),
  recordSuccess: (value) => Cursor.recordSuccess(cursorOf(value)),
  side: (offset) => caseOf(Cursor.side(offsetOf(offset))),
  sideSequence: (count) => listItems(Cursor.sideSequence(count)).map(caseOf),
  effectiveAgent: (pair, value) => Cursor.effectiveAgent(pair, cursorOf(value)),
  isValidAdvance: (prevOffset, nextOffset, prevCount, nextCount) =>
    Cursor.isValidAdvance(offsetOf(prevOffset), offsetOf(nextOffset), prevCount, nextCount),

  /** CTX-006: is this one of the primed slots (A′ / B′). */
  isRecoverySlot: (offset) => Cursor.isRecoverySlot(offsetOf(offset)),
  attemptIdentity: (session, run, root, providerRunId) => Cursor.attemptIdentity(session, run, root, providerRunId),

  /** FALLBACK-005: `MayContinue` | `Exhausted`, with the cursor as payload. */
  recoveryVerdict: (budget, value) => caseOf(Cursor.recoveryVerdict(budget, cursorOf(value))),

  defaultBudget: Cursor.DefaultAutoRecoveryBudget,

  /**
   * The cursor's two quantities as a plain object.
   *
   * `assert.deepEqual` compares prototypes, and every cursor coming out of the
   * domain is an F# record instance — so comparing one against `{ Offset, ... }`
   * fails on the class, not on the values, and the diff blames the wrong thing.
   */
  read: (value) => ({ offset: offsetValue(value.Offset), failures: value.ConsecutiveFailureCount }),
}

export const fallbackProjection = (() => {
  const m = bind(FallbackProj, 'FallbackProjection', [
    'forAuthority',
    'applyAdvance',
    'applyExhausted',
    'recordSuccess',
    'mayContinue',
  ])
  return {
    /** FALLBACK-001. There is deliberately no `empty`: a run and a root are required. */
    forAuthority: (runId, root) => m.forAuthority(runId, root),

    /** Rejections carry no payload, so the case name is the whole answer. */
    applyAdvance: (identity, prevOffset, nextOffset, count, current) => {
      const result = resultOf(m.applyAdvance(identity, offsetOf(prevOffset), offsetOf(nextOffset), count, current))
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    applyExhausted: (current) => m.applyExhausted(current),
    recordSuccess: (current) => m.recordSuccess(current),
    mayContinue: (budget, current) => m.mayContinue(budget, current),

    /** The durable state as plain JS, so a renamed field cannot read `undefined`. */
    read: (current) => ({
      logicalRun: idValue.logicalRun(current.LogicalRunId),
      authorityRoot: idValue.authorityRoot(current.AuthorityRootUserMessageId),
      offset: offsetValue(current.Cursor.Offset),
      failures: current.Cursor.ConsecutiveFailureCount,
      dedupeKeys: listItems(current.RecentFailureKeys).length,
      exhausted: current.Exhausted,
    }),
  }
})()

/**
 * COMPANION-005 / CTX-011: the Companion frame sequence and its coverage.
 *
 * `frame()` builds a `BlogFrame` by kind NAME, never by tag ordinal — inserting a
 * case ahead of `Squash` would otherwise silently turn every squash frame into an
 * entry, and no assertion would notice.
 */
export const blogProjection = (() => {
  const m = bind(BlogProj, 'BlogProjection', [
    'empty',
    'frameCount',
    'frames',
    'coverableFrames',
    'squashWidth',
    'applyEntry',
    'applySquash',
    'applyReanchor',
    'hasCoverage',
  ])
  const buildKind = unionCase(BlogProj.BlogFrameKind, 'BlogFrameKind')

  return {
    empty: m.empty,
    frameCount: (state) => m.frameCount(state),
    squashWidth: (state) => m.squashWidth(state),
    hasCoverage: (state) => m.hasCoverage(state),
    frameEpochOf: (state) => idValue.frameEpoch(state.FrameEpochId),

    frame: ({ kind, digest, ref, coveredFrom = 0, coveredThrough = 0 }) => ({
      Kind: buildKind(kind, []),
      Digest: blobDigest(digest),
      TextRef: blobRef(ref),
      CoveredFromSequence: BigInt(coveredFrom),
      CoveredThroughSequence: BigInt(coveredThrough),
    }),

    frames: (state) => m.frames(state),
    frameKinds: (state) => listItems(m.frames(state)).map((f) => caseOf(f.Kind)),

    cursor: (turn, part) => ({ TurnIndex: turn, PartIndex: part }),

    coverage: (state) => ({
      ingestedThroughSequence: Number(state.Coverage.IngestedThroughSequence),
      cutoff: state.Coverage.CoverableTurnCutoffExclusive,
      digest: state.Coverage.CoveredPrefixDigest,
      coverableFrames: state.Coverage.CoverableFrameCount,
    }),

    /** CTX-011: the frames a probe may build FrozenRecordPrefix from, by kind. */
    coverableFrameKinds: (state) => listItems(m.coverableFrames(state)).map((f) => caseOf(f.Kind)),

    /** Rejections carry payloads; the name alone is what a test asserts on. */
    applyEntry: ({ epoch, previous, next, previousCutoff, nextCutoff, digest, frame }, state) => {
      // The record coverage is an XTrace cursor sequence: Fable compiles int64 to
      // BigInt, so the facade converts before crossing (VERIFY-008).
      const result = resultOf(
        m.applyEntry(
          frameEpochId(epoch),
          BigInt(previous),
          BigInt(next),
          previousCutoff,
          nextCutoff,
          digest,
          frame,
          state,
        ),
      )
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    applySquash: ({ previousEpoch, nextEpoch, count, frame }, state) => {
      const result = resultOf(m.applySquash(frameEpochId(previousEpoch), frameEpochId(nextEpoch), count, frame, state))
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    applyReanchor: (state) => m.applyReanchor(state),
  }
})()

/**
 * ENFORCER-045/070/154: enforcement half of BlogObservationCommitted + bounded RecentTips.
 * VERIFY-008: tip RuleId / FieldName / CycleId only via this facade.
 */
export const enforcementProjection = (() => {
  const m = bind(EnforcementProj, 'EnforcementProjection', [
    'empty',
    'applyFromEntry',
    'applySquash',
    'tryFindByProviderRun',
    'recentTips',
  ])

  return {
    empty: m.empty,
    RecentTipLimit: EnforcementProj.RecentTipLimit ?? 8,

    /** Build an EnforcementCycleRecord (tip v2). */
    cycleRecord: ({
      mainSessionId,
      bloggerSessionId,
      run,
      toolCallIds = [],
      textRef,
      textDigest,
      tipRuleId,
      fieldNameAtCommit,
      evidenceRef,
      prefixEpoch = 0,
    }) => ({
      MainSessionId: typeof mainSessionId === 'string' ? sessionId(mainSessionId) : mainSessionId,
      BloggerSessionId: typeof bloggerSessionId === 'string' ? sessionId(bloggerSessionId) : bloggerSessionId,
      ProviderRun: typeof run === 'string' ? providerRun(run) : run,
      ToolCallIds: toList(toolCallIds.map((id) => (typeof id === 'string' ? toolCallId(id) : id))),
      CycleTextRef: typeof textRef === 'string' ? blobRef(textRef) : textRef,
      CycleTextDigest: typeof textDigest === 'string' ? blobDigest(textDigest) : textDigest,
      TipRuleId: tipRuleId,
      FieldNameAtCommit: fieldNameAtCommit,
      CycleEvidenceRef: evidenceRef == null ? undefined : typeof evidenceRef === 'string' ? blobRef(evidenceRef) : evidenceRef,
      ObservedPrefixEpochId: prefixEpochId(prefixEpoch),
    }),

    applyFromEntry: (state, record) => resultOf(m.applyFromEntry(state, record)),

    /** Co-truncate oldest tips with BlogSquash covered frame count. */
    applySquash: (count, state) => m.applySquash(count, state),

    tryFindByProviderRun: (run, state) => {
      const key = typeof run === 'string' ? providerRun(run) : run
      return unwrapOption(m.tryFindByProviderRun(key, state))
    },

    /** Oldest → newest RecentTip list (plain objects). */
    recentTips: (state) =>
      listItems(m.recentTips(state)).map((t) => ({
        ruleId: t.RuleId,
        fieldName: t.FieldName,
        cycleId: t.CycleId,
      })),

    tipRuleIdOf: (record) => record?.TipRuleId,
    fieldNameAtCommitOf: (record) => record?.FieldNameAtCommit,
  }
})()

/** COMPANION-009 / CTX-012: which X prefix generation is in force. */
export const prefixEpochProjection = (() => {
  const m = bind(PrefixProj, 'PrefixEpochProjection', [
    'empty',
    'applyRebase',
    'applyReanchor',
    'hasSnapshot',
    'isReanchored',
  ])

  return {
    empty: m.empty,
    hasSnapshot: (state) => m.hasSnapshot(state),
    epochOf: (state) => idValue.prefixEpoch(state.EpochId),

    snapshot: ({ ref, digest, cutoff, prefixDigest, sealRoot, syntheticId }) => ({
      FrozenRecordPrefixRef: blobRef(ref),
      FrozenRecordPrefixDigest: blobDigest(digest),
      CutoffExclusive: cutoff,
      CoveredPrefixDigest: prefixDigest,
      SealRoot: sealRoot,
      SyntheticMessageId: syntheticId,
    }),

    applyRebase: ({ previousEpoch, nextEpoch, candidate }, state) => {
      const result = resultOf(m.applyRebase(prefixEpochId(previousEpoch), prefixEpochId(nextEpoch), candidate, state))
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    /**
     * `observedRun` is the compaction pseudo-run being reanchored (HOST-006).
     *
     * Required, not optional: the projection records it so the same compaction cannot
     * be reanchored twice, and a facade default would let a test skip the argument and
     * silently exercise a shape production cannot produce.
     */
    applyReanchor: ({ previousEpoch, nextEpoch, observedRun }, state) => {
      const result = resultOf(
        m.applyReanchor(prefixEpochId(previousEpoch), prefixEpochId(nextEpoch), providerRun(observedRun), state),
      )
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    isReanchored: (run, state) => m.isReanchored(providerRun(run), state),

    reanchoredRuns: (state) => [...state.ReanchoredRuns].map(idValue.providerRun).sort(),
  }
})()