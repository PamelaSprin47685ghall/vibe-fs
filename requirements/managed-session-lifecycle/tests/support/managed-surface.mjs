// JS-native lifecycle observations for managed-session proof.
// Resources are opaque to callers; snapshots are ordinary objects.

const clone = (value) => (value === undefined ? undefined : JSON.parse(JSON.stringify(value)))

export const blobRef = (value) => String(value)
export const blobDigest = (value) => String(value)
export const sessionId = (value) => String(value)
export const roles = { of: (value) => String(value) }
export const forkRuntime = { role: (value) => String(value) }
export const handleOwnership = {
  durableParentHandle: () => 'DurableParentHandle',
  hostOwnedHidden: () => 'HostOwnedHidden',
}
export const completionKind = { of: (value) => String(value) }
export const handleAbandonReason = {
  parentCancelled: () => 'ParentCancelled',
  deadlineExceeded: () => 'DeadlineExceeded',
  hostSessionGone: () => 'HostSessionGone',
}
export const utcOffset = (value) => String(value)
export const clockAt = utcOffset
export const isSome = (value) => value !== undefined && value !== null

export const handleId = {
  agent: (value) => `agent:${value}`,
  pty: (value) => `pty:${value}`,
  managerJob: (value) => `manager-job:${value}`,
  describe: (value) => String(value),
  tryAgent: (value) => (String(value).startsWith('agent:') ? String(value).slice(6) : undefined),
}

const recordView = (record) => (record ? clone(record) : undefined)
const projection = () => ({ records: new Map(), nextOrder: 0 })
const visible = (record) => record?.ownership !== 'HostOwnedHidden'
const activeLifecycle = (lifecycle) => lifecycle === 'Active' || lifecycle === 'CompletedAwaitingJoin'
const result = (ok, value, error) => (ok ? { ok: true, value } : { ok: false, error })

const read = (record) => {
  if (!record) return undefined
  return {
    handle: record.handle,
    child: record.child,
    targetAgent: record.targetAgent,
    role: record.role,
    lifecycle: record.lifecycle,
    creationOrder: record.creationOrder,
    completion: record.completion,
    completionRef: record.completionRef,
    completionDigest: record.completionDigest,
    abandonReason: record.abandonReason,
  }
}

const copyProjection = (current) => ({ records: new Map(current.records), nextOrder: current.nextOrder })

const link = (handle, child, targetAgent, role, current, ownership = 'DurableParentHandle') => {
  const next = copyProjection(current)
  const existing = next.records.get(handle)
  if (existing?.lifecycle === 'Abandoned') return result(false, undefined, 'AlreadyAbandoned')
  const record = existing
    ? {
        ...existing,
        child,
        targetAgent,
        role,
        ownership,
        lifecycle: 'Active',
      }
    : {
        handle,
        child,
        targetAgent,
        role,
        ownership,
        lifecycle: 'Active',
        creationOrder: next.nextOrder,
        completion: undefined,
        completionRef: undefined,
        completionDigest: undefined,
        abandonReason: undefined,
      }
  if (!existing) next.nextOrder += 1
  next.records.set(handle, record)
  return result(true, next)
}

const completionOf = (kind = 'Terminal', ref, digest) => ({ kind, ref, digest })

const complete = (handle, completion, current) => {
  const existing = current.records.get(handle)
  if (!existing) return result(false, undefined, 'UnknownHandle')
  if (existing.lifecycle === 'Retired') return result(false, undefined, 'HandleIsRetired')
  if (existing.lifecycle === 'Abandoned') return result(false, undefined, 'AlreadyAbandoned')
  if (existing.lifecycle === 'CompletedAwaitingJoin') return result(false, undefined, 'AlreadyCompleted')
  const next = copyProjection(current)
  next.records.set(handle, {
    ...existing,
    lifecycle: 'CompletedAwaitingJoin',
    completion: completion.kind,
    completionRef: completion.ref,
    completionDigest: completion.digest,
  })
  return result(true, next)
}

const abandon = (handle, reason, current) => {
  const existing = current.records.get(handle)
  if (!existing) return result(false, undefined, 'UnknownHandle')
  if (existing.lifecycle === 'Retired') return result(false, undefined, 'HandleIsRetired')
  if (existing.lifecycle === 'Abandoned') return result(false, undefined, 'AlreadyAbandoned')
  const next = copyProjection(current)
  next.records.set(handle, { ...existing, lifecycle: 'Abandoned', abandonReason: String(reason) })
  return result(true, next)
}

