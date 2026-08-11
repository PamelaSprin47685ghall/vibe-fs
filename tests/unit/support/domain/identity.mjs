// tests/unit/support/domain/identity.mjs — identity family.
// Ids table + id constructors + identity adapters (PROMPT-001/HOST-013/HOST-004).

import { Identity, unionCase, unwrapOption } from './interop.mjs'

// ── identity ─────────────────────────────────────────────────────────────────
// PROMPT-001: no generic message id. `role=user` on the wire is a
// PhysicalUserMessageId; the semantic root is an AuthorityRootUserMessageId; a
// `role=assistant` message is a ProviderRunIdentity. The absence of a
// `messageId(...)` helper here is the clause, not an omission.

const idModule = (name) => ({
  create: (value) => Identity[`${name}Module_create`](value),
  value: (id) => Identity[`${name}Module_value`](id),
})

const Ids = {
  runtime: idModule('RuntimeId'),
  session: idModule('SessionId'),
  child: idModule('ChildId'),
  process: idModule('ProcessId'),
  event: idModule('EventId'),
  logicalRun: idModule('LogicalRunId'),
  authorityRoot: idModule('AuthorityRootUserMessageId'),
  physicalUser: idModule('PhysicalUserMessageId'),
  promptKey: idModule('PromptKey'),
  transportReceipt: idModule('TransportReceipt'),
  providerRun: idModule('ProviderRunIdentity'),
  toolCall: idModule('ToolCallId'),
  hostToolPart: idModule('HostToolPartId'),
  systemPrompt: idModule('SystemPromptId'),
  reviewBarrier: idModule('ReviewBarrierId'),
  gitTree: idModule('GitTreeHash'),
  sealDigest: idModule('SealDigest'),
  agentHandle: idModule('AgentHandleId'),
  ptyHandle: idModule('PtyHandleId'),
  managerJob: idModule('ManagerJobId'),
  worktreeIdentity: idModule('WorktreeIdentity'),
  worktreePath: idModule('WorktreePath'),
  targetRef: idModule('TargetRef'),
  commit: idModule('CommitHash'),
  blobRef: idModule('BlobRef'),
  blobDigest: idModule('BlobDigest'),
  bloggerRequest: idModule('BloggerRequestId'),
  managerLife: idModule('ManagerLifeId'),
  finalityRequest: idModule('FinalityRequestId'),
}

export const runtimeId = (v) => Ids.runtime.create(v)
export const sessionId = (v) => Ids.session.create(v)
export const childId = (v) => Ids.child.create(v)
export const processId = (v) => Ids.process.create(v)
export const eventId = (v) => Ids.event.create(v)
export const logicalRunId = (v) => Ids.logicalRun.create(v)
export const authorityRoot = (v) => Ids.authorityRoot.create(v)
export const physicalUser = (v) => Ids.physicalUser.create(v)
export const promptKey = (v) => Ids.promptKey.create(v)
export const transportReceipt = (v) => Ids.transportReceipt.create(v)
export const providerRun = (v) => Ids.providerRun.create(v)
export const toolCallId = (v) => Ids.toolCall.create(v)
export const hostToolPartId = (v) => Ids.hostToolPart.create(v)
export const systemPromptId = (v) => Ids.systemPrompt.create(v)
export const reviewBarrierId = (v) => Ids.reviewBarrier.create(v)
export const gitTreeHash = (v) => Ids.gitTree.create(v)
export const sealDigest = (v) => Ids.sealDigest.create(v)
export const agentHandleId = (v) => Ids.agentHandle.create(v)
export const ptyHandleId = (v) => Ids.ptyHandle.create(v)
export const managerJobId = (v) => Ids.managerJob.create(v)
export const worktreeIdentity = (v) => Ids.worktreeIdentity.create(v)
export const worktreePath = (v) => Ids.worktreePath.create(v)
export const targetRef = (v) => Ids.targetRef.create(v)
export const commitHash = (v) => Ids.commit.create(v)
export const blobRef = (v) => Ids.blobRef.create(v)
export const blobDigest = (v) => Ids.blobDigest.create(v)
export const bloggerRequestId = (v) => Ids.bloggerRequest.create(v)
export const managerLifeId = (v) => Ids.managerLife.create(v)
export const finalityRequestId = (v) => Ids.finalityRequest.create(v)

