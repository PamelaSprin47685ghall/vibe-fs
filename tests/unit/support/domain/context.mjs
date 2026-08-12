// tests/unit/support/domain/context.mjs — context + companion family.
// Synthetic TOML, Magic Todo, XTrace capture, companion prompt/identity/projection,
// recovery slot, session association, compaction, probe selection, roles/catalog,
// session ownership, X-prefix projection algebra, prefix probe.

import {
  SyntheticTomlModule,
  MagicTodoModule,
  MagicTodoListCodecModule,
  MagicTodoAdmissionModule,
  MagicTodoHostCodecModule,
  MagicTodoFactsModule,
  MagicTodoProjectionModule,
  MagicTodoFactCodecModule,
  MagicTodoLocalityModule,
  MagicTodoMembraneModule,
  ToolResultBoundModule,
  ToolHostCodecModule,
  XTraceModule,
  XTraceCaptureModule,
  LifecycleWorkRecordProjectionModule,
  LifecycleWorkRecordModule,
  CompanionPromptModule,
  CompanionIdentityModule,
  CompanionBuilderModule,
  RecoverySlotModule,
  AssociationProj,
  CompactionPolicyModule,
  ProbeSelectionModule,
  RolesModule,
  ManagedAgentCatalogModule,
  XPrefixModule,
  ProjectionIntentModule,
  ProjectionPlannerModule,
  ProjectionRendererModule,
  ProjectionAlgebraModule,
  PrefixCandidateModule,
  ProviderProj,
  HostMessageCodecModule,
  unionCase,
  bind,
  member,
  caseOf,
  payloadOf,
  resultOf,
  toList,
  listItems,
  unwrapOption,
  isNone,
  mapEntries,
  setItems,
  stringSet,
  offsetOf,
  prod,
} from './interop.mjs'
import {
  sessionId,
  blobDigest,
  blobRef,
  prefixEpochId,
  frameEpochId,
  idValue,
  providerRun,
  toolCallId,
} from './identity.mjs'

/**
 * ARCH-010: the one canonical writer for runtime synthetic TOML.
 *
 * Exposed separately from `bloggerToml` because the ownership split is the point of the
 * clause. Blogger owns which parts exist and their key order; the string rules and the
 * document layout belong here, to every synthetic surface equally. A facade that let
 * `bloggerToml.renderString` keep working would tell the next reader that Blogger owns
 * string rendering — the local dialect ARCH-010 forbids.
 *
 * `document` resolves to Fable's `document$`: the plain name would collide with the DOM
 * global, so Fable escapes it. `member()` tries the `$` spelling last, which is what
 * turns that into a resolution rule rather than a per-call-site accident.
 */
export const syntheticToml = (() => {
  const m = bind(SyntheticTomlModule, 'SyntheticToml', [
    'normalizeNewlines',
    'renderString',
    'comment',
    'field',
    'tableArrayEntry',
    'tableEntry',
    'renderBool',
    'renderInt',
    'renderFloat',
    'renderKey',
    'encodeData',
    'encodeFs',
    'document',
    'byteCount',
  ])

  return {
    normalizeNewlines: (text) => m.normalizeNewlines(text),
    renderString: (text) => m.renderString(text),
    comment: (text) => m.comment(text),
    field: (name, renderedValue) => m.field(name, renderedValue),
    tableArrayEntry: (name, fields) => m.tableArrayEntry(name, toList(fields)),
    tableEntry: (name, fields) => m.tableEntry(name, toList(fields)),
    renderBool: (value) => m.renderBool(value),
    renderInt: (value) => m.renderInt(value),
    renderFloat: (value) => m.renderFloat(value),
    renderKey: (name) => m.renderKey(name),
    encodeData: (value) => listItems(m.encodeData(value)),
    encodeFs: (rewritten, created) => listItems(m.encodeFs(toList(rewritten), toList(created))),
    document: (instructions, body) => m.document(toList(instructions), toList(body)),
    byteCount: (text) => m.byteCount(text),
  }
})()

/**
 * Magic Todo pure algebra façade. Tests construct Fable values through this one
 * boundary instead of depending on emitted union ordinals or module spellings.
 */
