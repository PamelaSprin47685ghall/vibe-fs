import { write } from 'node:fs'
import { isAbsolute, resolve } from 'node:path'

export const WORKER_COST_PREFIX = '[verification-worker-cost] '
export const MAX_WORKER_COST_BYTES = 8192
const BODY_PREFIX = '[verification-test-start] '

function recordFields(record) {
  const keys = ['version', 'phase', 'sequence', 'entryFile', 'pid', 'parentPid', 'monotonicMs', 'cpuUserMicros', 'cpuSystemMicros']
  if (record.phase === 'exit') keys.push('exitCode')
  return keys
}

function validateRecord(record) {
  if (!record || typeof record !== 'object' || Array.isArray(record) ||
      record.version !== 1 || !['pre-import', 'exit'].includes(record.phase) ||
      record.sequence !== (record.phase === 'pre-import' ? 0 : 1) ||
      typeof record.entryFile !== 'string' || !isAbsolute(record.entryFile) ||
      !Number.isSafeInteger(record.pid) || record.pid <= 0 ||
      !Number.isSafeInteger(record.parentPid) || record.parentPid <= 0 ||
      record.pid === record.parentPid ||
      !Number.isFinite(record.monotonicMs) || record.monotonicMs < 0 ||
      !Number.isSafeInteger(record.cpuUserMicros) || record.cpuUserMicros < 0 ||
      !Number.isSafeInteger(record.cpuSystemMicros) || record.cpuSystemMicros < 0 ||
      record.phase === 'exit' && (!Number.isInteger(record.exitCode) || record.exitCode < 0 || record.exitCode > 255) ||
      Object.keys(record).some(key => !recordFields(record).includes(key))) {
    throw new TypeError('Invalid native worker cost record')
  }
  return { ...record }
}

export function encodeWorkerCostRecord(record) {
  const line = `${WORKER_COST_PREFIX}${JSON.stringify(validateRecord(record))}\n`
  if (Buffer.byteLength(line) > MAX_WORKER_COST_BYTES) throw new RangeError('Worker cost record exceeds its byte boundary')
  return line
}

export function installWorkerCostObservation() {
  if (process.env.WXS_VERIFICATION_WORKER_DIAGNOSTICS === '0' ||
      process.env.NODE_TEST_CONTEXT !== 'child-v8' || typeof process.argv[1] !== 'string') return
  const sample = (phase, exitCode) => {
    const cpu = process.cpuUsage()
    return {
      version: 1, phase, sequence: phase === 'pre-import' ? 0 : 1,
      entryFile: resolve(process.argv[1]), pid: process.pid, parentPid: process.ppid,
      monotonicMs: performance.now(), cpuUserMicros: cpu.user, cpuSystemMicros: cpu.system,
      ...(phase === 'exit' ? { exitCode } : {}),
    }
  }
  try { write(1, encodeWorkerCostRecord(sample('pre-import')), () => {}) } catch {}
  process.once('exit', code => {
    try { process.stdout.write(encodeWorkerCostRecord(sample('exit', code)), () => {}) } catch {}
  })
}

function interval(preImport, exit) {
  if (exit.monotonicMs < preImport.monotonicMs ||
      exit.cpuUserMicros < preImport.cpuUserMicros || exit.cpuSystemMicros < preImport.cpuSystemMicros) {
    throw new TypeError('Worker cost counters moved backwards')
  }
  return {
    clockPid: preImport.pid, wallMs: exit.monotonicMs - preImport.monotonicMs,
    cpuUserMicros: exit.cpuUserMicros - preImport.cpuUserMicros,
    cpuSystemMicros: exit.cpuSystemMicros - preImport.cpuSystemMicros,
  }
}

