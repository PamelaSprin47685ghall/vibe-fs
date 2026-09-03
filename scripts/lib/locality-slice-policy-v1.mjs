import { compareCanonicalTextV1 } from './canonical-json-v1.mjs'
import {
  capabilityDispositionViolatesContractV1,
  validateCanonicalCapabilityFactV1,
} from './capability-observations-v1.mjs'

const coordinates = (violation) => Object.entries(violation)
  .filter(([key]) => key !== 'code')
  .map(([key, value]) => `${key}\0${value}`)
  .join('\0')

const sortViolations = (violations) => violations.sort((left, right) =>
  compareCanonicalTextV1(`${left.code}\0${coordinates(left)}`, `${right.code}\0${coordinates(right)}`))

const isObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value)

const hasExactKeys = (value, keys) => {
  if (!isObject(value)) return false
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

const isNonEmptyText = (value) => typeof value === 'string' && value.length > 0

const isUniqueTextArray = (value) => Array.isArray(value)
  && value.every(isNonEmptyText)
  && new Set(value).size === value.length

const localitySchemaIsValid = (locality) => {
  const includesExposure = Object.hasOwn(locality ?? {}, 'exposure')
  if (!hasExactKeys(locality, [
    'id',
    'kind',
    ...(includesExposure ? ['exposure'] : []),
    'capability_facts',
  ])) return false
  const exposureIsValid = locality.kind === 'contract'
    ? ['shared', 'bounded'].includes(locality.exposure)
    : ['runtime', 'adapter'].includes(locality.kind)
      ? locality.exposure === undefined || locality.exposure === 'effect'
      : locality.exposure === undefined
  return isNonEmptyText(locality.id)
    && ['contract', 'runtime', 'adapter', 'composition'].includes(locality.kind)
    && exposureIsValid
    && Array.isArray(locality.capability_facts)
    && locality.capability_facts.every(validateCanonicalCapabilityFactV1)
}

const dependencySchemaIsValid = (dependency) => hasExactKeys(dependency, [
  'consumer',
  'provider',
  'mode',
  'relation_kind',
  'direct_grant',
])
  && isNonEmptyText(dependency.consumer)
  && isNonEmptyText(dependency.provider)
  && ['compile', 'injected'].includes(dependency.mode)
  && (dependency.relation_kind === null || ['physical-port', 'adapter', 'composition-wiring'].includes(dependency.relation_kind))
  && typeof dependency.direct_grant === 'boolean'

const carriesEffect = (locality) => locality.capability_facts
  .some(({ disposition }) => capabilityDispositionViolatesContractV1(disposition))

const contractIsPure = (locality) => !carriesEffect(locality)

const publishesCapabilityType = (locality) => locality.capability_facts.some(({ disposition }) =>
  disposition.case === 'classified'
  && disposition.payload.semantic_classes.includes('capability-type-only'))

const compileReachableProviders = (consumer, compileProvidersByConsumer) => {
  const visited = new Set()
  const pending = [...(compileProvidersByConsumer.get(consumer) ?? [])]
  while (pending.length > 0) {
    const provider = pending.shift()
    if (visited.has(provider)) continue
    visited.add(provider)
    pending.push(...(compileProvidersByConsumer.get(provider) ?? []))
  }
  return visited
}

export const validateLayeringBlueprintV1 = (blueprint) => {
  if (!hasExactKeys(blueprint, ['localities', 'dependencies'])
    || !Array.isArray(blueprint.localities)
    || !Array.isArray(blueprint.dependencies)) {
    return [{ code: 'locality-slice-policy-schema', path: '$' }]
  }

  const localities = new Map()
  for (let index = 0; index < blueprint.localities.length; index += 1) {
    const locality = blueprint.localities[index]
    if (!localitySchemaIsValid(locality)) {
      return [{ code: 'locality-slice-policy-schema', path: `$.localities[${index}]` }]
    }
    if (localities.has(locality.id)) {
      return [{ code: 'locality-slice-policy-schema', path: `$.localities[${index}].id` }]
    }
    localities.set(locality.id, locality)
  }

  const compileProvidersByConsumer = new Map()
  for (let index = 0; index < blueprint.dependencies.length; index += 1) {
    const dependency = blueprint.dependencies[index]
    if (!dependencySchemaIsValid(dependency)
      || !localities.has(dependency.consumer)
      || !localities.has(dependency.provider)) {
      return [{ code: 'locality-slice-policy-schema', path: `$.dependencies[${index}]` }]
    }
    if (dependency.mode === 'compile') {
      const providers = compileProvidersByConsumer.get(dependency.consumer) ?? []
      providers.push(dependency.provider)
      compileProvidersByConsumer.set(dependency.consumer, providers)
    }
  }

  const violations = []
  for (const locality of localities.values()) {
    if (locality.kind === 'contract' && !contractIsPure(locality)) {
      violations.push({ code: 'invalid-contract-surface', locality_id: locality.id })
    }
    if (locality.kind === 'composition') continue
    for (const providerId of compileReachableProviders(locality.id, compileProvidersByConsumer)) {
      const provider = localities.get(providerId)
      if (carriesEffect(provider)) {
        violations.push({
          code: 'effect-reachable-from-non-composition',
          consumer_locality: locality.id,
          provider_locality: provider.id,
        })
      }
    }
  }
  for (const dependency of blueprint.dependencies) {
    if (dependency.mode !== 'compile' || dependency.relation_kind !== 'physical-port') continue
    const provider = localities.get(dependency.provider)
    if (provider.kind !== 'contract' || !contractIsPure(provider) || !publishesCapabilityType(provider)) {
      violations.push({
        code: 'invalid-physical-port-surface',
        consumer_locality: dependency.consumer,
        provider_locality: dependency.provider,
      })
    }
  }

  return sortViolations(violations)
}

const fatalBoundarySchemaIsValid = (descriptor) => hasExactKeys(descriptor, [
  'owner',
  'incident',
  'settlement',
  'capability_binding',
  'physical_dependency',
  'report_owner_count',
  'kill_owner_count',
  'already_handled',
])
  && isNonEmptyText(descriptor.owner)
  && hasExactKeys(descriptor.incident, ['id', 'owner'])
  && isNonEmptyText(descriptor.incident.id)
  && descriptor.incident.owner === descriptor.owner
  && Number.isSafeInteger(descriptor.report_owner_count)
  && descriptor.report_owner_count >= 0
  && Number.isSafeInteger(descriptor.kill_owner_count)
  && descriptor.kill_owner_count >= 0
  && typeof descriptor.already_handled === 'boolean'

export const validateFatalBoundaryV1 = (descriptor) => {
  if (!fatalBoundarySchemaIsValid(descriptor)) return [{ code: 'fatal-boundary-schema', path: '$' }]
  const violations = []
  if (descriptor.capability_binding !== 'mandatory-injected'
    || descriptor.report_owner_count === 0
    || descriptor.kill_owner_count === 0) {
    violations.push({ code: 'fatal-capability-not-mandatory', owner: descriptor.owner })
  }
  if (descriptor.physical_dependency !== 'none') {
    violations.push({ code: 'fatal-direct-physical-dependency', owner: descriptor.owner })
  }
  if (descriptor.already_handled || descriptor.report_owner_count > 1 || descriptor.kill_owner_count > 1) {
    violations.push({ code: 'fatal-incident-duplicate', incident_id: descriptor.incident.id })
  }
  if (!['committed', 'unknown', 'not-required'].includes(descriptor.settlement)) {
    violations.push({ code: 'fatal-before-settlement', incident_id: descriptor.incident.id })
  }
  return sortViolations(violations)
}

export const preserveOutcomeAcrossOptionalObservationV1 = async (outcome, observe) => {
  try {
    await observe()
  } catch {
    return outcome
  }
  return outcome
}
