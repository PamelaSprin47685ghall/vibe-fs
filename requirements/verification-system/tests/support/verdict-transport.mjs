import { setImmediate } from 'node:timers/promises'
import nativeConsole, { Console } from 'node:console'
import { syncBuiltinESMExports } from 'node:module'
import { resolve } from 'node:path'
import { isMainThread } from 'node:worker_threads'
import { installWorkerCostObservation } from './worker-cost-observation.mjs'

const control = new URL(import.meta.url).searchParams
const claimedEntry = control.get('entryFile')
const applies = claimedEntry === null || isMainThread && typeof process.argv[1] === 'string' &&
  resolve(process.argv[1]) === claimedEntry && String(process.ppid) === control.get('workerParentPid')
if (applies) {
  installWorkerCostObservation()
  const { beforeEach } = await import('node:test')
  if (claimedEntry !== null) {
    const { installFinitePhysicalWait } = await import('./finite-physical-wait.mjs')
    if (!installFinitePhysicalWait(Object.fromEntries(control))) throw new Error('The allocated test entry refused its transport')
    // The native reporter has already captured its own stdout stream.
    Object.defineProperty(process, 'stdout', { configurable: true, get: () => process.stderr })
    Object.assign(nativeConsole, new Console({ stdout: process.stderr, stderr: process.stderr }))
    globalThis.console = nativeConsole
    syncBuiltinESMExports()
  }

  // The previous verdict exists only after its afterEach hooks have finished.
  // Yield before starting the next leaf so synchronous work cannot hold that verdict.
  beforeEach(context => {
    process.stderr.write(`[verification-test-start] ${JSON.stringify({
      pid: process.pid, parentPid: process.ppid,
      entryFile: process.argv[1], name: context.name, fullName: context.fullName,
    })}\n`)
    return setImmediate()
  })
}