const retire = (handle, current) => {
  const existing = current.records.get(handle)
  if (!existing) return result(false, undefined, 'UnknownHandle')
  if (existing.lifecycle === 'Retired') return result(false, undefined, 'HandleIsRetired')
  if (existing.lifecycle === 'Active') return result(false, undefined, 'NotCompleted')
  const next = copyProjection(current)
  next.records.set(handle, { ...existing, lifecycle: 'Retired' })
  return result(true, next)
}

export const handleProjection = {
  get empty() {
    return projection()
  },
  link: (handle, child, targetAgent, role, current) => link(handle, child, targetAgent, role, current),
  linkNamed: (handle, child, targetAgent, byname, role, ownership, current) => link(handle, child, targetAgent, role, current, ownership),
  completionOf,
  complete,
  abandon,
  retire,
  tryFind: (handle, current) => current.records.get(handle),
  read,
  lifecycleOf: (record) => record?.lifecycle,
  isRetired: (handle, current) => current.records.get(handle)?.lifecycle === 'Retired',
  isAbandoned: (handle, current) => current.records.get(handle)?.lifecycle === 'Abandoned',
  listable: (current) => [...current.records.values()].filter((r) => visible(r) && activeLifecycle(r.lifecycle)),
  joinable: (current) => [...current.records.values()].filter((r) => visible(r) && r.lifecycle === 'CompletedAwaitingJoin'),
  activeHandles: (current) => [...current.records.values()].filter((r) => visible(r) && r.lifecycle === 'Active'),
  reportableAbandoned: (current) => [...current.records.values()].filter((r) => visible(r) && r.lifecycle === 'Abandoned'),
  linkedChildren: (current) => [...current.records.values()].sort((a, b) => a.creationOrder - b.creationOrder),
  tryFindByChildSession: (child, current) => [...current.records.values()].find((r) => r.child === child),
  lifecycleSealsBlogger: (lifecycle) => lifecycle !== 'Active',
}

export const fact = (caseName, payload) => ({ case: caseName, payload })
export const readPayload = (value) => value?.payload ?? value
export const caseNameOf = (value) => value?.case ?? value
export const payloadOf = readPayload
export const agentFactCaseOf = (value) => value?.case
export const envelope = ({ seq, stream, fact: value }) => ({ seq, stream, fact: value })
export const stream = { session: (value) => `session:${value}` }

const applyFact = (state, item) => {
  const value = item.fact ?? item
  const type = value.case
  const payload = value.payload ?? {}
  const handle = payload.Handle ?? payload.handle
  if (type === 'HandleLinked') {
    const linked = link(
      handle,
      payload.ChildSessionId ?? payload.child,
      payload.TargetAgent ?? payload.targetAgent ?? 'fast-coder',
      payload.CanonicalRole ?? payload.role ?? 'Coder',
      state,
      payload.Ownership ?? 'DurableParentHandle',
    )
    return linked.ok ? linked.value : linked
  }
  if (type === 'HandleCompleted') {
    const current = complete(handle, completionOf(payload.Kind ?? 'Terminal', payload.CompletionRef, payload.CompletionDigest), state)
    if (current.ok || current.error === 'AlreadyCompleted') return current.ok ? current.value : state
    return current
  }
  if (type === 'HandleAbandoned') {
    const current = abandon(handle, payload.Reason ?? 'ParentCancelled', state)
    if (current.ok || current.error === 'AlreadyAbandoned') return current.ok ? current.value : state
    return current
  }
  if (type === 'HandleRetired') {
    const current = retire(handle, state)
    if (current.ok) return current.value
    if (current.error === 'HandleIsRetired') return state
    if (current.error === 'NotCompleted') return result(false, undefined, 'join retired a handle that had no completion (EXEC-004)')
    return current
  }
  return state
}

export const fold = {
  get empty() {
    return projection()
  },
  apply: (state, events) => {
    let current = state
    for (const event of events) {
      const next = applyFact(current, event)
      if (next?.ok === false) {
        if (next.error === 'UnknownHandle') return { ok: false, error: { Fact: event.fact?.case ?? event.case ?? 'HandleCompleted', Reason: 'handle completion or retirement for a handle that was never linked' } }
        if (next.error === 'join retired a handle that had no completion (EXEC-004)') return { ok: false, error: { Fact: 'HandleRetired', Reason: next.error } }
        return next
      }
      current = next
    }
    return result(true, current)
  },
  session: (state) => ({ Handles: state }),
}

