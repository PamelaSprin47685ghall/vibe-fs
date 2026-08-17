import { createHash } from 'node:crypto'
import * as HostSignalSurface from '../../../../dist/OpenCode/Host/HostSignalSurface.js'

const sha256Hex = (value) => createHash('sha256').update(String(value)).digest('hex')

export const signalRouter = (owned = new Set(), onSignal = () => {}) => {
  const ownedSessions = new Set(owned)
  const observe = (raw) => {
    const decoded = HostSignalSurface.tryDecode(raw) ?? undefined
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

export const pluginHooks = {
  names: ['chat.message', 'chat.params', 'experimental.chat.messages.transform', 'experimental.session.compacting', 'experimental.compaction.autocontinue', 'tool.definition', 'tool.execute.before', 'tool.execute.after', 'event', 'dispose'],
  positional: true,
  fatal: true,
}