export const magicTodo = (() => {
  const input = unionCase(MagicTodoModule.MagicTodoInputItem, 'MagicTodoInputItem')
  const listCodec = bind(MagicTodoListCodecModule, 'MagicTodoListCodec', ['encode', 'tryDecode'])
  const m = bind(MagicTodoModule, 'MagicTodo', [
    'todoWriteId',
    'todoItemId',
    'todoReviewId',
    'dedicatedReviewerId',
    'listDigest',
    'validateCompletedGate',
    'normalizeProposed',
    'semanticMerge',
    'settle',
    'admitTodowriteBatch',
    'checkPreparedReplay',
    'desiredLag1Cutoff',
    'workRecordStart',
    'blindPlanOpeningBoundary',
    'effectiveOpeningFloor',
    'bloggerEffectiveStart',
    'requireCheckpointBeforeFirstSuicide',
  ])

  return {
    ...m,
    encodeList: (items) => listCodec.encode(toList(items)),
    decodeList: (json) => resultOf(listCodec.tryDecode(json)),
    TodoStatus: MagicTodoModule.TodoStatus,
    MagicTodoInputItem: MagicTodoModule.MagicTodoInputItem,
    MagicTodoItem: MagicTodoModule.MagicTodoItem,
    ProcessReviewVerdict: MagicTodoModule.ProcessReviewVerdict,
    PreparedIdentity: MagicTodoModule.PreparedIdentity,
    todoItemIdCreate: MagicTodoModule.TodoItemIdModule_create,
    todoItemIdValue: MagicTodoModule.TodoItemIdModule_value,
    todoWriteIdCreate: MagicTodoModule.TodoWriteIdModule_create,
    todoWriteIdValue: MagicTodoModule.TodoWriteIdModule_value,
    existing: (id, content, status, priority) => input('Existing', [id, content, status, priority]),
    new: (content, status, priority) => input('New', [content, status, priority]),
    item: (id, content, status, priority) => new MagicTodoModule.MagicTodoItem(id, content, status, priority),
    perfect: MagicTodoModule.ProcessReviewVerdict.Perfect,
    revise: MagicTodoModule.ProcessReviewVerdict.Revise,
    workRecordStart: (openingSequence) => Number(m.workRecordStart({ Sequence: BigInt(openingSequence) }).Sequence),
    blindPlanOpeningBoundary: (openingSequence, t1CallSequence, t1CallIdValue, parts) =>
      Number(
        m.blindPlanOpeningBoundary(
          { Sequence: BigInt(openingSequence) },
          { Sequence: BigInt(t1CallSequence) },
          toolCallId(t1CallIdValue),
          toList(
            parts.map((part) => ({
              Cursor: { Sequence: BigInt(part.sequence) },
              Kind: part.kind,
              ToolCallId: part.toolCallId == null ? undefined : toolCallId(part.toolCallId),
            })),
          ),
        ).Sequence,
      ),
    effectiveOpeningFloor: (
      hasOpenLife,
      acceptedCount,
      openingSequence,
      t1CallSequence,
      t1CallIdValue,
      xTraceHeadSequence,
      parts = [],
    ) => {
      const floor = m.effectiveOpeningFloor(
        hasOpenLife,
        acceptedCount,
        { Sequence: BigInt(openingSequence) },
        t1CallSequence == null ? undefined : { Sequence: BigInt(t1CallSequence) },
        t1CallIdValue == null ? undefined : toolCallId(t1CallIdValue),
        BigInt(xTraceHeadSequence),
        toList(
          parts.map((part) => ({
            Cursor: { Sequence: BigInt(part.sequence) },
            Kind: part.kind,
            ToolCallId: part.toolCallId == null ? undefined : toolCallId(part.toolCallId),
          })),
        ),
      )
      return floor == null ? undefined : Number(floor.Sequence)
    },
    bloggerEffectiveStart: (ingestedThrough, workRecordStartSequence) =>
      Number(
        m.bloggerEffectiveStart(
          { IngestedThrough: { Sequence: BigInt(ingestedThrough) } },
          { Sequence: BigInt(workRecordStartSequence) },
        ).Sequence,
      ),
  }
})()

export const magicTodoAdmission = (() => {
  const m = bind(MagicTodoAdmissionModule, 'MagicTodoAdmission', ['admit'])

  return {
    admit: (sha256, life, settled, lagAdmission, existingPrepared, localized, inputs) =>
      m.admit(sha256, life, toList(settled), lagAdmission, existingPrepared, localized, toList(inputs)),
    LocalizedToolCall: MagicTodoAdmissionModule.LocalizedToolCall,
    ExistingPrepared: MagicTodoAdmissionModule.ExistingPrepared,
    PrepareSuccess: MagicTodoAdmissionModule.PrepareSuccess,

  }
})()

/** Magic Todo's raw Host argument and compatibility-output boundary. */
export const magicTodoHost = (() => {
  const m = bind(MagicTodoHostCodecModule, 'MagicTodoHostCodec', [
    'tryDecodeObligations',
    'canonicalInput',
    'canonicalInputDigest',
    'replaceCompatibilityArgs',
    'replaceEnrichedResult',
    'applyDefinition',
  ])

  return {
    decodeObligations: (args) => resultOf(m.tryDecodeObligations(args)),
    canonicalInput: (args) => m.canonicalInput(args),
    canonicalInputDigest: (sha256, args) => m.canonicalInputDigest(sha256, args),
    replaceCompatibilityArgs: (output, rows) => m.replaceCompatibilityArgs(output, toList(rows)),
    replaceEnrichedResult: (output, text) => m.replaceEnrichedResult(output, text),
    applyDefinition: (output) => m.applyDefinition(output),
  }
})()

/**
 * Magic Todo durable-fact/projection façade.
 */
export const magicTodoJournal = (() => {
  const fact = unionCase(MagicTodoFactsModule.MagicTodoFact, 'MagicTodoFact')
  const m = bind(MagicTodoProjectionModule, 'MagicTodoProjection', ['fold', 'foldConcluded'])
  const codec = bind(MagicTodoFactCodecModule, 'MagicTodoFactCodec', ['encode', 'tryDecode'])

  return {
    ...m,
    ...codec,
    fold: (event, state, value) => m.fold(event, state, value),
    empty: MagicTodoProjectionModule.empty,
    MagicTodoFact: fact,
    PhysicalSuccessEvidence: MagicTodoFactsModule.PhysicalSuccessEvidence,
    TodoWritePrepared: MagicTodoFactsModule.TodoWritePrepared,
    TodoWriteAccepted: MagicTodoFactsModule.TodoWriteAccepted,
    TodoProcessReviewAssigned: MagicTodoFactsModule.TodoProcessReviewAssigned,
    TodoReviewConcluded: MagicTodoFactsModule.TodoReviewConcluded,
    DedicatedTodoReviewerEnlisted: MagicTodoFactsModule.DedicatedTodoReviewerEnlisted,
    LegacyTodoSeedAdopted: MagicTodoFactsModule.LegacyTodoSeedAdopted,
    XTraceCursor: XTraceModule.XTraceCursor,
  }
})()

