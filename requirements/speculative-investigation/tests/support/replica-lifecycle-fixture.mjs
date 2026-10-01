import * as Strength from '../../../../dist/Strength/Surface.js'
import * as Events from '../../../../dist/OpenCode/Host/EventsSurface.js'

export const ok = children => ({ ok: true, children })
export const error = message => ({ ok: false, error: message })

export function replicaFixture({ create, list, acquire, getMessages } = {}) {
  const owner = 'work-owner'
  const children = []
  const aborted = []
  const physicals = new Map()
  const events = Events.create()
  const handle = Strength.replicaPreparationCreate(owner, 'engineer', events, {
    create: async (_owner, options) => {
      if (create) await create()
      const sessionId = `replica-${children.length + 1}`
      children.push({ sessionId, agent: options.agent, title: options.title })
      return sessionId
    },
    list: async () => list ? list(children) : ok(children),
    abort: async replica => { aborted.push(replica) },
    acquireModel: acquire,
    getMessages: getMessages ? (async replica => getMessages(replica)) : undefined,
  })
  const prepare = decision => Strength.replicaPrepare(handle, Strength.runtimeBinding(
    owner, 'unprepared', decision, `target-${decision}`, 'engineer', 1, 'anchor', [],
  ))
  const admit = (replica, physical) => {
    physicals.set(replica, physical)
    return Strength.replicaHandleTransform(handle, { messages: [{
      info: { id: physical, role: 'user', sessionID: replica },
      parts: [{ type: 'text', text: 'readonly assignment' }],
    }] })
  }
  const observe = (replica, physical, providerRun) => Strength.replicaHandleTurn(handle, {
    sessionId: replica, physicalUserMessageId: physical, providerRun, outcome: 'completed', parts: [],
  })
  const terminal = replica => {
    const physical = physicals.get(replica)
    const run = `response-${physical}`
    observe(replica, physical, run)
    return Events.notify(events, replica, 'Completed', run, 'done')
  }
  return {
    children, aborted, owner, prepare, admit, observe, terminal,
    notifyCompleted: (replica, run) => Events.notify(events, replica, 'Completed', run, 'done'),
    awaitOutcome: Strength.replicaAwaitOutcome,
    isReplica: replica => Strength.replicaIsReplica(handle, replica),
    tryOutcome: (replica, decision) => Strength.replicaDecisionOutcome(handle, replica, decision),
    releaseOutcome: decision => Strength.replicaReleaseDecisionOutcome(handle, decision),
    sendPrepared: replica => Strength.replicaSendPrepared(handle, replica),
    cancelOwner: () => Strength.replicaCancelOwner(handle, owner),
    dispose: () => Strength.replicaDispose(handle),
  }
}
