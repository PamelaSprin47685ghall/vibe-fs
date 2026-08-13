/**
 * Append named gate facts into the unified EventStore tip for VERIFY-004 waitFact cases.
 *
 * After G4 Phase 5, `journal-observer.js` watches `refs/wanxiang/store` only (leave-unread
 * NDJSON). Harness scripts that used to `appendFileSync` under wanxiangshu-next must plant
 * EventStore events whose canonical JSON contains the fact name string.
 */

import { eventId, toList } from '../../unit/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const ProcessGit = await import('../../../dist/Infrastructure/Persist/ProcessGitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)

/** 40-char hex EventId from a monotonic counter (stable, collision-free in one tip). */
export const hexId = (n) => n.toString(16).padStart(40, '0')

/**
 * @param {string} workDir git work tree (already `git init`)
 * @returns {{ appendNamedFact: (name: string, n: number) => Promise<void>, count: () => number }}
 */
export function openGateFactStore(workDir) {
  const raw = ProcessGit.ProcessGitRawStoreModule_create(workDir)
  const es = Store.EventStore_create(raw)
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
      const result = await es.Append(await es.Refresh(), toList([envelope]))
      if (result.tag !== 0) {
        throw new Error(`EventStore.Append(${name}) failed: ${JSON.stringify(result)}`)
      }
      lastId = id
    })
    serial = run
    return run
  }

  return { appendNamedFact, count: () => seq }
}
