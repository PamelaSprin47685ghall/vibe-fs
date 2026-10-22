import { execFileSync } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'
import { PROCESS_TREE_TIMEOUT_MS } from './time-budget.js'

function inspectProcesses({ timeout }) {
  return execFileSync('ps', ['-eo', 'pid=,ppid=,pgid=,stat='], { encoding: 'utf8', timeout })
}

function processRecords(deadline, phase, inspect = inspectProcesses) {
  const remaining = deadline - Date.now()
  if (remaining <= 0) throw new Error('Owned termination observation deadline expired')
  const output = inspect({ phase, timeout: Math.min(PROCESS_TREE_TIMEOUT_MS, remaining) })
  if (!output.trim()) throw new Error('process inspection returned no records')
  return output.trim().split('\n').map(line => {
    const fields = /^\s*(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s*$/.exec(line)
    if (!fields) throw new Error(`unparseable process record: ${line}`)
    return { pid: Number(fields[1]), parent: Number(fields[2]), group: Number(fields[3]), state: fields[4] }
  }).filter(record => !/^[ZX]/.test(record.state))
}

function rememberDescendantGroups(pgid, records, groups) {
  const descendants = new Set(records.filter(record => record.group === pgid).map(record => record.pid))
  let added
  do {
    added = false
    for (const record of records) {
      if (descendants.has(record.parent) && !descendants.has(record.pid)) {
        descendants.add(record.pid)
        added = true
      }
    }
  } while (added)
  for (const record of records) {
    if (!descendants.has(record.pid) || record.group === pgid) continue
    if (!groups.has(record.group)) groups.set(record.group, new Set())
    groups.get(record.group).add(record.pid)
  }
}

function terminateObservedGroups(groups, deadline, inspect) {
  if (groups.size === 0) return
  const records = processRecords(deadline, 'descendant-termination', inspect)
  const caller = records.find(record => record.pid === process.pid)
  if (!caller) throw new Error('The supervisor process was absent from the descendant termination snapshot')
  const failures = []
  for (const [group, captured] of groups) {
    const members = records.filter(record => record.group === group)
    if (members.length === 0) continue
    if (group === caller.group || !members.some(record => captured.has(record.pid))) {
      failures.push(new Error(`Captured descendant group ${group} no longer has its exact owned process witness`))
      continue
    }
    try {
      process.kill(-group, 'SIGKILL')
    } catch (error) {
      if (error.code !== 'ESRCH') failures.push(error)
    }
  }
  if (failures.length > 0) throw new AggregateError(failures, 'Captured descendant group termination failed', { cause: failures[0] })
}

async function freezeAndCaptureDescendantGroups(pgid, deadline, inspect, groups) {
  try {
    process.kill(-pgid, 'SIGSTOP')
  } catch (error) {
    if (error.code === 'ESRCH') throw new Error('The owned inner group disappeared before frozen descendant capture', { cause: error })
    throw error
  }
  while (true) {
    const records = processRecords(deadline, 'freeze-confirmation', inspect)
    rememberDescendantGroups(pgid, records, groups)
    const owned = records.filter(record => record.group === pgid)
    if (owned.length === 0) throw new Error('The owned inner group was absent from the frozen descendant snapshot')
    if (owned.every(record => /^[Tt]/.test(record.state))) return
    if (Date.now() >= deadline) throw new Error(`Could not observe the frozen inner process group ${pgid}`)
    await delay(5)
  }
}

async function captureFrozenDescendantGroups(pgid, deadline, inspect, groups) {
  let initialFailure
  try {
    rememberDescendantGroups(pgid, processRecords(deadline, 'initial-capture', inspect), groups)
  } catch (error) {
    initialFailure = { error }
  }
  try {
    await freezeAndCaptureDescendantGroups(pgid, deadline, inspect, groups)
  } catch (error) {
    if (initialFailure) throw new AggregateError([initialFailure.error, error], 'Initial and frozen descendant capture failed', { cause: initialFailure.error })
    throw error
  }
  if (initialFailure) throw initialFailure.error
}

async function awaitObservedGroups(groups, deadline, inspect, phase = 'descendant-drain') {
  if (groups.size === 0) return
  while (true) {
    const survivors = processRecords(deadline, phase, inspect).filter(record => groups.has(record.group))
    if (survivors.length === 0) return
    if (Date.now() >= deadline) {
      throw new Error(`Observed descendant groups did not drain: ${survivors.map(record => `${record.pid}/${record.group}`).join(', ')}`)
    }
    await delay(10)
  }
}

async function failAfterObservedCleanup(error, groups, deadline, inspect) {
  try {
    await awaitObservedGroups(groups, deadline, inspect, 'failure-drain')
  } catch (cleanupError) {
    throw new AggregateError([error, cleanupError], 'Owned termination and remaining group observation failed', { cause: error })
  }
  throw error
}

// The caller owns the detached child and the single absolute cleanup deadline.
export async function reclaimOwnedProcessGroup(child, { deadline, inspectProcessTree } = {}) {
  if (!Number.isSafeInteger(child?.pid) || child.pid <= 0) throw new TypeError('Owned reclamation requires the actual spawned child PID')
  if (!Number.isSafeInteger(deadline)) throw new TypeError('Owned reclamation requires an absolute cleanup deadline')
  const groups = new Map()
  let captureFailure
  try {
    await captureFrozenDescendantGroups(child.pid, deadline, inspectProcessTree, groups)
  } catch (error) {
    captureFailure = { error }
  } finally {
    try {
      terminateObservedGroups(groups, deadline, inspectProcessTree)
    } catch (error) {
      captureFailure = { error: captureFailure
        ? new AggregateError([captureFailure.error, error], 'Owned capture and descendant termination failed', { cause: captureFailure.error })
        : error }
    }
    try {
      process.kill(-child.pid, 'SIGKILL')
    } catch (error) {
      if (error.code !== 'ESRCH') {
        captureFailure = { error: captureFailure
          ? new AggregateError([captureFailure.error, error], 'Owned capture and inner termination failed', { cause: captureFailure.error })
          : error }
      }
    }
  }
  groups.set(child.pid, new Set([child.pid]))
  if (captureFailure) await failAfterObservedCleanup(captureFailure.error, groups, deadline, inspectProcessTree)
  try {
    await awaitObservedGroups(groups, deadline, inspectProcessTree)
  } catch (error) {
    await failAfterObservedCleanup(error, groups, deadline, inspectProcessTree)
  }
}
