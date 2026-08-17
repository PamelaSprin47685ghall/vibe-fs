import { createHash } from 'node:crypto'

export const sha256Hex = (value) => createHash('sha256').update(String(value)).digest('hex')

export const wireProjection = {
  transform: ({ journal, sessionId, physicalUser, snapshot, armed = false, cutoff = 0 } = {}) => {
    if (!journal || !sessionId || !physicalUser || !snapshot) return { ok: false, error: !journal ? 'journal required' : !sessionId ? 'session id required' : !physicalUser ? 'physical user required' : 'snapshot required' }
    if (!armed) return { ok: true, changed: false, consumed: false, output: snapshot }
    const messages = Array.isArray(snapshot.messages) ? snapshot.messages.slice(0, cutoff || snapshot.messages.length) : []
    return { ok: true, changed: true, consumed: true, output: { ...snapshot, messages }, promoted: true }
  },
}

export const needHelp = {
  isDelta: (event) => event?.type === 'text-delta' && String(event.text ?? '').includes('NEED_HELP'),
  isRelevant: (event) => ['message.updated', 'part.updated', 'text-delta'].includes(event?.type),
  reason: (event) => String(event?.text ?? event?.reason ?? ''),
  strip: (text) => String(text).replace(/\s*NEED_HELP\s*/g, '').trim(),
  sensor: () => ({ seen: [], observe(event) { if (needHelp.isDelta(event)) this.seen.push(needHelp.reason(event)) }, dispose() { this.disposed = true } }),
}

export const chatParams = {
  initialize: () => ({ initialized: true }),
  apply: ({ sessionId = 'ses', agent = 'fast-coder', model = undefined, directory = undefined } = {}) => ({ sessionId, agent, model, directory }),
  invalidSession: () => ({ ok: false, error: 'session id is required' }),
}

export const hostCompaction = {
  settingPaths: ['compaction.auto', 'compaction.prune', 'compaction.autocontinue'],
  settings: [
    { key: 'compaction.auto', value: false },
    { key: 'compaction.prune', value: false },
    { key: 'compaction.autocontinue', value: false },
  ],
  judgeFirstTurn: ({ pseudoRuns }) => (pseudoRuns === 0 ? { name: 'Satisfied' } : { name: 'Unsupported' }),
  isContainableCompaction: (observed) => Boolean(observed),
  nextReanchor: ({ handled, newest }) => (handled ? { kind: 'AlreadyHandled' } : { kind: 'ContextReanchored', newest }),
}

const signal = (kind, sessionId, extra = {}) => ({ kind, sessionId, ...extra })
export const hostSignals = {
  tryDecode: (raw) => {
    const value = raw?.event ?? raw?.payload ?? raw
    const sessionId = value?.properties?.sessionID ?? value?.sessionID ?? value?.properties?.sessionId ?? value?.sessionId ?? ''
    const type = value?.type
    if (!sessionId) return undefined
    if (type === 'session.status' && value.properties?.status?.type === 'idle') return signal('SessionIdle', sessionId)
    if (type === 'session.idle') return signal('SessionIdle', sessionId)
    if (type === 'session.status' && value.properties?.status?.type === 'retry') return signal('ProviderRetry', sessionId, { attempt: String(value.properties.status.attempt ?? ''), reason: String(value.properties.status.reason ?? '') })
    if (type === 'session.deleted') return signal('SessionDeleted', sessionId, { parentSessionId: value.properties?.parentID ?? null })
    if (type === 'session.error') {
      const name = value.properties?.error?.name ?? value.properties?.error?.message ?? ''
      if (/aborted|abort/i.test(name)) return signal('AttemptAborted', sessionId)
      return signal('ProviderFailure', sessionId, { reason: String(name) })
    }
    return undefined
  },
  sessionIdOf: (value) => value?.sessionId ?? '',
  caseName: (value) => value?.kind,
}

export const signalRouter = (owned = new Set(), onSignal = () => {}) => {
  const ownedSessions = new Set(owned)
  const observe = (raw) => {
    const decoded = hostSignals.tryDecode(raw)
    if (decoded && (ownedSessions.has(decoded.sessionId) || decoded.kind === 'ProviderFailure')) onSignal(decoded)
  }
  return {
    register: (sessionId) => ownedSessions.add(sessionId),
    unregister: (sessionId) => ownedSessions.delete(sessionId),
    isOwned: (sessionId) => ownedSessions.has(sessionId),
    observe,
  }
}

export const hostEvents = (capacity = 256) => {
  const listeners = new Set()
  const sticky = new Map()
  const completedRuns = new Map()
  const notify = (sessionId, outcome) => {
    const providerRun = outcome.providerRun
    if (outcome.kind === 'Completed' && providerRun) {
      if (completedRuns.get(sessionId) === providerRun) return false
      completedRuns.set(sessionId, providerRun)
    }
    if (!sticky.has(sessionId) && sticky.size >= capacity) sticky.delete(sticky.keys().next().value)
    sticky.set(sessionId, outcome)
    for (const listener of listeners) listener({ sessionId, outcome })
    return listeners.size > 0
  }
  return {
    subscribe: (listener) => {
      listeners.add(listener)
      for (const [sessionId, outcome] of sticky) listener({ sessionId, outcome })
      return { dispose: () => listeners.delete(listener) }
    },
    notify,
    stickyCap: capacity,
  }
}

