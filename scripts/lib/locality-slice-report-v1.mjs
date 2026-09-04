import {
  projectCanonicalLocalityReportV1,
  projectCanonicalLocalitySummaryV1,
} from './locality-slice-world-v1.mjs'
import {
  canonicalDigestV1,
  compareCanonicalTextV1,
  encodeCanonicalJsonV1,
} from './canonical-json-v1.mjs'

export const EMPTY_AUTHORIZATION_PROJECTION_V2 = Object.freeze({
  authorization_schema_version: 2,
  slices: Object.freeze([]),
  capability_relations: Object.freeze([]),
  generated_module_relations: Object.freeze([]),
})

const plainRecord = (value) => value !== null && typeof value === 'object' && !Array.isArray(value)

const UNKNOWN_CENSUS_SAMPLE_LIMIT = 5

const canonicalFindings = (findings) => {
  if (!Array.isArray(findings)) throw new TypeError('locality slice report findings must be an array')
  const rows = findings.map((finding) => {
    if (!plainRecord(finding) || typeof finding.code !== 'string' || finding.code.length === 0) {
      throw new TypeError('locality slice report finding must be an object with a non-empty code')
    }
    encodeCanonicalJsonV1(finding)
    return structuredClone(finding)
  })
  return rows.sort((left, right) => compareCanonicalTextV1(
    encodeCanonicalJsonV1(left),
    encodeCanonicalJsonV1(right),
  ))
}

const summarizeFindings = (findings) => {
  if (!Array.isArray(findings)) throw new TypeError('locality slice report findings must be an array')
  const counts = new Map()
  for (const finding of findings) {
    if (!plainRecord(finding) || typeof finding.code !== 'string' || finding.code.length === 0) {
      throw new TypeError('locality slice report finding must be an object with a non-empty code')
    }
    encodeCanonicalJsonV1(finding)
    counts.set(finding.code, (counts.get(finding.code) ?? 0) + 1)
  }
  return [...counts]
    .sort(([left], [right]) => compareCanonicalTextV1(left, right))
    .map(([code, count]) => ({ code, count }))
}

const unknownGroupIdentity = ({ observation_case: observationCase, unknown_class: unknownClass, syntax_kind: syntaxKind, raw_identity: rawIdentity }) =>
  encodeCanonicalJsonV1({ observation_case: observationCase, unknown_class: unknownClass, syntax_kind: syntaxKind, raw_identity: rawIdentity })

const projectUnknownCapabilityCensus = (worldInput, expectedUnknownCount) => {
  const groups = new Map()
  const seenUnknownObservations = new Set()
  for (const fact of worldInput.observed.capability_facts) {
    if (fact.disposition.case !== 'unknown' || seenUnknownObservations.has(fact.observation_id)) continue
    seenUnknownObservations.add(fact.observation_id)
    const { unknown_class: unknownClass, syntax_kind: syntaxKind, raw_identity: rawIdentity } = fact.disposition.payload
    const identity = {
      observation_case: fact.observation.case,
      unknown_class: unknownClass,
      syntax_kind: syntaxKind,
      raw_identity: rawIdentity,
    }
    const key = unknownGroupIdentity(identity)
    if (!groups.has(key)) groups.set(key, { ...identity, fact_count: 0, localities: new Set(), sources: new Set() })
    const group = groups.get(key)
    group.fact_count += 1
    group.localities.add(fact.observation.payload.site.locality_id)
    group.sources.add(fact.observation.payload.site.source_path)
  }
  const rows = [...groups.values()]
    .sort((left, right) => compareCanonicalTextV1(unknownGroupIdentity(left), unknownGroupIdentity(right)))
    .map(({ localities: localitySet, sources: sourceSet, ...group }) => {
      const localities = [...localitySet].sort(compareCanonicalTextV1)
      const sources = [...sourceSet].sort(compareCanonicalTextV1)
      return {
        ...group,
        affected_locality_count: localities.length,
        affected_locality_digest: canonicalDigestV1('unknown-capability-census/localities/v1\0', localities),
        affected_source_count: sources.length,
        affected_source_digest: canonicalDigestV1('unknown-capability-census/sources/v1\0', sources),
        representative_localities: localities.slice(0, UNKNOWN_CENSUS_SAMPLE_LIMIT),
        representative_sources: sources.slice(0, UNKNOWN_CENSUS_SAMPLE_LIMIT),
      }
    })
  const unknownFactCount = rows.reduce((count, group) => count + group.fact_count, 0)
  if (unknownFactCount !== expectedUnknownCount) {
    throw new TypeError('report-only Unknown census does not equal canonical unknown count')
  }
  return {
    schema_version: 1,
    census_kind: 'm6.3b-report-only-unknown-capability-census',
    sample_limit: UNKNOWN_CENSUS_SAMPLE_LIMIT,
    unknown_fact_count: unknownFactCount,
    group_count: rows.length,
    groups_digest: canonicalDigestV1('unknown-capability-census/groups/v1\0', rows),
    groups: rows,
  }
}

export const buildLocalitySliceReportV1 = ({ world: worldInput, findings = [] } = {}) => {
  const projection = projectCanonicalLocalityReportV1(worldInput)
  return {
    schema_version: 1,
    report_kind: 'm6.3b-report-only',
    canonical_world_digest: projection.canonical_world_digest,
    census: projection.census,
    unknown_capability_census: projectUnknownCapabilityCensus(worldInput, projection.census.unknown_capability_count),
    findings: canonicalFindings(findings),
    localities: projection.localities,
  }
}

export const buildLocalitySliceSummaryV1 = ({ world: worldInput, findings = [] } = {}) => {
  const projection = projectCanonicalLocalitySummaryV1(worldInput)
  const findingCounts = summarizeFindings(findings)
  return {
    schema_version: 1,
    report_kind: 'm6.3b-report-only-summary',
    canonical_world_digest: projection.canonical_world_digest,
    census: projection.census,
    unknown_capability_census: projectUnknownCapabilityCensus(worldInput, projection.census.unknown_capability_count),
    finding_count: findingCounts.reduce((count, row) => count + row.count, 0),
    finding_counts: findingCounts,
    localities: projection.localities,
  }
}

export const serializeLocalitySliceReportV1 = (input) =>
  encodeCanonicalJsonV1(buildLocalitySliceReportV1(input))

export const serializeLocalitySliceSummaryV1 = (input) =>
  encodeCanonicalJsonV1(buildLocalitySliceSummaryV1(input))
