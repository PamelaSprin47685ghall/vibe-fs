// Transitional mirrors. M7C removes satellite/distiller; M7D–M7E remove the rest.

export const satelliteLifecycle = ({ linked = true, physical = true, conflict = false, queryError = false } = {}) => {
  if (queryError) return { ok: false, error: 'Cannot recover companion satellite: children unavailable', origin: undefined, created: [], closed: [] }
  if (conflict) return { ok: false, error: 'Conflicting companion satellite recovery', origin: undefined, created: [], closed: [] }
  if (linked && physical) return { ok: true, origin: 'Reused', child: 'blogger-1', created: [], closed: [], linked: [['work', 'blogger-1', 'fast-blogger']] }
  if (linked && !physical) return { ok: true, origin: 'Replacement', child: 'created-1', created: ['created-1'], closed: ['work'], linked: [['work', 'created-1', 'fast-blogger']] }
  return { ok: true, origin: 'Created', child: 'created-1', created: ['created-1'], closed: [], linked: [['work', 'created-1', 'fast-blogger']] }
}

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
