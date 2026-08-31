// Transitional mirrors. M7D–M7E remove the remaining SyncDelegate and PTY paths.

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