export function createWorkerCostObserver({ entryFile, sourceFile = entryFile, parentPid, enabled = true }) {
  if (!isAbsolute(entryFile) || !isAbsolute(sourceFile) || !Number.isSafeInteger(parentPid) || parentPid <= 0 || typeof enabled !== 'boolean') {
    throw new TypeError('Worker cost observation requires its actual file and parent identity')
  }
  const sources = new Set([resolve(entryFile), resolve(sourceFile)])
  let preImport = null
  let exit = null
  let bodyIdentity = null
  let invalidReason = null
  let pending = ''
  let droppingLine = false
  const invalidate = reason => { invalidReason ??= reason }
  const sameIdentity = record => record.parentPid === parentPid && sources.has(resolve(record.entryFile)) &&
    (!preImport || record.pid === preImport.pid) && (!bodyIdentity || record.pid === bodyIdentity.pid)
  const observeLine = (line, event) => {
    const cost = line.startsWith(WORKER_COST_PREFIX)
    const body = line.startsWith(BODY_PREFIX)
    if (!cost && !body) return
    if (event.data?.entryFile !== entryFile ||
        typeof event.data?.file === 'string' && !sources.has(resolve(event.data.file))) {
      invalidate('Worker diagnostic differs from its node:test source file')
      return
    }
    if (invalidReason) return
    try {
      const record = JSON.parse(line.slice((cost ? WORKER_COST_PREFIX : BODY_PREFIX).length))
      if (body) {
        if (!record || !Number.isSafeInteger(record.pid) || record.pid <= 0 ||
            !Number.isSafeInteger(record.parentPid) || typeof record.entryFile !== 'string' || !isAbsolute(record.entryFile) ||
            !sameIdentity(record)) throw new TypeError('Worker body identity differs from its native cost observation')
        bodyIdentity = { pid: record.pid, parentPid: record.parentPid }
        return
      }
      validateRecord(record)
      if (!sameIdentity(record)) throw new TypeError('Worker cost record names another file or process')
      if (record.phase === 'pre-import') {
        if (preImport || exit) throw new TypeError('Worker pre-import record was repeated or reordered')
        preImport = record
      } else {
        if (!preImport || exit) throw new TypeError('Worker exit record was repeated or reordered')
        interval(preImport, record)
        exit = record
      }
    } catch (error) { invalidate(error instanceof SyntaxError ? 'Worker diagnostic contains invalid JSON' : error.message) }
  }
  const snapshot = () => ({
    version: 1, entryFile, enabled, pid: preImport?.pid ?? null,
    status: !enabled ? 'disabled' : invalidReason ? 'invalid' : !preImport || !exit ? 'missing' : 'complete',
    reason: !enabled ? 'Worker diagnostics disabled' : invalidReason ?? (!preImport ? 'Pre-import observation not received' : !exit ? 'Exit observation not received' : null),
    preImport: preImport ? { ...preImport } : null, exit: exit ? { ...exit } : null,
    interval: enabled && !invalidReason && preImport && exit ? interval(preImport, exit) : null,
  })
  return {
    observe(event) {
      if (!enabled || event?.type !== 'test:stdout' || typeof event.data?.message !== 'string') return
      if (event.data.entryFile !== entryFile ||
          typeof event.data.file === 'string' && !sources.has(resolve(event.data.file))) {
        invalidate('Worker diagnostic differs from its node:test source file')
        return
      }
      const message = event.data.message
      let offset = 0
      while (offset < message.length) {
        const end = message.indexOf('\n', offset)
        const stop = end === -1 ? message.length : end
        if (!droppingLine) {
          pending += message.slice(offset, Math.min(stop, offset + MAX_WORKER_COST_BYTES + 1))
          if (Buffer.byteLength(pending) >= MAX_WORKER_COST_BYTES) {
            if (pending.startsWith(WORKER_COST_PREFIX)) invalidate('Worker cost record exceeds its byte boundary')
            pending = ''
            droppingLine = true
          }
        }
        if (end === -1) break
        if (!droppingLine) observeLine(pending, event)
        pending = ''
        droppingLine = false
        offset = end + 1
      }
    },
    snapshot,
    finish() {
      if (pending.startsWith(WORKER_COST_PREFIX)) invalidate('Worker cost record ended before its newline')
      pending = ''
      return snapshot()
    },
  }
}

export function validateWorkerCostSnapshot(value, { entryFile, sourceFile = entryFile, parentPid, enabled = true }) {
  const fields = ['version', 'entryFile', 'enabled', 'pid', 'status', 'reason', 'preImport', 'exit', 'interval']
  if (!value || typeof value !== 'object' || value.version !== 1 || value.entryFile !== entryFile ||
      value.enabled !== enabled || Object.keys(value).length !== fields.length || Object.keys(value).some(key => !fields.includes(key)) ||
      !['disabled', 'missing', 'invalid', 'complete'].includes(value.status) ||
      !(value.reason === null || typeof value.reason === 'string' && Buffer.byteLength(value.reason) <= 256) ||
      Buffer.byteLength(JSON.stringify(value)) > 3 * MAX_WORKER_COST_BYTES) {
    throw new TypeError('Invalid worker cost snapshot')
  }
  const observer = createWorkerCostObserver({ entryFile, sourceFile, parentPid, enabled })
  for (const record of [value.preImport, value.exit]) {
    if (record === null) continue
    observer.observe({ type: 'test:stdout', data: { entryFile, file: sourceFile, message: encodeWorkerCostRecord(record) } })
  }
  const actual = observer.finish()
  if (actual.status === 'invalid' || value.pid !== actual.pid ||
      JSON.stringify(value.preImport) !== JSON.stringify(actual.preImport) || JSON.stringify(value.exit) !== JSON.stringify(actual.exit) ||
      !enabled && value.status === 'invalid' ||
      value.status !== 'invalid' && value.status !== actual.status ||
      value.status !== 'invalid' && value.reason !== actual.reason ||
      value.status === 'invalid' && (typeof value.reason !== 'string' || value.reason.length === 0) ||
      JSON.stringify(value.interval) !== JSON.stringify(value.status === 'invalid' ? null : actual.interval)) {
    throw new TypeError('Worker cost snapshot does not match its native observations')
  }
  return { ...actual, status: value.status, reason: value.reason, interval: value.status === 'invalid' ? null : actual.interval }
}
