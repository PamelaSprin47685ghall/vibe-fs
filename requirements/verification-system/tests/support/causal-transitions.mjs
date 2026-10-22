// tests/support/causal-transitions.mjs — monitored-target progress that is not a verdict.
//
// verification-system-006 asks for 「距最近一次因果进展的静默时长」 and forbids renewing on
// 「有字节在动」 and on 「同一进展的重复观察」. Besides a verdict, one thing advances a
// supervised run: the monitored work demonstrably moved from one state to the next.
//
// A transition is admitted only when it is (a) about a planned unit of work, (b) a real
// cross-phase event, and (c) not seen before. Everything else — an unknown file, an
// out-of-order phase, a repeated observation, byte traffic, a worker measurement — is not
// progress and cannot renew the window.
//
// Exact meanings, so nothing claims more than it observed:
//
//   runner:started       the inner runner accepted this exact plan and began executing it
//   runner:file-start    that planned file's work was submitted to the runner
//   runner:file-drained  that planned file's result stream drained
//
// `test:start` is deliberately NOT progress: node:test reports it when a leaf is about to
// run, and that does not prove the leaf body was entered — the leaf may still be waiting on
// its first await. Claiming it as progress would be a heartbeat in disguise.

import { resolve } from 'node:path'

function plannedFile(event) {
  const file = event?.data?.entryFile
  if (typeof file !== 'string' || file.length === 0) return null
  return resolve(file)
}

/**
 * A tracker that admits each validated transition once.
 *
 * @param {{ plannedFiles: Iterable<string> }} options
 */
export function createCausalTransitionTracker({ plannedFiles = [], expectedPid = null } = {}) {
  const planned = new Set([...plannedFiles].map(file => resolve(file)))
  const submitted = new Set()
  const drained = new Set()
  let started = false
  let childPid = expectedPid

  return {
    expectedPid(pid) { childPid = pid },
    observe(event) {
      const type = event?.type
      if (type === 'runner:started') {
        if (started) return null
        if (childPid !== null && event?.data?.pid !== childPid) return null
        const announced = event?.data?.files
        if (!Array.isArray(announced)) return null
        const resolved = announced.map(file => resolve(file))
        if (resolved.length !== planned.size) return null
        const announcedSet = new Set(resolved)
        if (announcedSet.size !== planned.size) return null
        for (const file of announcedSet) if (!planned.has(file)) return null
        started = true
        return { key: 'runner:started', reason: 'runner:started', lane: 'runner' }
      }
      if (type === 'runner:file-start') {
        const file = plannedFile(event)
        if (file === null || !planned.has(file) || submitted.has(file)) return null
        submitted.add(file)
        return { key: `file-submitted:${file}`, reason: 'runner:file-start', lane: file }
      }
      if (type === 'runner:file-drained') {
        const file = plannedFile(event)
        if (file === null || !planned.has(file) || !submitted.has(file) || drained.has(file)) return null
        drained.add(file)
        return { key: `file-drained:${file}`, reason: 'runner:file-drained', lane: file }
      }
      return null
    },
    size() { return submitted.size + drained.size },
    submitted() { return [...submitted] },
    drained() { return [...drained] },
  }
}