// HOST-013: Host transcript message address (raw `info.id` / `id`). A transcript
// position, not a user-only / authority / run identity.
export const transcriptAddress = {
  create: (value) => Identity.TranscriptMessageAddressModule_create(value),
  value: (id) => Identity.TranscriptMessageAddressModule_value(id),
}

/** HOST-013 TranscriptGap: Start | Before addr | After addr. */
const buildTranscriptGap = unionCase(Identity.TranscriptGap, 'TranscriptGap')
export const transcriptGap = {
  start: () => buildTranscriptGap('Start', []),
  before: (address) => buildTranscriptGap('Before', [address]),
  after: (address) => buildTranscriptGap('After', [address]),
}

// HOST-004: process-local idle admission token. Constructed only by the gate in
// production; tests construct one for decision-layer calls that take a wake.
export const quiescencePermit = {
  create: (session, serial) => Identity.QuiescencePermitModule_create(sessionId(session), BigInt(serial)),
  sessionId: (permit) => idValue.session(Identity.QuiescencePermitModule_sessionId(permit)),
  attemptSerial: (permit) => Number(Identity.QuiescencePermitModule_attemptSerial(permit)),
}

// Epoch ids wrap int64, so Fable represents them as BigInt. Taking a JS number
// here and converting once keeps `1` out of every call site — passing a plain
// number where F# expects int64 does not throw, it silently compares unequal.
export const frameEpochId = (value) => Identity.FrameEpochIdModule_create(BigInt(value))
export const prefixEpochId = (value) => Identity.PrefixEpochIdModule_create(BigInt(value))

export const localSeq = (value) => Identity.LocalSeqModule_create(BigInt(value))

export const journalRevision = {
  create: (value) => Identity.JournalRevisionModule_create(BigInt(value)),
  value: (rev) => Number(Identity.JournalRevisionModule_value(rev)),
  /** Prefer create(0): Fable may emit `initial` as a value or getter. */
  initial: () =>
    Identity.JournalRevisionModule_initial ??
    Identity.JournalRevisionModule_create(0n),
  next: (rev) => Identity.JournalRevisionModule_next(rev),
  isAfter: (a, b) => Identity.JournalRevisionModule_isAfter(a, b),
}

export const idValue = Object.fromEntries(
  Object.entries(Ids).map(([name, module]) => [name, module.value]),
)
idValue.localSeq = (id) => Identity.LocalSeqModule_value(id)
idValue.frameEpoch = (id) => Identity.FrameEpochIdModule_value(id)
idValue.prefixEpoch = (id) => Identity.PrefixEpochIdModule_value(id)
idValue.journalRevision = (id) => Number(Identity.JournalRevisionModule_value(id))

/** PROMPT-002: one-way promotion. There is deliberately no inverse. */
export const promoteToAuthorityRoot = (physical) => Identity.PhysicalUserMessageIdModule_promoteToAuthorityRoot(physical)

/** PROMPT-005: is this receipt `accepted-*` shaped. */
export const isAdmissionShaped = (receipt) => Identity.TransportReceiptModule_isAdmissionShaped(receipt)

const buildHandleId = unionCase(Identity.HandleId, 'HandleId')

export const handleId = {
  agent: (value) => buildHandleId('Agent', [agentHandleId(value)]),
  pty: (value) => buildHandleId('Pty', [ptyHandleId(value)]),
  managerJob: (value) => buildHandleId('ManagerJob', [managerJobId(value)]),
  describe: (handle) => Identity.HandleIdModule_describe(handle),
  tryAgent: (handle) => unwrapOption(Identity.HandleIdModule_tryAgent(handle)),
}

export const fallbackAttemptIdentity = {
  dedupeKey: (identity) => Identity.FallbackAttemptIdentityModule_dedupeKey(identity),
}

export const reviewAttemptIdentity = {
  dedupeKey: (identity) => Identity.ReviewAttemptIdentityModule_dedupeKey(identity),
  isDistinctAttempt: (a, b) => Identity.ReviewAttemptIdentityModule_isDistinctAttempt(a, b),
}