/**
 * Custom tool result pre-bound (tail kept) under OpenCode Host Truncate defaults.
 * Host: 2000 lines / 51200 bytes / default head. We keep tail so Host no-ops.
 */
export const toolResultBound = (() => {
  const m = bind(ToolResultBoundModule, 'ToolResultBound', [
    'HostMaxLines',
    'HostMaxBytes',
    'Marker',
    'MarkerBytes',
    'ContentMaxLines',
    'ContentMaxBytes',
    'bound',
  ])

  return {
    hostMaxLines: m.HostMaxLines,
    hostMaxBytes: m.HostMaxBytes,
    marker: m.Marker,
    markerBytes: m.MarkerBytes,
    contentMaxLines: m.ContentMaxLines,
    contentMaxBytes: m.ContentMaxBytes,
    bound: (text) => m.bound(text),
  }
})()

/**
 * COMPANION-003 / HOST-005: XTrace — X 的唯一原始语义轨迹。
 *
 * Cursor 是独立单调序列（不随 Host compaction 作废）；part 复用 SemanticPart
 * 语义；renderer 永不输出 provenance。
 *
 * Fable 把 int64 编译为 BigInt，facade 在此吸收（VERIFY-008：Fable 约定只允许
 * 出现在 domain.mjs）。
 */export const xTrace = (() => {
  const m = bind(XTraceModule, 'XTrace', [
    'originCursor',
    'nextCursor',
    'isAfter',
    'sliceBetween',
    'sliceFrom',
    'head',
    'flatten',
    'isWorkRecordPart',
    'forWorkRecord',
    'forOpening',
    'renderItem',
    'render',
  ])
  const semanticPart = unionCase(ProviderProj.SemanticPart, 'SemanticPart')

  const part = (kind, ...fields) => semanticPart(kind, fields)

  const cursorOf = ({ Sequence }) => ({ Sequence: Number(Sequence) })
  const toCursor = (sequence) => ({ Sequence: BigInt(sequence) })
  const toCursorList = (items) => toList(items.map((item) => ({ ...item, Cursor: toCursor(item.Cursor.Sequence) })))
  const fromCursorList = (list) => listItems(list).map((item) => ({ ...item, Cursor: cursorOf(item.Cursor) }))

  return {
    originCursor: cursorOf(m.originCursor),
    next: (cursor) => cursorOf(m.nextCursor(toCursor(cursor.Sequence))),
    isAfter: (next, previous) => m.isAfter(toCursor(next.Sequence), toCursor(previous.Sequence)),
    sliceBetween: (start, end, items) => fromCursorList(m.sliceBetween(toCursor(start.Sequence), toCursor(end.Sequence), toCursorList(items))),
    sliceFrom: (start, items) => fromCursorList(m.sliceFrom(toCursor(start.Sequence), toCursorList(items))),
    head: (items) => cursorOf(m.head(toCursorList(items))),
    text: (value) => part('SemanticText', value),
    reasoning: (value) => part('SemanticReasoning', value),
    toolCall: (name, args) => part('SemanticToolCall', name, args),
    toolResult: (value) => part('SemanticToolResult', value),
    media: (mediaType, digest) => part('SemanticMedia', mediaType, digest),

    /** 一个 XTraceItem。`part` 必须是本 facade 的 part 构造器产物。 */
    item: ({ sequence, role = 'user', part: partValue, provenance = '' } = {}) => ({
      Cursor: { Sequence: sequence },
      Provenance: provenance,
      Role: role,
      Part: partValue,
    }),

    /** `[{ role, parts }]` → 平铺的 `{ role, part }` F# list。 */
    flatten: (turns) => {
      const result = m.flatten(toList(turns.map((turn) => ({ Role: turn.role, Parts: toList(turn.parts) }))))
      return listItems(result).map((entry) => ({ role: entry.Role, part: entry.Part }))
    },

    renderItem: m.renderItem,
    render: m.render,
    /** COMPANION-003: LWR projection — drop raw tool call/result. */
    forWorkRecord: (items) => fromCursorList(m.forWorkRecord(toCursorList(items))),
    /** COMPANION-014: Opening interval — keep constitutive T1 tools. */
    forOpening: (items) => fromCursorList(m.forOpening(toCursorList(items))),
    isWorkRecordPart: (partValue) => m.isWorkRecordPart(partValue),
    toItems: (items) => toList(items),
  }
})()

/**
 * COMPANION-003 / COMPANION-012: 唯一 semantic capture mapper。
 *
 * MessagePart → SemanticPart。Activity 是 transport bookkeeping，被丢弃。
 */