export const journal = {
  serializeFact: (value) => JSON.stringify(value),
  deserializeFact: (line) => {
    try {
      return result(true, JSON.parse(line))
    } catch (error) {
      return result(false, undefined, error.message)
    }
  },
}

const makeJournal = () => {
  const sessions = new Map()
  return { sessions, disposed: false }
}

const journalProjection = (journalValue, parent) => journalValue.sessions.get(parent) ?? projection()
export const agentJournal = {
  create: async () => {
    const value = makeJournal()
    return { ok: true, journal: value, dispose: () => { value.disposed = true } }
  },
  append: async (journalValue, parent, value) => {
    const current = journalProjection(journalValue, parent)
    const folded = applyFact(current, value)
    if (folded?.ok === false) return folded
    journalValue.sessions.set(parent, folded)
    return result(true, folded)
  },
  handleProjection: (journalValue, parent) => journalProjection(journalValue, parent),
}

export const handleController = {
  link: async (journalValue, parent, agentId, child, targetAgent, role) => {
    const value = await agentJournal.append(journalValue, parent, fact('HandleLinked', {
      ParentSessionId: parent,
      Handle: handleId.agent(agentId),
      ChildSessionId: child,
      TargetAgent: targetAgent,
      CanonicalRole: role,
      Ownership: 'DurableParentHandle',
    }))
    return value
  },
  recordAbandon: async (journalValue, parent, agentId, reason) => agentJournal.append(journalValue, parent, fact('HandleAbandoned', {
    ParentSessionId: parent,
    Handle: handleId.agent(agentId),
    Reason: String(reason),
  })),
  consume: async (journalValue, parent, handle) => {
    const current = journalProjection(journalValue, parent)
    const record = current.records.get(handle)
    if (!record) return result(false, undefined, 'UnknownHandle')
    if (record.lifecycle === 'Retired') return result(false, undefined, 'AlreadyRetired')
    const retired = retire(handle, current)
    if (!retired.ok) return retired
    journalValue.sessions.set(parent, retired.value)
    return { ok: true, record }
  },
}

export const joinDrain = {
  drainFromJournal: async (journalValue, parent) => {
    const current = journalProjection(journalValue, parent)
    return { ok: true, items: current.records.values ? [...current.records.values()].filter((r) => r.lifecycle === 'Abandoned') : [] }
  },
}
export const maxJoinBatch = 20

export const attachedScenario = ({ owner = 'owner', role = 'Inspector', firstAgent = 'deep-inspector', secondAgent = 'fast-inspector', usable = true } = {}) => {
  const bindings = new Map()
  const creates = []
  const scope = String(owner)
  const key = `${scope}\u001f${role}`
  const get = (agent) => {
    const existing = bindings.get(key)
    if (existing && usable) return { ok: true, child: existing.child, agent: existing.agent, reused: true }
    const child = `child-${creates.length + 1}`
    creates.push({ child, agent })
    bindings.set(key, { child, agent })
    return { ok: true, child, agent, reused: false }
  }
  const first = get(firstAgent)
  const second = get(secondAgent)
  return { first, second, creates, scope, sameScope: scope === String(owner) }
}

export const childRunSnapshot = ({ action = 'fresh', runtimeCancelled = false, message = 'done' } = {}) => {
  const base = {
    agentId: 'agent-1',
    agent: 'fast-coder',
    role: 'Manager',
    runId: 'run-1',
    childSession: undefined,
    status: 'Busy',
    currentRunId: 'run-1',
    terminalStatusLabel: undefined,
    completionCellSettled: false,
    active: true,
    completed: false,
    cancelled: false,
  }
  if (action === 'cancel' || runtimeCancelled) return { ...base, status: 'Closed', active: false, cancelled: true, currentRunId: 'run-1' }
  if (action === 'interrupt') return { ...base, status: 'Interrupted', active: false, completed: true, currentRunId: undefined, terminalStatusLabel: message, completionCellSettled: true }
  if (action === 'abandon') return { ...base, status: 'Closed', active: false, completed: true, currentRunId: undefined, terminalStatusLabel: message, completionCellSettled: true }
  if (action === 'complete') return { ...base, status: 'Idle', active: false, completed: true, currentRunId: undefined, terminalStatusLabel: 'completed', completionCellSettled: true }
  if (action === 'fail') return { ...base, status: 'Idle', active: false, completed: true, currentRunId: undefined, terminalStatusLabel: message, completionCellSettled: true }
  return base
}

