// tests/support/tool-lifecycle.mjs — the monitored child's REAL lifecycle, as the host observes it.
//
// verification-system-006: 「因果进展必须有受监测目标实际推进的证据，无关活动及同一进展的重复观察不得续期」.
//
// The evidence here is the lifecycle of a real child process, observed by the party that owns it:
//
//   spawned        the child's process really exists (its pid was confirmed alive)
//   exited         the child terminated on its own (with its own exit code / signal)
//   group-drained  the child's process GROUP is empty — not merely that the child exited
//
// The three are ordered and each is admitted once per child. A repeated or out-of-order stage is
// not progress. Unknown stages are rejected rather than treated as progress.
//
// The ladder alone would let an endless loop of children keep a case alive forever, so each
// monitored leaf has a renewal budget: the window may be extended only while a REAL transition is
// advancing, and the silence budget can still end the case.

import { resolve } from 'node:path'

export const TOOL_LIFECYCLE_STAGES = ['spawned', 'exited', 'group-drained']

export const TOOL_LIFECYCLE_PREFIX = '[verification-tool-lifecycle] '

/**
 * The monotonic ladder plus the per-leaf renewal budget.
 *
 * @param {{ maxRenewalsPerLeaf?: number }} options
 */
export function createToolLifecycleTracker({ maxRenewalsPerLeaf = 6 } = {}) {
  if (!Number.isInteger(maxRenewalsPerLeaf) || maxRenewalsPerLeaf <= 0) {
    throw new TypeError(`The tool lifecycle renewal budget must be a positive integer, got ${maxRenewalsPerLeaf}`)
  }
  const stageByProbe = new Map()
  const renewalsByLeaf = new Map()

  return {
    observe({ leaf, probe, stage, facts }) {
      if (typeof leaf !== 'string' || leaf.length === 0) return { renewed: false, reason: 'unidentified monitored leaf' }
      if (typeof probe !== 'string' || probe.length === 0) return { renewed: false, reason: 'unidentified monitored child' }
      const index = TOOL_LIFECYCLE_STAGES.indexOf(stage)
      if (index === -1) return { renewed: false, reason: `unknown lifecycle stage: ${String(stage)}` }
      if (facts?.pid !== undefined && String(facts.pid) !== probe) {
        return { renewed: false, reason: 'the observed facts name another child' }
      }
      const previous = stageByProbe.get(probe)
      if (previous !== undefined && index <= previous) {
        return { renewed: false, reason: 'the same lifecycle stage observed again' }
      }
      stageByProbe.set(probe, index)
      const used = renewalsByLeaf.get(leaf) ?? 0
      if (used >= maxRenewalsPerLeaf) {
        return { renewed: false, reason: `the monitored leaf exhausted its ${maxRenewalsPerLeaf} lifecycle renewal(s)` }
      }
      renewalsByLeaf.set(leaf, used + 1)
      return { renewed: true, reason: `tool-lifecycle:${TOOL_LIFECYCLE_STAGES[index]}`, stage: TOOL_LIFECYCLE_STAGES[index] }
    },
    renewalsFor(leaf) { return renewalsByLeaf.get(leaf) ?? 0 },
  }
}

/**
 * Report one observed lifecycle fact of a child this process owns.
 *
 * The line carries the reporter's own process identity so the receiver can bind it to the
 * process that actually observed it — a fact about a child is only evidence when the reporter
 * is the party that owns that child.
 */
export function reportToolLifecycle({ probe, stage, facts }) {
  const line = `${TOOL_LIFECYCLE_PREFIX}${JSON.stringify({
    pid: process.pid, parentPid: process.ppid, probe: String(probe), stage, facts: facts ?? null,
  })}\n`
  process.stdout.write(line)
}

/**
 * Parse the lifecycle facts a worker reported, and bind each to the leaf that was running.
 *
 * The leaf is not declared by the reporter: it is the leaf whose body the worker's own
 * `[verification-test-start]` fact announced last, so a fact cannot be attributed to a leaf
 * that was not running.
 *
 * @param {{ entryFile: string }} options
 */
export function createToolLifecycleObserver({ entryFile }) {
  const planned = resolve(entryFile)
  let currentLeaf = null
  let pending = ''
  let dropping = false

  const observeLine = (line, event) => {
    let record
    try {
      record = JSON.parse(line.slice(TOOL_LIFECYCLE_PREFIX.length))
    } catch {
      throw new TypeError('A tool lifecycle record contained invalid JSON')
    }
    if (!record || typeof record !== 'object') throw new TypeError('A tool lifecycle record must be an object')
    if (record.parentPid !== process.pid) throw new TypeError('A tool lifecycle record came from another parent')
    if (record.entryFile !== undefined && resolve(record.entryFile) !== planned) {
      throw new TypeError('A tool lifecycle record names another test file')
    }
    if (currentLeaf === null) throw new TypeError('A tool lifecycle record arrived before any leaf started')
    return {
      leaf: currentLeaf,
      probe: String(record.probe),
      stage: record.stage,
      facts: record.facts ?? null,
    }
  }

  return {
    observe(event) {
      if (event?.type !== 'test:stdout' && event?.type !== 'test:stderr') return null
      if (typeof event.data?.message !== 'string') return null
      const message = event.data.message
      let offset = 0
      const observed = []
      while (offset < message.length) {
        const end = message.indexOf('\n', offset)
        const stop = end === -1 ? message.length : end
        if (!dropping) {
          pending += message.slice(offset, stop)
          if (pending.length > 8192) { pending = ''; dropping = true }
        }
        if (end === -1) break
        if (!dropping) {
          if (pending.startsWith(TOOL_LIFECYCLE_PREFIX)) observed.push(observeLine(pending, event))
          else if (pending.startsWith('[verification-test-start] ')) {
            const fact = JSON.parse(pending.slice('[verification-test-start] '.length))
            if (fact.pid === event.data.pid) currentLeaf = `${planned}::${fact.fullName ?? fact.name}`
          }
        }
        pending = ''
        dropping = false
        offset = end + 1
      }
      return observed
    },
  }
}