export const xTraceCapture = (() => {
  const m = bind(XTraceCaptureModule, 'XTraceCapture', ['semanticPart', 'captureProjection', 'captureMessageView', 'captureOpening'])
  const semanticPart = unionCase(ProviderProj.SemanticPart, 'SemanticPart')
  const messagePart = unionCase(HostMessageCodecModule.MessagePart, 'MessagePart')

  const part = (kind, ...fields) => messagePart(kind, fields)

  return {
    text: (value) => part('Text', value),
    reasoning: (value) => part('Reasoning', value),
    toolCall: (callId, name, args) => part('ToolCall', callId, name, args),
    toolResult: (callId, result) => part('ToolResult', callId, result),
    activity: (kind) => part('Activity', kind),

    map: (messagePartValue) => {
      const mapped = m.semanticPart(messagePartValue)
      return isNone(mapped) ? undefined : { tag: caseOf(mapped), part: mapped }
    },

    /** `semantic({ messages: [{ role, parts }] })` → ProviderSemanticProjection. */
    semantic: ({ messages = [] } = {}) => ({
      ProviderId: undefined,
      ModelId: undefined,
      Variant: undefined,
      Tools: toList([]),
      System: toList([]),
      Messages: toList(
        messages.map((turn) => ({
          Role: turn.role,
          Parts: toList(
            turn.parts.flatMap((part) => {
              const mapped = m.semanticPart(part)
              return isNone(mapped) ? [] : [mapped]
            }),
          ),
        })),
      ),
    }),

    /**
     * COMPANION-007: synchronise the XTrace with the semantic projection.
     * `journal` is the `{ journal }` from `agentJournal.create`.
     * Returns the updated XTrace projection state (or `undefined` without a
     * journal).
     */
    captureProjection: (journal, sessionIdValue, semanticProjection) => {
      const result = m.captureProjection(journal, sessionIdValue, semanticProjection)
      return isNone(result) ? undefined : result
    },

    captureMessageView: (journal, sessionIdValue, capturedMessages) => {
      const result = m.captureMessageView(journal, sessionIdValue, toList(capturedMessages))
      return isNone(result) ? undefined : result
    },

    /** COMPANION-003: capture the opening; requirements are a JS array. */
    captureOpening: (journal, sessionIdValue, text, requirements = []) =>
      m.captureOpening(journal, sessionIdValue, text, toList(requirements)),
  }
})()

/** Journal-backed LWR projection (COMPANION-003 / EXEC-006). Not domain materialize. */
export const lifecycleWorkRecordProjection = (() => {
  const m = bind(LifecycleWorkRecordProjectionModule, 'LifecycleWorkRecordProjection', ['lifecycleWorkRecord'])
  return {
    lifecycleWorkRecord: (journal, sessionIdValue, includeOpening = true) => {
      const result = m.lifecycleWorkRecord(journal, sessionIdValue, includeOpening)
      return isNone(result) ? undefined : result
    },
  }
})()

/** COMPANION-003: LWR — 唯一跨 Session 工作记录。 */export const lifecycleWorkRecord = (() => {
  const m = bind(LifecycleWorkRecordModule, 'LifecycleWorkRecord', ['render', 'materialize', 'withConstitutive'])
  const opening = ({ assignment = '', requirements = [], constitutive = '' } = {}) => ({
    AssignmentText: assignment,
    AuthoritativeRequirements: toList(requirements),
    ConstitutiveBody: constitutive,
  })

  return {
    opening,
    render: (record, includeOpening = true) => m.render(includeOpening, record),
    withConstitutive: (openingValue, constitutiveItems) =>
      m.withConstitutive(openingValue, toList(constitutiveItems)),
    // Default includeOpening=true (parent→child / same-session). Pass false for join.
    materialize: (
      openingValue,
      frames,
      traceItems,
      ingestedThrough,
      terminalItems,
      openingEnd = { Sequence: 0 },
      includeOpening = true,
    ) =>
      m.materialize(
        openingValue,
        toList(frames),
        toList(traceItems),
        { IngestedThrough: { Sequence: BigInt(ingestedThrough.Sequence) } },
        { Sequence: BigInt(openingEnd.Sequence) },
        toList(terminalItems),
        includeOpening,
      ),
  }
})()

/** COMPANION-004/005 / ENFORCER-030: request strings; system lives in provider/role/blogger. */
export const companionPrompt = {
  normalInstruction: CompanionPromptModule.NormalInstruction,
  squashInstruction: CompanionPromptModule.SquashInstruction,
  memoryPreamble: CompanionPromptModule.CompanionMemoryPreamble,
  workingRecord: (body) => CompanionPromptModule.workingRecordMessage(body),
  /** ENFORCER-071: previous tip as low-trust assistant body. */
  previousTip: (tipField, cycleId) => CompanionPromptModule.previousTipMessage(tipField, cycleId),
  newWork: (toml) => CompanionPromptModule.newWorkMessage(toml),
  memoryBlock: (frozenRecordPrefix) => CompanionPromptModule.companionMemoryBlock(frozenRecordPrefix),
}

/**
 * COMPANION-013: the four synthetic identity formulas.
 *
 * `sha256` is injected so a test can supply a visible, deterministic stand-in and
 * assert on the INPUT the formula composed. Asserting on real hex would only prove
 * the digest is stable, not that the right fields went into it.
 */
