import { spawn } from 'node:child_process'

export function runVerificationToolProbe(executable, argv, { cwd, env, signal }) {
  signal?.throwIfAborted()
  return new Promise((resolve, reject) => {
    const child = spawn(executable, argv, { cwd, env, detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'] })
    let failure
    let cancellation
    let stdout = ''
    let stderr = ''
    const stop = () => {
      try {
        if (process.platform === 'win32') child.kill('SIGKILL')
        else if (child.pid) process.kill(-child.pid, 'SIGKILL')
      } catch (error) {
        if (error.code !== 'ESRCH') failure ??= error
      }
    }
    const abort = () => {
      cancellation ??= { reason: signal.reason }
      stop()
    }
    signal?.addEventListener('abort', abort, { once: true })
    if (signal?.aborted) abort()
    child.once('error', error => { failure ??= error })
    for (const [name, stream] of [['stdout', child.stdout], ['stderr', child.stderr]]) {
      stream.setEncoding('utf8').on('data', chunk => {
        if (name === 'stdout') stdout += chunk
        else stderr += chunk
        if (stdout.length + stderr.length > 65536) {
          failure ??= new Error('Tool identity probe exceeded its output boundary')
          stop()
        }
      })
      stream.once('error', error => {
        failure ??= error
        stop()
      })
    }
    child.once('exit', stop)
    child.once('close', (exitCode, exitSignal) => {
      signal?.removeEventListener('abort', abort)
      if (cancellation) reject(cancellation.reason)
      else if (failure || exitCode !== 0) reject(Object.assign(new Error('Selected tool identity probe failed', { cause: failure }), { code: 'verification-tool-probe-failed', exitCode, signal: exitSignal, stdout, stderr }))
      else resolve(stdout.trim())
    })
  })
}
