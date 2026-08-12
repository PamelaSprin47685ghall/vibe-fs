import { randomUUID } from 'node:crypto'
import { startInquiry, resumeInquiry } from './kernel/inquire.js'

export function createSessionStore() {
  const sessions = new Map()

  function start(question) {
    const { state, result } = startInquiry(question)
    if (result.status === 'error') {
      return { status: 'error', error: result.error }
    }
    const handle = randomUUID()
    sessions.set(handle, state)
    return { handle, ...result }
  }

  function resume(handle, observation) {
    if (handle == null || handle === '') {
      return { status: 'error', error: 'missing handle' }
    }
    if (!sessions.has(handle)) {
      return { handle, status: 'error', error: 'unknown handle' }
    }
    const state = sessions.get(handle)
    const { state: next, result } = resumeInquiry(state, observation)
    if (result.status !== 'error') sessions.set(handle, next)
    return { handle, ...result }
  }

  return { start, resume, sessions }
}

export const defaultStore = createSessionStore()
export const start = (question) => defaultStore.start(question)
export const resume = (handle, observation) => defaultStore.resume(handle, observation)