export const companionIdentity = {
  sealRoot: (sha256, { session, epoch, cutoff, prefixDigest, frozenDigest }) =>
[…396ln elided…]
    allRoles: () => listItems(m.allRoles).map(caseOf),
    managerForkableRoles: () => listItems(m.managerForkableRoles).map(caseOf),
    managerForkableNames: () => listItems(m.managerForkableNames),
    /** AGENT-002: exactly 22 names (20 Role × tier + Bookkeeper pair). */
    requiredNames: () => listItems(m.requiredNames),
    orchestratorForkableNames: () => listItems(m.orchestratorForkableNames),
    inspectorToolNames: () => listItems(m.inspectorToolNames),
    coderToolNames: () => listItems(m.coderToolNames),
    /** AGENT-004: the exact bare legacy names. */
    legacyAgentNames: () => setItems(m.legacyAgentNames),
    /** AGENT-004: legacy rejection predicate (lowercase input). */
    isLegacyAgentName: (lower) => m.isLegacyAgentName(lower),
    /** AGENT-004: version-agnostic rejection prose. */
    formatLegacyNameNotSupported: (name) => m.formatLegacyNameNotSupported(name),
    formatLegacyNameInConfig: (name) => m.formatLegacyNameInConfig(name),
    /** AGENT-002: InternalLeaf Bookkeeper pair. */
    bookkeeperNames: () => listItems(m.bookkeeperNames),
    isBookkeeperName: (name) => m.isBookkeeperName(name),
    tryParseBookkeeperTier: (name) => unwrapOption(m.tryParseBookkeeperTier(name)),
    bookkeeperNameOf: (tier) => m.bookkeeperNameOf(tier),
    bookkeeperPeerName: (name) => unwrapOption(m.bookkeeperPeerName(name)),
  }
})()

// ── SyncDelegate vocabulary (EXEC-026 / HOST-008) ────────────────────────────
// Types + pure helpers only. SyncDelegateRuntime is deliberately not surfaced.

const SyncDelegateModule = await prod('Kernel/SyncDelegate')
const SessionOwnershipModule = await prod('Kernel/SessionOwnership')

const buildSyncDelegateRole = unionCase(SyncDelegateModule.SyncDelegateRole, 'SyncDelegateRole')
const buildAttachmentKind = unionCase(SessionOwnershipModule.AttachmentKind, 'AttachmentKind')
const buildSessionOwnership = unionCase(SessionOwnershipModule.SessionOwnership, 'SessionOwnership')
const buildSessionExecutionClass = unionCase(
  SessionOwnershipModule.SessionExecutionClass,
  'SessionExecutionClass',
)

/** EXEC-026: reuse-scope half of a dedicated SyncDelegate key. */
export const reuseScopeId = {
  create: (value) => SyncDelegateModule.ReuseScopeIdModule_create(value),
  value: (id) => SyncDelegateModule.ReuseScopeIdModule_value(id),
  equals: (a, b) => SyncDelegateModule.ReuseScopeIdModule_equals(a, b),
}

/** EXEC-026: `(ReuseScopeId, SyncDelegateRole)` dedicated-session key. */
export const dedicatedDelegateKey = {
  create: (scope, role) => SyncDelegateModule.DedicatedDelegateKeyModule_create(scope, role),
}

/** HOST-008 AttachmentKind constructors (Bookkeeper carries a transaction id). */
export const attachmentKind = {
  of: (name, fields = []) => buildAttachmentKind(name, fields),
  companion: () => SessionOwnershipModule.AttachmentKind.Companion,
  syncInspector: () => SessionOwnershipModule.AttachmentKind.SyncInspector,
  syncCoder: () => SessionOwnershipModule.AttachmentKind.SyncCoder,
  bookkeeper: (transactionId) => buildAttachmentKind('Bookkeeper', [transactionId]),
}

/** HOST-008 SessionExecutionClass + predicates. */
export const sessionExecutionClass = {
  of: (name) => buildSessionExecutionClass(name, []),
  isWork: (value) => SessionOwnershipModule.SessionExecutionClassModule_isWork(value),
  isInternalLeaf: (value) => SessionOwnershipModule.SessionExecutionClassModule_isInternalLeaf(value),
}

/** HOST-008 SessionOwnership constructors + tryOwner / attachmentKind. */
export const sessionOwnership = {
  root: () => SessionOwnershipModule.SessionOwnership.Root,
  attached: (ownerSessionId, attachment) => buildSessionOwnership('Attached', [ownerSessionId, attachment]),
  tryOwner: (ownership) => unwrapOption(SessionOwnershipModule.SessionOwnershipModule_tryOwner(ownership)),
  attachmentKind: (ownership) =>
    unwrapOption(SessionOwnershipModule.SessionOwnershipModule_attachmentKind(ownership)),
}

/**
 * EXEC-026 / HOST-008: SyncDelegateRole + pure mapping helpers.
 * Role values are built by case name so a renamed DU case fails at construction.
 */
export const syncDelegate = {
  role: (name) => buildSyncDelegateRole(name, []),
  delegateRoleToAttachment: (role) => SyncDelegateModule.SyncDelegate_delegateRoleToAttachment(role),
  tierForOwner: (ownerTier) => SyncDelegateModule.SyncDelegate_tierForOwner(ownerTier),
  agentNameFor: (role, tier) => SyncDelegateModule.SyncDelegate_agentNameFor(role, tier),
}

/**
 * COMPANION-009 / CTX-010: which prefix X sends, as a `ProjectionIntent` (PROJ-005).
 *
 * `frozenBBody` is supplied by the caller because the snapshot carries a `BlobRef`,
 * never the body (PERSIST-007). Passing it here is the same split `ResolvedPrefixMemory`
 * makes in production: the journal records where the body is, and only a resolved copy
 * reaches the transform boundary.
 *
 * The facade flattens the intent into the plan-shaped view the legacy `XPrefixPlan`
 * exposed, so tests keep asserting the same business facts (drop leading count,
 * synthetic id reuse, low-trust memory block, replace-or-not) without learning the
 * union wire shape.
 */