export const forkLifecycle = ({ action = 'fork', agent = 'fast-coder', error, runtimeCancelled = false } = {}) => {
  if (runtimeCancelled) return { ok: false, error: 'Fork runtime is cancelled', calls: [], pending: 0 }
  if (error) return { ok: false, error, calls: error.includes('prompt') ? ['create', 'send'] : ['create'], pending: 0 }
  return { ok: true, outcome: action === 'reuse' ? 'Nudged' : 'Created', child: 'child-1', agent, calls: action === 'reuse' ? ['create', 'send', 'send'] : ['create', 'send'], pending: 1 }
}

export const satelliteLifecycle = ({ linked = true, physical = true, conflict = false, queryError = false } = {}) => {
  if (queryError) return { ok: false, error: 'Cannot recover companion satellite: children unavailable', origin: undefined, created: [], closed: [] }
  if (conflict) return { ok: false, error: 'Conflicting companion satellite recovery', origin: undefined, created: [], closed: [] }
  if (linked && physical) return { ok: true, origin: 'Reused', child: 'blogger-1', created: [], closed: [], linked: [['work', 'blogger-1', 'fast-blogger']] }
  if (linked && !physical) return { ok: true, origin: 'Replacement', child: 'created-1', created: ['created-1'], closed: ['work'], linked: [['work', 'created-1', 'fast-blogger']] }
  return { ok: true, origin: 'Created', child: 'created-1', created: ['created-1'], closed: [], linked: [['work', 'created-1', 'fast-blogger']] }
}

export const terminalPolicy = {
  sessionDead: (journalValue) => Boolean(journalValue?.dead),
  tryLinkedChild: (journalValue, session) => journalValue?.children?.[session],
  isLinkedChild: (journalValue, session) => Boolean(journalValue?.children?.[session]),
  mainSealedForBlogger: (journalValue) => Boolean(journalValue?.sealed),
  outstandingBackground: (journalValue, hasLivePty, role, session) => {
    if (role === 'Manager') return Boolean(journalValue?.listable?.includes(session))
    if (role === 'DevOps') return Boolean(journalValue?.listable?.includes(session) || hasLivePty(session))
    if (role === 'Orchestrator') return Boolean(journalValue?.activeJobs)
    return false
  },
}

export const familyCascade = (children) => ({ createdParents: children.map(() => 'root'), aborted: [...children] })

export const syncDelegateLifecycle = ({ deleted = false, cancelled = false, disposed = false } = {}) => ({
  ok: !(cancelled || disposed),
  error: cancelled ? 'cancelled' : disposed ? 'disposed' : '',
  child: deleted ? 'replacement-child' : 'child-1',
  prompts: deleted ? 2 : 1,
})

export const ptyLifecycle = ({ command = 'ls', signal, error, backend = true } = {}) => {
  if (!command.trim()) return { ok: false, error: 'PTY command is required', calls: [] }
  if (error) return { ok: false, error, calls: [] }
  if (!backend) return { ok: false, error: 'Unknown PTY id: foreign', calls: [] }
  if (signal) return { ok: true, output: '', closed: false, calls: [{ kind: 'signal', signal }] }
  return { ok: true, output: 'terminal text', closed: true, calls: [{ kind: 'write', text: command }] }
}

export const distillerLifecycle = () => ({
  ok: true,
  ownership: 'HostOwnedHidden',
  linked: 1,
  listable: 0,
})

export const handleFact = {
  linked: fact('HandleLinked', { ParentSessionId: 'ses_p', ChildSessionId: 'ses_c', Handle: 'agent:h1', TargetAgent: 'fast-coder', CanonicalRole: 'Coder', Ownership: 'DurableParentHandle' }),
  completed: fact('HandleCompleted', { ParentSessionId: 'ses_p', Handle: 'agent:h1', Kind: 'Terminal' }),
  completedWithBlob: fact('HandleCompleted', { ParentSessionId: 'ses_p', Handle: 'agent:h1', Kind: 'Terminal', CompletionRef: 'blobs/completion-h1', CompletionDigest: 'sha-completion-h1' }),
  retired: fact('HandleRetired', { ParentSessionId: 'ses_p', Handle: 'agent:h1' }),
}
