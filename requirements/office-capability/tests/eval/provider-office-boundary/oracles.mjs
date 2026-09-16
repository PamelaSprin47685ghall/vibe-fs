/**
 * Pure oracles over a recorded trace. Eval-only. Never imported by production Tools.
 *
 * Trace: { role, toolCalls: [{ name, args }], mutations?: boolean }
 */

const named = (trace, ...names) => (trace.toolCalls ?? []).filter((call) => names.includes(call.name))

const chargeOf = (call) => call?.args?.charge ?? ''

export const evaluateEngineerLocalInvestigationAndMutation = (_c, trace) => {
  if (trace.role !== 'engineer') return { ok: false, reason: 'expected Engineer' }
  const executes = named(trace, 'run', 'query-shell', 'open-terminal', 'send-terminal', 'exec')
  if (executes.length > 0) return { ok: false, reason: 'Engineer must not execute real commands' }
  const reads = named(trace, 'read', 'glob', 'grep')
  const writes = named(trace, 'write', 'edit', 'mv', 'rm')
  if (reads.length > 0 || writes.length > 0 || trace.mutations === true) return { ok: true }
  return { ok: false, reason: 'Engineer should read or edit source directly' }
}

export const evaluateManagerMixedMission = (_c, trace) => {
  if (trace.role !== 'manager') return { ok: false, reason: 'Manager must retain the mission' }
  const forks = named(trace, 'fork')
  const resumes = named(trace, 'resume')

  // Manager must not fork DevOps (DevOps is resumed, not forked)
  for (const fork of forks) {
    const calling = (fork.args?.calling ?? '').toLowerCase()
    if (calling === 'devops' || calling === 'operator') {
      return { ok: false, reason: 'Manager cannot fork DevOps' }
    }
  }

  const hasEngineerFork = forks.some((f) => /engineer|coder/.test((f.args?.calling ?? '').toLowerCase()))
  const hasDevOpsResume = resumes.some((r) => r.args?.name !== undefined)

  if (hasEngineerFork && hasDevOpsResume) return { ok: true }
  return { ok: false, reason: 'expected Engineer fork + fixed DevOps resume split' }
}

export const evaluateDevopsInherentRepair = (_c, trace) => {
  if (trace.role !== 'devops') return { ok: false, reason: 'expected DevOps' }
  const fissions = named(trace, 'fission')
  if (fissions.length > 0) return { ok: false, reason: 'DevOps must not Fission' }
  const runs = named(trace, 'run', 'open-terminal', 'send-terminal')
  if (runs.length === 0) return { ok: false, reason: 'expected execution by DevOps' }
  return { ok: true }
}

export const evaluateDevopsDoesNotChooseAmongValidBehaviors = (_c, trace) => {
  if (trace.role !== 'devops') return { ok: false, reason: 'expected DevOps' }
  if (trace.mutations === true && named(trace, 'edit', 'write').length > 0) {
    return { ok: false, reason: 'DevOps must not unilaterally choose between distinct architectural/product behaviors' }
  }
  const evidence = named(trace, 'run', 'read-terminal', 'open-terminal')
  if (evidence.length === 0) return { ok: false, reason: 'expected runtime evidence' }
  return { ok: true }
}

export const ORACLES = Object.freeze({
  'engineer-local-investigation-and-mutation': evaluateEngineerLocalInvestigationAndMutation,
  'manager-mixed-mission': evaluateManagerMixedMission,
  'devops-inherent-repair': evaluateDevopsInherentRepair,
  'devops-does-not-choose-among-valid-behaviors': evaluateDevopsDoesNotChooseAmongValidBehaviors,
})

export const evaluateCase = (c, trace) => {
  const oracle = ORACLES[c.id]
  if (!oracle) return { ok: false, reason: `unknown case ${c.id}` }
  return oracle(c, trace)
}