export const xPrefix = (() => {
  const m = bind(XPrefixModule, 'XPrefixProjection', ['forSnapshot', 'forChoice', 'requiredBlob'])

  const intentOf = (intent) => {
    const name = caseOf(intent)

    if (name === 'KeepPhysicalPrefix') {
      return {
        intent: name,
        replacesPrefix: false,
        dropLeading: 0,
        memoryId: undefined,
        memoryText: undefined,
      }
    }

    const activation = payloadOf(intent)

    return {
      intent: name,
      replacesPrefix: true,
      dropLeading: activation.DropLeading,
      memoryId: activation.SyntheticMessageId,
      memoryText: activation.Memory,
    }
  }

  return {
    forSnapshot: (snapshot, frozenBBody = '') => intentOf(m.forSnapshot(snapshot, frozenBBody)),
    forChoice: (choice, committed, frozenBBody = '') => intentOf(m.forChoice(choice, committed, frozenBBody)),

    /** `undefined` when the plan needs no blob read. */
    requiredBlob: (choice, committed) => {
      const ref = unwrapOption(m.requiredBlob(choice, committed))
      return isNone(ref) ? undefined : idValue.blobRef(ref)
    },
  }
})()

/** PROJ-005: a `ProjectionIntent`, built by case name. */
export const projectionIntent = (() => {
  // Resolved at call time so a missing step-3a case fails the test that needs it,
  // not the whole facade import for stage 1–2 tests.
  const build = (caseName, fields = []) =>
    unionCase(ProjectionAlgebraModule.ProjectionIntent, 'ProjectionIntent')(caseName, fields)

  return {
    get keepPhysicalPrefix() {
      return build('KeepPhysicalPrefix', [])
    },
    activatePrefixEpoch: (activation) => build('ActivatePrefixEpoch', [activation]),
    /**
     * PROJ-008 step 3: Y frames from Snapshot.BlogFrames + Companion rebuild payload.
     * Defaults keep step-3a algebra smokes working (empty session/tips/delta → frames only
     * or empty no-op when Snapshot.BlogFrames is empty).
     */
    insertBlogFrames: (
      intent = {
        RequestKind: 'normal',
        SquashFrameCount: 0,
        BloggerSessionId: 'ses_blogger',
        FrameEpoch: 0,
        PhysicalDelta: undefined,
        PreviousTips: [],
      },
    ) => {
      const payload = {
        RequestKind: intent.RequestKind ?? 'normal',
        SquashFrameCount: intent.SquashFrameCount ?? 0,
        BloggerSessionId: intent.BloggerSessionId ?? 'ses_blogger',
        FrameEpoch: intent.FrameEpoch ?? 0,
        PhysicalDelta:
          intent.PhysicalDelta === undefined || intent.PhysicalDelta === null
            ? undefined
            : Array.isArray(intent.PhysicalDelta)
              ? intent.PhysicalDelta
              : [intent.PhysicalDelta.messageId ?? intent.PhysicalDelta[0], intent.PhysicalDelta.toml ?? intent.PhysicalDelta[1]],
        PreviousTips: toList(
          (intent.PreviousTips ?? []).map((t) =>
            Array.isArray(t) ? t : [t.field ?? t[0], t.cycleId ?? t[1]],
          ),
        ),
      }
      return build('InsertBlogFrames', [payload])
    },
    /** PROJ-008 step 4: InteractionRepair instruction. */
    insertRepair: (intent) => build('InsertRepair', [intent]),
    /** COMPANION-012: drop message ids listed in Snapshot.TransportMessages. */
    get suppressTransportOnly() {
      return build('SuppressTransportOnly', [])
    },
    /** PROJ-008 step 5: REVIEW-003 skeptical challenge. */
    appendReviewChallenge: (intent = { TextVersion: 1 }) => build('AppendReviewChallenge', [intent]),
    /** PROJ-008 step 6: Host compaction reanchor (renderer no-op on wire bytes). */
    get reanchorAfterCompaction() {
      return build('ReanchorAfterCompaction', [])
    },
    nameOf: (intent) => caseOf(intent),
  }
})()
/**
 * PROJ-004/006: the pure planner and canonical renderer of the projection DSL.
 *
 * The renderer's wire view (`renderMessages`) is what a digest is computed from —
 * byte-equal to the Host's decode of the written-back message list, so tests can
 * assert the DSL's bytes without touching Host objects.
 */
