import { spawnOwnedVerificationTool } from './verification-owned-tool.mjs'

export function runVerificationToolProbe(executable, argv, { cwd, env, signal }) {
  signal?.throwIfAborted()
  return new Promise((resolve, reject) => {
    const owned = spawnOwnedVerificationTool(executable, argv, { cwd, env })
    const child = owned.child
    let failure
    let setupFailure
    let cancellation
    let stdout = ''
    let stderr = ''
    const abort = () => {
      cancellation ??= { reason: signal.reason }
      owned.stop()
    }
    const complete = ({ exitCode, signal: exitSignal, failure: cleanupFailure }) => {
      signal?.removeEventListener('abort', abort)
      if (setupFailure) {
        if (cleanupFailure !== null && cleanupFailure !== setupFailure.error) reject(new AggregateError([setupFailure.error, cleanupFailure], 'Tool setup and owned cleanup failed', { cause: setupFailure.error }))
        else reject(setupFailure.error)
      } else if (cancellation) {
        if (cleanupFailure !== null) reject(new AggregateError([cancellation.reason, cleanupFailure], 'Tool cancellation and owned cleanup failed', { cause: cancellation.reason }))
        else reject(cancellation.reason)
      } else if (failure || cleanupFailure !== null || exitCode !== 0 || exitSignal !== null) {
        const cause = failure && cleanupFailure !== null && failure !== cleanupFailure
          ? new AggregateError([failure, cleanupFailure], 'Tool observation and owned cleanup failed', { cause: failure })
          : failure ?? cleanupFailure ?? undefined
        reject(Object.assign(new Error('Selected tool identity probe failed', { cause }), { code: 'verification-tool-probe-failed', exitCode, signal: exitSignal, stdout, stderr }))
      }
      else resolve(stdout.trim())
    }
    owned.completed.then(complete, error => complete({
      exitCode: null, signal: null,
      failure: new Error('Owned tool completion failed', { cause: error }),
    }))
    try {
      signal?.addEventListener('abort', abort, { once: true })
      if (signal?.aborted) abort()
      child.once('error', error => { failure ??= error })
      for (const [name, stream] of [['stdout', child.stdout], ['stderr', child.stderr]]) {
        stream.setEncoding('utf8').on('data', chunk => {
          if (name === 'stdout') stdout += chunk
          else stderr += chunk
          if (stdout.length + stderr.length > 65536) {
            failure ??= new Error('Tool identity probe exceeded its output boundary')
            owned.stop()
          }
        })
        stream.once('error', error => {
          failure ??= error
          owned.stop()
        })
      }
    } catch (error) {
      setupFailure = { error }
      owned.stop()
    }
  })
}
