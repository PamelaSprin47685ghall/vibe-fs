/**
 * Append named VERIFY-004 gate facts into the local process EventStore.
 * Shock-cut model: `.git/wanxiang/events/<WriterId>.ndjson`; no Git ODB/ref append.
 */

import { join } from 'node:path'
import { eventId } from '../../support/domain/identity.mjs'
import { toList } from '../../support/domain/interop.mjs'
import { createLocalEventStore } from '../../support/local-event-store.mjs'

const Domain = await import('../../../../../dist/Domain/EventStore.js')
const streamId = (v) => Domain.EventStreamIdModule_create(v)

export const hexId = (n) => n.toString(16).padStart(40, '0')

export function openGateFactStore(workDir) {
  const local = createLocalEventStore({ commonDir: join(workDir, '.git'), writerId: `verification-gate-${process.pid}` })
  const es = local.store
  let seq = 0
  let lastId = null
  let serial = Promise.resolve()

  const appendNamedFact = (name, n) => {
    const run = serial.then(async () => {
      seq += 1
      const id = hexId(seq)
      const parents = lastId === null ? [] : [eventId(lastId)]
      const envelope = new Domain.EventEnvelope(
        eventId(id),
        streamId('gate/wait-fact'),
        'JobRequested',
        toList(parents),
        { type: name, n },
        toList([]),
      )
      const result = await es.Append(toList([envelope]))
      if (result.tag !== 0) throw new Error(`EventStore.Append(${name}) failed: ${JSON.stringify(result)}`)
      lastId = id
    })
    serial = run
    return run
  }

  return { appendNamedFact, count: () => seq }
}