export const projectionAlgebra = (() => {
  const planner = bind(ProjectionAlgebraModule, 'ProjectionPlanner', ['plan'])
  // Stage 1–2 members only at load: step-3a APIs resolve lazily so a missing
  // production export fails the step-3a tests rather than the whole facade import.
  const renderer = bind(ProjectionAlgebraModule, 'ProjectionRenderer', ['renderPrefix', 'renderMessages', 'cutoffDigest'])

  const wireViewOf = (messages) =>
    listItems(messages).map((message) => ({
      role: message.Role,
      parts: listItems(message.Parts).map((part) => {
        const kind = caseOf(part)
        const payload = payloadOf(part)
        if (kind === 'WireText' || kind === 'WireReasoning') {
          return { kind, text: payload }
        }
        if (kind === 'WireToolResult') {
          const [callId, result] = part.fields ?? (Array.isArray(payload) ? payload : [undefined, payload])
          return { kind, callId, text: result }
        }
        if (kind === 'WireToolCall') {
          const [callId, name, args] = Array.isArray(payload) ? payload : [undefined, undefined, payload]
          return { kind, callId, name, text: args }
        }
        return { kind, payload }
      }),
    }))

  const renderOf = (rendered) => {
    const name = caseOf(rendered)
    if (name === 'PhysicalPrefix') return { name, activation: undefined }
    return { name, activation: payloadOf(rendered) }
  }

  return {
    /** Result<ProjectionIntent list, ProjectionConflict>. */
    plan: (intents) => {
      const result = resultOf(planner.plan(toList(intents)))

      if (result.ok) {
        return { ok: true, intents: listItems(result.value).map((intent) => caseOf(intent)) }
      }

      const error = result.error
      const conflict = caseOf(error)
      const payload = payloadOf(error)

      // Prefix conflicts carry two intents; other conflicts may be unit-like.
      if (Array.isArray(payload) && payload.length === 2 && payload[0]?.cases) {
        return {
          ok: false,
          conflict,
          first: caseOf(payload[0]),
          second: caseOf(payload[1]),
        }
      }

      return { ok: false, conflict }
    },

    renderPrefix: (intents) => renderOf(renderer.renderPrefix(toList(intents))),

    /** A `RenderedPrefix`, built by case name (for write-back tests). */
    rendered: (() => {
      const build = unionCase(ProjectionAlgebraModule.RenderedPrefix, 'RenderedPrefix')

      return {
        physical: build('PhysicalPrefix', []),
        synthetic: (activation) => build('SyntheticPrefix', [activation]),
        nameOf: (rendered) => caseOf(rendered),
      }
    })(),

    /** wire view: digest-ready description of the rendered bytes. */
    renderMessages: (messages, rendered) => wireViewOf(renderer.renderMessages(messages, rendered)),

    /**
     * PROJ-008 step 3a: fold ordered intents over base wire messages against a
     * ProjectionSnapshot. Lazy: missing production export fails only callers.
     * Production injects real sha256 via renderMessagesWithHostIds; this facade
     * keeps wire-only shape (default identity sha256 inside F#).
     */
    renderMessagesWithIntents: (snapshot, baseWireMessages, orderedIntents) => {
      const render = member(ProjectionAlgebraModule, 'ProjectionRenderer', 'renderMessagesWithIntents')
      const toWirePart = (p) => {
        if (p.kind === 'WireText') return new ProviderProj.WirePart(0, [p.text])
        if (p.kind === 'WireReasoning') return new ProviderProj.WirePart(1, [p.text])
        if (p.kind === 'WireToolCall') return new ProviderProj.WirePart(2, [p.callId, p.name, p.text])
        if (p.kind === 'WireToolResult') return new ProviderProj.WirePart(3, [p.callId, p.text])
        return p
      }
      const toWireMsg = (m) => {
        if (m.Role !== undefined) return m
        const parts = toList((m.parts || []).map(toWirePart))
        return new ProviderProj.WireMessage(m.role, parts)
      }
      const items = Array.isArray(baseWireMessages) ? baseWireMessages : listItems(baseWireMessages)
      const encoded = toList(items.map(toWireMsg))
      return wireViewOf(render(snapshot, encoded, toList(orderedIntents)))
    },

    /**
     * PROJ-004: wire + Host MessageId / IsPhysical side-channel (injected sha256).
     * Lazy: only callers that need ids bind this export.
     */
    renderMessagesWithHostIds: (sha256, snapshot, baseWireMessages, orderedIntents) => {
      const render = member(ProjectionAlgebraModule, 'ProjectionRenderer', 'renderMessagesWithHostIds')
      const toWirePart = (p) => {
        if (p.kind === 'WireText') return new ProviderProj.WirePart(0, [p.text])
        if (p.kind === 'WireReasoning') return new ProviderProj.WirePart(1, [p.text])
        if (p.kind === 'WireToolCall') return new ProviderProj.WirePart(2, [p.callId, p.name, p.text])
        if (p.kind === 'WireToolResult') return new ProviderProj.WirePart(3, [p.callId, p.text])
        return p
      }
      const toWireMsg = (m) => {
        if (m.Role !== undefined) return m
        const parts = toList((m.parts || []).map(toWirePart))
        return new ProviderProj.WireMessage(m.role, parts)
      }
      const items = Array.isArray(baseWireMessages) ? baseWireMessages : listItems(baseWireMessages)
      const encoded = toList(items.map(toWireMsg))
      const rendered = render(sha256, snapshot, encoded, toList(orderedIntents))
      const optionString = (id) => {
        if (isNone(id)) return null
        // Fable may box Some as value or as union with fields[0].
        if (typeof id === 'object' && id !== null && 'fields' in id) {
          const fields = id.fields
          return Array.isArray(fields) && fields.length > 0 ? fields[0] : null
        }
        return id
      }
      return {
        messages: wireViewOf(rendered.Messages),
        hostMessageIds: listItems(rendered.HostMessageIds).map(optionString),
        hostIsPhysical: listItems(rendered.HostIsPhysical),
      }
    },

    renderedOf: renderOf,

    /**
     * CTX-011 step 5: the digest proof of X's current prefix at the candidate
     * cutoff (PROJ-008 stage 2 — attempt-local probe projection).
     */
    cutoffDigest: (sha256, snapshot, cutoff) => renderer.cutoffDigest(sha256, snapshot, cutoff),
  }
})()

