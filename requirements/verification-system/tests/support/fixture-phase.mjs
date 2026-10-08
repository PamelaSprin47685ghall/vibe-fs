import { performance } from 'node:perf_hooks'

export function fixturePhase(phase, details = {}) {
  process.stdout.write(`[verification-fixture-phase] ${JSON.stringify({
    phase, pid: process.pid, parentPid: process.ppid, elapsedMs: performance.now(), ...details,
  })}\n`)
}