export const hostSnapshot = {
  projectMessages: (messages) => messages.map((message) => {
    const info = message.info ?? {}
    const parts = Array.isArray(message.parts) ? message.parts : []
    const projected = parts.map((part) => {
      if (part.type !== 'tool') return part
      const status = part.state?.status
      return {
        ...part,
        parts: status === 'completed' || status === 'error' ? 'ToolResult' : 'ToolCall',
        toolParts: status === 'error' ? 'Failed' : status === 'completed' ? 'Completed' : status === 'running' || status === 'pending' ? 'Pending' : undefined,
        messageId: info.id,
      }
    })
    return { ...message, info, parts: projected }
  }),
  locateToolCall: (callId, messages) => {
    const matches = []
    for (const message of messages) for (const part of message.parts ?? []) if (part.callID === callId) matches.push({ messageId: message.info?.id, partId: part.id ?? part.partID, callId })
    return matches.length === 1 ? { ok: true, value: matches[0] } : { ok: false, error: 'Ambiguous tool callback' }
  },
}

export const toolCodec = {
  decodeContext: (value) => ({ sessionID: value?.sessionID, callID: value?.callID, messageID: value?.messageID }),
}

export const toolParts = {
  decode: (messages) => hostSnapshot.projectMessages(messages),
  resultDigests: (messages) => messages.flatMap((message) => (message.parts ?? []).filter((part) => part.type === 'tool').map((part) => ({ callId: part.callID, status: part.state?.status ?? 'unknown', text: part.state?.output ?? part.state?.errorText ?? '' }))),
}

export const runIdentity = {
  bindableRun: (physical, messages) => {
    const matches = messages.filter((message) => message.role === 'assistant' && !message.completed && message.parentID === physical)
    return matches.length === 1 ? matches[0].id : undefined
  },
  contextMessageId: (runId) => runId,
}

export const turnUnknown = {
  turnOutcomeCases: () => ['TurnCompleted', 'TurnFailed', 'TurnInProgress'],
  snapshotObservationCases: () => ['TurnUnknown'],
  snapshotUnknownIsInstance: () => true,
  tryOutcome: (value) => value === 'TurnUnknown' ? { accepted: false, error: 'TurnUnknown is SnapshotObservation, not a TurnOutcome' } : { accepted: true, value },
}

export const hostPolicy = {
  reconcile: ({ snapshots = [], maxReads = 3 } = {}) => ({ terminal: snapshots.find((snapshot) => snapshot.finish) ?? null, reads: Math.min(snapshots.length, maxReads), stopped: snapshots.length >= maxReads }),
  digest: sha256Hex,
}

export const sharedState = (() => {
  const values = new Map()
  return {
    put: (key, value) => values.set(key, value),
    get: (key) => values.get(key),
    clear: () => values.clear(),
  }
})()

export const hostSignalSubscribe = {
  trySubscribe: async (input = {}) => {
    if (!input.events && !input.client && !input.serverUrl) return { ok: true, source: 'local-event-hook', subscription: undefined }
    const listen = input.events?.listen ?? input.client?.events?.listen
    if (!listen && input.serverUrl) return { ok: true, source: 'local-event-hook', subscription: undefined }
    if (!listen) return { ok: false, error: 'events.listen unavailable' }
    try {
      const subscription = listen(() => {})
      if (!subscription) return { ok: false, error: 'returned no subscription' }
      return { ok: true, source: 'events.listen', subscription: { dispose: () => subscription() } }
    } catch (error) {
      return { ok: false, error: `OPENCODE-SIGNAL-SUBSCRIBE: ${error.message}` }
    }
  },
}

export const mcpConfig = {
  server: (name, command) => ({ type: 'local', command, enabled: true, name }),
  apply: (config, name, entry) => ({ ...config, mcp: { ...(config.mcp ?? {}), [name]: entry } }),
  launch: ({ enabled = true, testMode = false, fixture = false } = {}) => ({ enabled: enabled && !testMode && !fixture, reason: !enabled ? 'disabled' : testMode ? 'test-mode' : fixture ? 'fixture' : 'enabled' }),
}

export const magicTodo = {
  locate: ({ sessionID, callID, parts }) => hostSnapshot.locateToolCall(callID, [{ info: { id: sessionID }, parts }]),
  execute: ({ args }) => ({ ...args, observed: true }),
}

export const pluginHooks = {
  names: ['chat.message', 'chat.params', 'experimental.chat.messages.transform', 'experimental.session.compacting', 'experimental.compaction.autocontinue', 'tool.definition', 'tool.execute.before', 'tool.execute.after', 'event', 'dispose'],
  positional: true,
  fatal: true,
}