/**
 * PROJ-002 step 3a snapshot fields (consumer-driven): BlogFrames,
 * TransportMessages, HostReanchor. Stage-1 fields remain CurrentProjection /
 * CommittedPrefix. Domain mirrors frame kinds as ProjectionBlogFrameKind
 * (not Journal.BlogFrameKind) without Journal dependency.
 *
 * Kind resolution is lazy: missing Domain cases fail step-3a tests only.
 */
/**
 * PROJ-008 Domain constants (ProjectionConstants). Single source for repair /
 * pair / challenge text; Host modules must reference these rather than literals.
 */
export const projectionConstants = (() => {
  const names = ['RepairInstruction', 'PairProgrammingGuidelineText', 'ReviewChallengeText', 'ReviewChallengePrompt']
  const out = {}
  for (const name of names) {
    try {
      out[name] = ProjectionAlgebraModule['ProjectionConstants_' + name] ?? member(ProjectionAlgebraModule, 'ProjectionConstants', name)
    } catch {
      out[name] = undefined
    }
  }
  return out
})()

export const projectionSnapshot = {
  /** Domain ResolvedBlogFrame (digest as hex string). */
  blogFrame: ({ kind = 'Entry', digest = 'frame-digest', body = 'frame body' } = {}) => {
    const kindUnion =
      ProjectionAlgebraModule.ProjectionBlogFrameKind ??
      ProjectionAlgebraModule.BlogFrameKind ??
      ProjectionAlgebraModule.ResolvedBlogFrameKind
    if (kindUnion === undefined) {
      throw new Error(
        'ProjectionAlgebra exports neither ProjectionBlogFrameKind nor BlogFrameKind (PROJ-008 step 3a)',
      )
    }
    const resolvedKind =
      typeof kind === 'string' ? unionCase(kindUnion, 'ProjectionBlogFrameKind')(kind, []) : kind
    return { Kind: resolvedKind, Digest: digest, Body: body }
  },
  hostReanchor: ({ previous = 'epoch-0', next = 'epoch-1', run = 'compact-1' } = {}) => ({
    PreviousEpochId: previous,
    NextEpochId: next,
    ObservedCompactionRunId: run,
  }),
  of: ({
    currentProjection,
    committedPrefix = undefined,
    blogFrames = [],
    transportMessages = [],
    hostReanchor = undefined,
  }) => ({
    CurrentProjection: currentProjection,
    CommittedPrefix: committedPrefix,
    BlogFrames: toList(blogFrames),
    TransportMessages: stringSet(transportMessages),
    HostReanchor: hostReanchor,
  }),
}/** CTX-010: the two prefix choices, built by case name. */
export const projectionChoice = (() => {
  const build = unionCase(PrefixCandidateModule.XProjectionChoice, 'XProjectionChoice')

  return {
    committed: build('UseCommittedEpoch', []),
    probe: (value) => build('UsePrefixProbe', [value]),
    nameOf: (choice) => caseOf(choice),
  }
})()

/**
 * CTX-010: a `PrefixProbe`, for a test that must hand one to production.
 *
 * Built here rather than as an object literal at each call site so the field names live
 * in one place. A misspelled field on an F# record reaching JS reads as `undefined`
 * rather than failing — the same silent class as the three hazards this facade exists
 * to close.
 */
export const prefixProbe = ({ id = 'probe-1', basedOnEpoch = 0, candidate }) => ({
  ProbeId: id,
  BasedOnEpochId: prefixEpochId(basedOnEpoch),
  Candidate: candidate,
})

/** CTX-011: a `NoCandidateReason`, by case name. Payload-carrying cases take fields. */
export const noCandidateReason = (() => {
  const build = unionCase(ProbeSelectionModule.NoCandidateReason, 'NoCandidateReason')
  return (name, ...fields) => build(name, fields)
})()

export const magicTodoLocality = (() => {
  const m = bind(MagicTodoLocalityModule, 'MagicTodoLocality', ['resolve'])

  return {
    resolve: (sessionIdValue, messages, projection, callId) =>
      resultOf(m.resolve(sessionIdValue, toList(messages), projection, callId)),
  }
})()

export const magicTodoMembrane = (() => {
  const m = bind(MagicTodoMembraneModule, 'MagicTodoMembrane', ['prepare', 'accept'])

  return {
    prepare: (journal, sessionIdValue, locality, inputDigest, obligations) =>
      resultOf(m.prepare(journal, sessionIdValue, locality, inputDigest, toList(obligations))),
    accept: (journal, bridge, physicalEvidence, inputDigest, outputDigest) =>
      resultOf(m.accept(journal, bridge, physicalEvidence, inputDigest, outputDigest)),
  }
})()

/**
 * HOST-011: ToolContext decode at the adapter boundary.
 * callID + messageID must both be present; either missing → None fail-closed.
 */
export const toolHostCodec = (() => {
  const decodeContext = member(ToolHostCodecModule, 'ToolHostCodec', 'decodeContext')
  return {
    decodeContext: (raw) => {
      const ctx = decodeContext(raw)
      return {
        sessionId: ctx.SessionId,
        agent: unwrapOption(ctx.Agent),
        toolCallId: (() => {
          const id = unwrapOption(ctx.ToolCallId)
          return id === undefined ? undefined : idValue.toolCall(id)
        })(),
        providerRunId: (() => {
          const id = unwrapOption(ctx.ProviderRunId)
          return id === undefined ? undefined : idValue.providerRun(id)
        })(),
        promptText: unwrapOption(ctx.PromptText),
      }
    },
  }
})()

[Showing lines 1-494 and 891-1384 of 1384; 396 middle lines (13.9KB) elided. Read artifact://124 for full output]