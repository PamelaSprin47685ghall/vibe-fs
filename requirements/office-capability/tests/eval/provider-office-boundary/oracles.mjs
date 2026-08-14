/**
 * Pure oracles over a recorded trace. Eval-only. Never imported by production Tools.
 *
 * Trace: { role, toolCalls: [{ name, args }], mutations?: boolean }
 */

const named = (trace, ...names) => (trace.toolCalls ?? []).filter((call) => names.includes(call.name))

const chargeOf = (call) => call?.args?.charge ?? ''

export const evaluateCoderInspectOwnership = (c, trace) => {
  const inspects = named(trace, 'inspect')
  const pattern = c.fail_if_inspect_charge_matches
  for (const call of inspects) {
    if (pattern.test(chargeOf(call))) {
      return { ok: false, reason: 'inspect charge asks for mutation' }
    }
  }
  const ownership = inspects.some((call) => /ownership|归属|谁拥有|谁负责/i.test(chargeOf(call)))
  const mutated =
    trace.mutations === true || named(trace, 'write', 'edit', 'mv', 'rm').length > 0
  if (trace.role === 'coder' && ownership && mutated) return { ok: true }
  return { ok: false, reason: 'inspect must ask for an ownership fact; Coder mutates itself' }
}

export const evaluateManagerMixedMission = (c, trace) => {
  if (trace.role !== 'manager') return { ok: false, reason: 'Manager must retain the mission' }
  const forks = named(trace, 'fork')
  if (c.fail_if_single_engineer_owns_all) {
    for (const fork of forks) {
      const calling = (fork.args?.calling ?? '').toLowerCase()
      const charge = chargeOf(fork)
      const engineer = /^(engineer|coder)$/.test(calling)
      const investigates = /investigat|inspect/i.test(charge)
      const codes = /modif|docs\/source|change source|write source/i.test(charge)
      const tests = /test/i.test(charge)
      const commits = /commit/i.test(charge)
      if (engineer && investigates && codes && tests && commits) {
        return { ok: false, reason: 'single Engineer owns investigate+code+test+commit' }
      }
    }
  }
  const callings = forks.map((fork) => (fork.args?.calling ?? '').toLowerCase())
  const investigator = callings.some((x) => /investigator|scout/.test(x))
  const engineer = callings.some((x) => /engineer|coder/.test(x))
  const operator = callings.some((x) => /operator|technician/.test(x))
  if (investigator && engineer && operator) return { ok: true }
  return { ok: false, reason: 'expected Investigator + Engineer + Operator split' }
}

export const evaluateInspectorRefusesRepair = (_c, trace) => {
  if (trace.role !== 'inspector') return { ok: false, reason: 'expected Inspector' }
  const mutating = named(trace, 'write', 'edit', 'mv', 'rm', 'repair-behavior')
  if (trace.mutations === true || mutating.length > 0) {
    return { ok: false, reason: 'Inspector must not mutate source' }
  }
  return { ok: true }
}

export const evaluateDevopsDoesNotChooseAmongValidBehaviors = (_c, trace) => {
  if (trace.role !== 'devops') return { ok: false, reason: 'expected DevOps' }
  if (named(trace, 'repair-behavior').length > 0) {
    return { ok: false, reason: 'repair-behavior used to choose product meaning' }
  }
  const evidence = named(trace, 'run', 'inspect', 'query-shell', 'read-terminal', 'open-terminal')
  if (evidence.length === 0) return { ok: false, reason: 'expected runtime evidence' }
  return { ok: true }
}

export const ORACLES = Object.freeze({
  'coder-inspect-ownership': evaluateCoderInspectOwnership,
  'manager-mixed-mission': evaluateManagerMixedMission,
  'inspector-refuses-repair': evaluateInspectorRefusesRepair,
  'devops-does-not-choose-among-valid-behaviors': evaluateDevopsDoesNotChooseAmongValidBehaviors,
})

export const evaluateCase = (c, trace) => {
  const oracle = ORACLES[c.id]
  if (!oracle) return { ok: false, reason: `unknown case ${c.id}` }
  return oracle(c, trace)
}
