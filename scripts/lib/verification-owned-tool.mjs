import { spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { writeVerificationToolPhase } from './verification-tool-diagnostics.mjs'

const monitorPath = fileURLToPath(new URL('./verification-tool-monitor.mjs', import.meta.url))
const deadlineVariable = 'WXS_VERIFICATION_TOOL_DEADLINE_MS'

function deadlineFailure(deadline) {
  return Object.assign(new Error(`The selected tool deadline expired (${deadline})`), {
    code: 'verification-tool-deadline-exceeded', deadline,
  })
}

function monitorFailure(message, cause) {
  return Object.assign(new Error(message, { cause }), { code: 'verification-tool-monitor-failed' })
}

function decodeFailure(value, depth = 0) {
  if (depth > 4 || !value || typeof value !== 'object' || typeof value.name !== 'string' || typeof value.message !== 'string') {
    throw monitorFailure('The tool monitor supplied an invalid failure')
  }
  const error = new Error(value.message, value.cause === undefined ? undefined : { cause: decodeFailure(value.cause, depth + 1) })
  error.name = value.name
  for (const key of ['code', 'errno', 'syscall', 'path']) {
    if (value[key] === undefined) continue
    if (typeof value[key] !== 'string' && typeof value[key] !== 'number') {
      throw monitorFailure(`The tool monitor supplied an invalid failure ${key}`)
    }
    error[key] = value[key]
  }
  if (value.deadline !== undefined) {
    if (!Number.isSafeInteger(value.deadline)) throw monitorFailure('The tool monitor supplied an invalid failure deadline')
    error.deadline = value.deadline
  }
  return error
}

function decodeTerminal(message) {
  if (!message || message.type !== 'verification-tool-terminal' || message.version !== 1 ||
      !Array.isArray(message.failures) || message.failures.length > 8 ||
      !(message.exitCode === null || Number.isInteger(message.exitCode) && message.exitCode >= 0 && message.exitCode <= 255) ||
      !(message.signal === null || typeof message.signal === 'string' && /^SIG[A-Z0-9]+$/.test(message.signal)) ||
      message.exitCode !== null && message.signal !== null ||
      message.exitCode === null && message.signal === null && message.failures.length === 0) {
    throw monitorFailure('The tool monitor supplied an invalid terminal record')
  }
  return { exitCode: message.exitCode, signal: message.signal, failures: message.failures.map(value => decodeFailure(value)) }
}

export function spawnOwnedVerificationTool(executable, argv, { cwd, env, deadline }) {
  if (typeof executable !== 'string' || executable.length === 0) throw new TypeError('The selected tool executable must be a non-empty string')
  if (!Array.isArray(argv)) throw new TypeError('The selected tool arguments must be an array')
  if (deadline !== undefined && !Number.isSafeInteger(deadline)) throw new TypeError('The selected tool deadline must be an absolute safe-integer millisecond time')
  if (deadline !== undefined && Date.now() >= deadline) throw deadlineFailure(deadline)
  const diagnostics = process.env.WXS_VERIFICATION_TOOL_DIAGNOSTICS === '1'
  const monitorEnv = { ...(env ?? process.env) }
  delete monitorEnv.WXS_VERIFICATION_TOOL_DIAGNOSTICS
  delete monitorEnv[deadlineVariable]
  if (deadline !== undefined) monitorEnv[deadlineVariable] = String(deadline)
  if (diagnostics) monitorEnv.WXS_VERIFICATION_TOOL_DIAGNOSTICS = '1'
  if (deadline !== undefined && Date.now() >= deadline) throw deadlineFailure(deadline)
  const child = spawn(process.execPath, [monitorPath, executable, ...argv], {
    cwd, env: monitorEnv, detached: process.platform !== 'win32', stdio: ['pipe', 'pipe', 'pipe', 'ipc'],
  })
  child.once('spawn', () => writeVerificationToolPhase(diagnostics, 'monitor-spawned', { monitorPid: child.pid, executable }))
  const failures = []
  const stop = () => {
    try { child.stdin?.destroy() }
    catch (error) { failures.push(monitorFailure('The tool monitor lifeline could not be closed', error)) }
  }
  const completed = new Promise(resolve => {
    let terminal = null
    const fail = error => {
      failures.push(error)
      stop()
    }
    child.once('error', error => fail(monitorFailure('The tool monitor could not start', error)))
    child.stdin?.on('error', error => fail(monitorFailure('The tool monitor lifeline failed', error)))
    child.on('message', message => {
      if (diagnostics && message?.type === 'verification-tool-phase') {
        if (['tool-spawned', 'tool-exited', 'group-drained', 'group-drain-failed'].includes(message.phase) &&
            message.data?.pid === child.pid && message.data?.parentPid === process.pid) {
          writeVerificationToolPhase(true, message.phase, message.data)
        }
        return
      }
      try {
        if (terminal !== null) throw monitorFailure('The tool monitor supplied more than one terminal record')
        terminal = decodeTerminal(message)
      } catch (error) { fail(error) }
    })
    child.once('close', (exitCode, signal) => {
      writeVerificationToolPhase(diagnostics, 'monitor-closed', { monitorPid: child.pid, exitCode, signal })
      if (terminal === null) failures.push(monitorFailure('The tool monitor closed without a valid terminal record'))
      if (exitCode !== 0 || signal !== null) failures.push(monitorFailure(`The tool monitor exited unexpectedly (${exitCode ?? signal})`))
      if (deadline !== undefined && Date.now() >= deadline &&
          !terminal?.failures.some(error => error.code === 'verification-tool-deadline-exceeded' && error.deadline === deadline)) {
        failures.push(deadlineFailure(deadline))
      }
      const allFailures = [...(terminal?.failures ?? []), ...failures]
      resolve({
        exitCode: terminal?.exitCode ?? null,
        signal: terminal?.signal ?? null,
        failure: allFailures.length === 0 ? null : allFailures.length === 1 ? allFailures[0]
          : new AggregateError(allFailures, 'The selected tool or its monitor failed', { cause: allFailures[0] }),
      })
    })
  })
  return { child, stop, completed }
}
