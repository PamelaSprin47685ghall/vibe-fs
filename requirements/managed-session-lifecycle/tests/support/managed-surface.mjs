// Transitional JS lifecycle observations. Each remaining export is removed by
// M7B–M7E when its consumer moves to the corresponding production owner.

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
