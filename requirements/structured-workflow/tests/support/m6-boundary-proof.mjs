import assert from 'node:assert/strict'

import {
  preserveOutcomeAcrossOptionalObservationV1,
  validateFatalBoundaryV1,
  validateLayeringBlueprintV1,
} from '../../../../scripts/lib/locality-slice-policy-v1.mjs'
import { extractObservedCapabilityFactsV1 } from '../../../../scripts/lib/capability-observations-v1.mjs'

const site = (localityId, ordinal = 0) => ({
  locality_id: localityId,
  source_path: `src/${localityId}.fs`,
  semantic_declaration_anchor: `${localityId}.boundary`,
  same_anchor_occurrence_ordinal: ordinal,
})

const facts = (...observations) => {
  const result = extractObservedCapabilityFactsV1(observations)
  assert.deepEqual(result.violations, [])
  return result.facts
}

const pureFact = (localityId) => ({
  case: 'fable-import',
  payload: { module_specifier: 'node:path/posix', selector: 'join', generated_artifact_id: null, site: site(localityId) },
})

const authorityReferences = {
  console: ['console', 'log'],
  'process-control': ['process', 'exit'],
  'file-system': ['fs', 'readFileSync'],
  host: ['Host', 'send'],
  timer: ['setTimeout'],
}

const authorityFact = (localityId, authority) => {
  const [root, ...memberPath] = authorityReferences[authority]
  return {
    case: 'javascript-capability',
    payload: {
      source_kind: 'generated-artifact',
      source_id: 'boundary-fixture',
      generated_artifact_id: 'boundary-fixture',
      javascript_observation: {
        kind: 'call',
        root,
        member_path: memberPath,
        binding_provenance: 'free',
      },
      site: site(localityId),
    },
  }
}

export const assertFatalBoundary = (owner, settlement = 'committed') => {
  const legal = {
    owner,
    incident: { id: `${owner}-incident`, owner },
    settlement,
    capability_binding: 'mandatory-injected',
    physical_dependency: 'none',
    report_owner_count: 1,
    kill_owner_count: 1,
    already_handled: false,
  }
  assert.deepEqual(validateFatalBoundaryV1(legal), [])
  assert.deepEqual(validateFatalBoundaryV1({ ...legal, capability_binding: 'optional' }), [{ code: 'fatal-capability-not-mandatory', owner }])
  assert.deepEqual(validateFatalBoundaryV1({ ...legal, physical_dependency: 'direct' }), [{ code: 'fatal-direct-physical-dependency', owner }])
  assert.deepEqual(validateFatalBoundaryV1({ ...legal, already_handled: true }), [{ code: 'fatal-incident-duplicate', incident_id: `${owner}-incident` }])
  if (settlement !== 'not-required') {
    assert.deepEqual(validateFatalBoundaryV1({ ...legal, settlement: 'missing' }), [{ code: 'fatal-before-settlement', incident_id: `${owner}-incident` }])
  }
}

export const assertEffectIsInjected = (authority) => {
  const legal = {
    localities: [
      { id: 'consumer', kind: 'runtime', capability_facts: facts(pureFact('consumer')) },
      { id: 'port', kind: 'contract', exposure: 'bounded', capability_facts: facts(pureFact('port')) },
      { id: 'physical', kind: 'adapter', exposure: 'effect', capability_facts: facts(authorityFact('physical', authority)) },
      { id: 'root', kind: 'composition', capability_facts: [] },
    ],
    dependencies: [
      { consumer: 'consumer', provider: 'port', mode: 'compile', relation_kind: 'physical-port', direct_grant: true },
      { consumer: 'root', provider: 'physical', mode: 'compile', relation_kind: 'composition-wiring', direct_grant: true },
      { consumer: 'root', provider: 'consumer', mode: 'compile', relation_kind: 'composition-wiring', direct_grant: true },
      { consumer: 'root', provider: 'port', mode: 'compile', relation_kind: 'physical-port', direct_grant: true },
      { consumer: 'consumer', provider: 'physical', mode: 'injected', relation_kind: null, direct_grant: false },
    ],
  }
  assert.deepEqual(legal.localities[2].capability_facts[0].disposition.payload.authorities, [authority])
  assert.deepEqual(validateLayeringBlueprintV1(legal), [])
  const oldWorld = structuredClone(legal)
  oldWorld.dependencies.at(-1).mode = 'compile'
  assert.deepEqual(validateLayeringBlueprintV1(oldWorld), [{
    code: 'effect-reachable-from-non-composition',
    consumer_locality: 'consumer',
    provider_locality: 'physical',
  }])
}

export const assertPureContract = () => {
  const legal = {
    localities: [{
      id: 'contract',
      kind: 'contract',
      exposure: 'bounded',
      capability_facts: facts(pureFact('contract')),
    }],
    dependencies: [],
  }
  assert.deepEqual(validateLayeringBlueprintV1(legal), [])
  legal.localities[0].capability_facts.push(...facts(authorityFact('contract', 'process-control')))
  assert.deepEqual(validateLayeringBlueprintV1(legal), [{ code: 'invalid-contract-surface', locality_id: 'contract' }])
}

export const assertOptionalObservationNoninterference = async () => {
  const outcome = Object.freeze({ case: 'continue', payload: { ordinal: 7 } })
  assert.equal(await preserveOutcomeAcrossOptionalObservationV1(outcome, () => undefined), outcome)
  assert.equal(await preserveOutcomeAcrossOptionalObservationV1(outcome, () => { throw new Error('diagnostic failed') }), outcome)
  assert.equal(await preserveOutcomeAcrossOptionalObservationV1(outcome, async () => { throw new Error('async diagnostic failed') }), outcome)
}
