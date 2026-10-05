import { writeSync } from 'node:fs'

export function writeVerificationToolPhase(enabled, phase, details = {}) {
  if (!enabled) return
  try {
    writeSync(2, `[verification-tool-phase] ${JSON.stringify({
      phase, pid: process.pid, parentPid: process.ppid, at: Date.now(), ...details,
    })}\n`)
  } catch {
    // A closed diagnostic sink must not change the tool's result or cleanup.
  }
}

export function publishVerificationToolPhase(enabled, phase, details = {}) {
  if (!enabled || !process.connected) return
  try {
    process.send({ type: 'verification-tool-phase', phase, data: {
      pid: process.pid, parentPid: process.ppid, at: Date.now(), ...details,
    } }, () => {})
  } catch {
    // Diagnostic transport does not decide whether the owned tool succeeded.
  }
}
