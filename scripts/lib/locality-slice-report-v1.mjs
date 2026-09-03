import {
  projectCanonicalLocalityReportV1,
  projectCanonicalLocalitySummaryV1,
} from './locality-slice-world-v1.mjs'
import { compareCanonicalTextV1, encodeCanonicalJsonV1 } from './canonical-json-v1.mjs'

export const EMPTY_AUTHORIZATION_PROJECTION_V2 = Object.freeze({
  authorization_schema_version: 2,
  slices: Object.freeze([]),
  capability_relations: Object.freeze([]),
  generated_module_relations: Object.freeze([]),
})

const plainRecord = (value) => value !== null && typeof value === 'object' && !Array.isArray(value)

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

export const buildLocalitySliceReportV1 = ({ world: worldInput, findings = [] } = {}) => {
  const projection = projectCanonicalLocalityReportV1(worldInput)
  return {
    schema_version: 1,
    report_kind: 'm6.3b-report-only',
    canonical_world_digest: projection.canonical_world_digest,
    census: projection.census,
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
    finding_count: findingCounts.reduce((count, row) => count + row.count, 0),
    finding_counts: findingCounts,
    localities: projection.localities,
  }
}

export const serializeLocalitySliceReportV1 = (input) =>
  encodeCanonicalJsonV1(buildLocalitySliceReportV1(input))

export const serializeLocalitySliceSummaryV1 = (input) =>
  encodeCanonicalJsonV1(buildLocalitySliceSummaryV1(input))
