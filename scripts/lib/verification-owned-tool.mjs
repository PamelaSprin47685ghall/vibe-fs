import { spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const monitorPath = fileURLToPath(new URL('./verification-tool-monitor.mjs', import.meta.url))

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

export function spawnOwnedVerificationTool(executable, argv, { cwd, env }) {
  if (typeof executable !== 'string' || executable.length === 0) throw new TypeError('The selected tool executable must be a non-empty string')
  if (!Array.isArray(argv)) throw new TypeError('The selected tool arguments must be an array')
  const child = spawn(process.execPath, [monitorPath, executable, ...argv], {
    cwd, env, detached: process.platform !== 'win32', stdio: ['pipe', 'pipe', 'pipe', 'ipc'],
  })
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
      try {
        if (terminal !== null) throw monitorFailure('The tool monitor supplied more than one terminal record')
        terminal = decodeTerminal(message)
      } catch (error) { fail(error) }
    })
    child.once('close', (exitCode, signal) => {
      if (terminal === null) failures.push(monitorFailure('The tool monitor closed without a valid terminal record'))
      if (exitCode !== 0 || signal !== null) failures.push(monitorFailure(`The tool monitor exited unexpectedly (${exitCode ?? signal})`))
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
