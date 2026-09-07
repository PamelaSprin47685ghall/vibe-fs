import assert from 'node:assert/strict'

const validateFatalBoundary = (descriptor) => {
  const violations = []
  if (descriptor.capabilityBinding !== 'mandatory-injected' || descriptor.reportOwnerCount !== 1 || descriptor.killOwnerCount !== 1) {
    violations.push({ code: 'fatal-capability-not-mandatory', subsystem: descriptor.subsystem })
  }
  if (descriptor.physicalDependency !== 'none') {
    violations.push({ code: 'fatal-direct-physical-dependency', subsystem: descriptor.subsystem })
  }
  if (descriptor.alreadyHandled || descriptor.reportOwnerCount > 1 || descriptor.killOwnerCount > 1) {
    violations.push({ code: 'fatal-incident-duplicate', incidentId: descriptor.incidentId })
  }
  if (!['committed', 'unknown', 'not-required'].includes(descriptor.settlement)) {
    violations.push({ code: 'fatal-before-settlement', incidentId: descriptor.incidentId })
  }
  return violations
}

export const assertFatalBoundary = (subsystem, settlement = 'committed') => {
  const legal = {
    subsystem,
    incidentId: `${subsystem}-incident`,
    settlement,
    capabilityBinding: 'mandatory-injected',
    physicalDependency: 'none',
    reportOwnerCount: 1,
    killOwnerCount: 1,
    alreadyHandled: false,
  }
  assert.deepEqual(validateFatalBoundary(legal), [])
  assert.deepEqual(validateFatalBoundary({ ...legal, capabilityBinding: 'optional' }), [{ code: 'fatal-capability-not-mandatory', subsystem }])
  assert.deepEqual(validateFatalBoundary({ ...legal, physicalDependency: 'direct' }), [{ code: 'fatal-direct-physical-dependency', subsystem }])
  assert.deepEqual(validateFatalBoundary({ ...legal, alreadyHandled: true }), [{ code: 'fatal-incident-duplicate', incidentId: `${subsystem}-incident` }])
  if (settlement !== 'not-required') {
    assert.deepEqual(validateFatalBoundary({ ...legal, settlement: 'missing' }), [{ code: 'fatal-before-settlement', incidentId: `${subsystem}-incident` }])
  }
}

const validateInjectedEffect = ({ compileEdges, injectedEdges, physicalShard }) => {
  const violations = []
  for (const [consumer, provider] of compileEdges) {
    if (provider === physicalShard && consumer !== 'root') {
      violations.push({ code: 'effect-reachable-from-non-composition', consumer, provider })
    }
  }
  if (!injectedEdges.some(([consumer, provider]) => consumer === 'consumer' && provider === physicalShard)) {
    violations.push({ code: 'effect-capability-not-injected', consumer: 'consumer', provider: physicalShard })
  }
  return violations
}

export const assertEffectIsInjected = (authority) => {
  const physicalShard = `physical-${authority}`
  const legal = {
    physicalShard,
    compileEdges: [
      ['consumer', 'port'],
      ['root', physicalShard],
      ['root', 'consumer'],
      ['root', 'port'],
    ],
    injectedEdges: [['consumer', physicalShard]],
  }
  assert.deepEqual(validateInjectedEffect(legal), [])
  const direct = structuredClone(legal)
  direct.compileEdges.push(['consumer', physicalShard])
  direct.injectedEdges = []
  assert.deepEqual(validateInjectedEffect(direct), [
    { code: 'effect-reachable-from-non-composition', consumer: 'consumer', provider: physicalShard },
    { code: 'effect-capability-not-injected', consumer: 'consumer', provider: physicalShard },
  ])
}

export const assertPureContract = () => {
  const validate = (authorities) => authorities.length === 0 ? [] : [{ code: 'invalid-contract-surface', shard: 'contract' }]
  assert.deepEqual(validate([]), [])
  assert.deepEqual(validate(['process-control']), [{ code: 'invalid-contract-surface', shard: 'contract' }])
}

export const assertOptionalObservationNoninterference = async () => {
  const outcome = Object.freeze({ case: 'continue', payload: { ordinal: 7 } })
  const preserve = async (observe) => {
    try {
      await observe()
    } catch {
      return outcome
    }
    return outcome
  }
  assert.equal(await preserve(() => undefined), outcome)
  assert.equal(await preserve(() => { throw new Error('diagnostic failed') }), outcome)
  assert.equal(await preserve(async () => { throw new Error('async diagnostic failed') }), outcome)
}
