#!/usr/bin/env node

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { performance } from 'node:perf_hooks'

import { compareCanonicalTextV1, encodeCanonicalJsonV1, sha256BytesV1 } from '../lib/canonical-json-v1.mjs'
import { buildFreshMigrationWorksheetV1, validateMigrationWorksheetV1 } from '../lib/cutover-inputs-v1.mjs'
import { writeLoopDetectorEnvelopeArtifact } from '../lib/derive-loop-detector-envelope.mjs'
import { javascriptSourceIdV1 } from '../lib/capability-observations-v1.mjs'
import { analyzeLocalityDependencies } from '../lib/locality-dependencies.mjs'
import {
  buildLocalitySliceReportV1,
  buildLocalitySliceSummaryV1,
  EMPTY_AUTHORIZATION_PROJECTION_V2,
} from '../lib/locality-slice-report-v1.mjs'
import { extractProductionCapabilityFactsV1 } from '../lib/production-capability-extractor-v1.mjs'
import {
  readOwnerProjectInventoryV1,
  scanCompilerObservationsV1,
} from './locality-dependencies.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')
const WORKSHEET = join(ROOT, 'docs/OWNER-CONTRACT-SLICE-ADJUDICATION-WORKSHEET.json')

const sameTextSet = (left, right) => {
  const canonical = (values) => [...new Set(values)].sort(compareCanonicalTextV1)
  return encodeCanonicalJsonV1(canonical(left)) === encodeCanonicalJsonV1(canonical(right))
}

const countsBy = (values, keyOf) => Object.fromEntries(
  [...values.reduce((counts, value) => {
    const key = keyOf(value)
    counts.set(key, (counts.get(key) ?? 0) + 1)
    return counts
  }, new Map())].sort(([left], [right]) => compareCanonicalTextV1(left, right)),
)

const canonicalSite = (site, sourceLocalities) => ({
  locality_id: sourceLocalities.get(site.sourcePath),
  source_path: site.sourcePath,
  semantic_declaration_anchor: site.semanticDeclarationAnchor,
  same_anchor_occurrence_ordinal: site.sameAnchorOccurrenceOrdinal,
})

const localityBySource = (inventory) => new Map(inventory.localities.flatMap((locality) =>
  locality.sources.flatMap(({ implementationPath, signaturePath }) => [
    [implementationPath, locality.id],
    [signaturePath, locality.id],
  ])))

const observationSite = (site, sourceLocalities) => ({
  localityId: sourceLocalities.get(site.sourcePath),
  ...structuredClone(site),
})

const fableJavaScriptUnits = (compilerObservations, sourceLocalities) => compilerObservations.fableInterop
  .filter(({ kind }) => ['fable-emit', 'emit-js-expr'].includes(kind))
  .map(({ kind, expression, site }) => ({
    sourceKind: kind,
    sourceId: javascriptSourceIdV1(expression, canonicalSite(site, sourceLocalities)),
    sourceBytes: Buffer.from(expression, 'utf8'),
    observationSite: observationSite(site, sourceLocalities),
  }))

const generatedArtifactInputs = async (repositoryRoot, compilerObservations, sourceLocalities) => {
  let artifactBytes = null
  const result = await writeLoopDetectorEnvelopeArtifact(repositoryRoot, {
    writeArtifact: (_target, bytes) => { artifactBytes = Buffer.from(bytes) },
  })
  if (artifactBytes === null) throw new Error('loop detector generator produced no artifact bytes')
  const importObservation = compilerObservations.fableInterop.find((observation) =>
    observation.kind === 'fable-import'
    && observation.moduleSpecifier === result.generatedArtifact.linkage.import_specifier)
  if (!importObservation) throw new Error('generated loop detector artifact has no compiler-resolved import observation')
  return {
    artifacts: [result.generatedArtifact],
    units: [{
      sourceKind: 'generated-artifact',
      sourceId: result.generatedArtifact.id,
      sourceBytes: artifactBytes,
      observationSite: observationSite(importObservation.site, sourceLocalities),
    }],
  }
}

const canonicalLocalities = (inventory, repositoryRoot, readBytes) => inventory.localities.map((locality) => ({
  id: locality.id,
  owner: locality.owner,
  kind: locality.kind,
  project_path: locality.projectPath,
  sources: locality.sources.map((source) => ({
    implementation_path: source.implementationPath,
    implementation_digest: sha256BytesV1(readBytes(resolve(repositoryRoot, source.implementationPath))),
    signature_path: source.signaturePath,
    signature_digest: sha256BytesV1(readBytes(resolve(repositoryRoot, source.signaturePath))),
  })),
}))

const canonicalReferences = (inventory) => inventory.projectReferences.map((reference) => ({
  consumer_locality: reference.consumerLocality,
  provider_locality: reference.providerLocality,
}))

const canonicalSourceEdges = (analysis) => analysis.edges.map((edge) => ({
  consumer_locality: edge.consumerLocality,
  consumer_source: edge.consumerSource,
  provider_locality: edge.providerLocality,
  provider_source: edge.providerSource,
}))

export const scanProductionLocalitySliceReportV1 = async ({
  repositoryRoot = ROOT,
  sourceRoot = join(repositoryRoot, 'src/Wanxiangshu'),
  aggregate = join(sourceRoot, 'Wanxiangshu.fsproj'),
  readInventory = readOwnerProjectInventoryV1,
  scanCompiler = scanCompilerObservationsV1,
  analyzeDependencies = analyzeLocalityDependencies,
  deriveGeneratedArtifacts = generatedArtifactInputs,
  extractCapabilities = extractProductionCapabilityFactsV1,
  readBytes = readFileSync,
  observeStage = () => {},
  reportDetail = 'full',
} = {}) => {
  if (!['full', 'summary'].includes(reportDetail)) throw new TypeError('reportDetail must be full or summary')
  let stageStarted = performance.now()
  const completed = (stage, details = {}) => {
    const elapsedMilliseconds = Math.round(performance.now() - stageStarted)
    observeStage({ stage, elapsedMilliseconds, ...details })
    stageStarted = performance.now()
  }
  const inventory = readInventory({ sourceRoot, aggregate })
  completed('owner-inventory', { localityCount: inventory.localities.length })
  const compilerObservations = scanCompiler({ aggregate, productionRoot: sourceRoot })
  completed('compiler-observations', {
    fsharpNodeCount: compilerObservations.fsharpNodes.length,
    externalSymbolUseCount: compilerObservations.externalSymbolUses.length,
    interopCount: compilerObservations.fableInterop.length,
    signatureExportCount: compilerObservations.signatureExports.length,
    fsharpNodeKinds: countsBy(compilerObservations.fsharpNodes, ({ nodeKind }) => nodeKind),
    externalAssemblies: countsBy(compilerObservations.externalSymbolUses, ({ assembly }) => assembly),
  })
  if (!sameTextSet(inventory.productionFiles, compilerObservations.productionFiles)
    || !sameTextSet(inventory.signatureFiles, compilerObservations.signatureFiles)) {
    throw new Error('compiler observation source set does not equal owner project inventory')
  }
  const dependencyAnalysis = analyzeDependencies({
    localities: inventory.localities.map((locality) => ({
      id: locality.id,
      owner: locality.owner,
      sources: locality.sources.map(({ implementationPath }) => implementationPath),
      references: locality.references,
    })),
    declarationUses: compilerObservations.declarationUses,
  })
  completed('dependency-analysis', { sourceEdgeCount: dependencyAnalysis.edges.length })
  const sourceLocalities = localityBySource(inventory)
  const generated = await deriveGeneratedArtifacts(repositoryRoot, compilerObservations, sourceLocalities)
  completed('generated-artifacts', { artifactCount: generated.artifacts.length })
  const extraction = extractCapabilities({
    inventory,
    compilerObservations,
    javascriptUnits: [
      ...fableJavaScriptUnits(compilerObservations, sourceLocalities),
      ...generated.units,
    ],
    generatedArtifacts: generated.artifacts,
  }, { collectUnknownViolations: reportDetail === 'full' })
  completed('capability-extraction', {
    factCount: extraction.capabilityFacts.length,
    traversalCount: extraction.javascriptTraversals.length,
    diagnosticCount: extraction.diagnostics.length,
  })
  const world = {
    schema_version: 1,
    fact_schema_version: 1,
    observed: {
      localities: canonicalLocalities(inventory, repositoryRoot, readBytes),
      project_references: canonicalReferences(inventory),
      actual_source_edges: canonicalSourceEdges(dependencyAnalysis),
      generated_artifacts: generated.artifacts,
      javascript_traversals: extraction.javascriptTraversals,
      capability_extraction: extraction.capabilityExtraction,
      capability_facts: extraction.capabilityFacts,
    },
    normative: structuredClone(EMPTY_AUTHORIZATION_PROJECTION_V2),
  }
  const report = (reportDetail === 'full' ? buildLocalitySliceReportV1 : buildLocalitySliceSummaryV1)({
    world,
    findings: [...dependencyAnalysis.violations, ...extraction.violations],
  })
  completed('canonical-world', {
    findingCount: report.finding_count ?? report.findings.length,
    unknownCapabilityGroupCount: report.unknown_capability_census.group_count,
  })
  return {
    report,
    diagnostics: extraction.diagnostics,
  }
}

const argumentsV1 = (values) => {
  let writeFreshWorksheet = false
  let reportDetail = 'summary'
  for (const value of values) {
    if (value === '--write-fresh-worksheet') writeFreshWorksheet = true
    else if (value === '--full') reportDetail = 'full'
    else throw new Error('usage: node scripts/checks/locality-slice-report.mjs [--full] [--write-fresh-worksheet]')
  }
  return { writeFreshWorksheet, reportDetail }
}

export const writeFreshMigrationWorksheetFileV1 = ({
  report,
  worksheetPath = WORKSHEET,
  fileExists = existsSync,
  readText = (path) => readFileSync(path, 'utf8'),
  writeText = (path, text) => writeFileSync(path, text, 'utf8'),
}) => {
  if (fileExists(worksheetPath)) {
    const existing = JSON.parse(readText(worksheetPath))
    const violations = validateMigrationWorksheetV1(existing)
    if (violations.length > 0) throw new Error('existing migration worksheet is invalid')
    if (existing.records.some(({ status }) => status === 'decided')) {
      throw new Error('refusing to overwrite a migration worksheet containing decided records')
    }
  }
  const worksheet = buildFreshMigrationWorksheetV1(
    report.localities.map(({ locality_id: localityId, reasons }) => ({ locality_id: localityId, reasons })),
  )
  const violations = validateMigrationWorksheetV1(worksheet)
  if (violations.length > 0) throw new Error(`generated migration worksheet is invalid: ${encodeCanonicalJsonV1(violations)}`)
  writeText(worksheetPath, `${JSON.stringify(worksheet, null, 2)}\n`)
  return worksheet
}

const main = async () => {
  const options = argumentsV1(process.argv.slice(2))
  const result = await scanProductionLocalitySliceReportV1({
    observeStage: (stage) => process.stderr.write(`${encodeCanonicalJsonV1(stage)}\n`),
    reportDetail: options.reportDetail,
  })
  for (const diagnostic of result.diagnostics) process.stderr.write(`${encodeCanonicalJsonV1(diagnostic)}\n`)
  if (options.writeFreshWorksheet) writeFreshMigrationWorksheetFileV1({ report: result.report })
  process.stdout.write(encodeCanonicalJsonV1(result.report))
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    await main()
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`)
    process.exitCode = 1
  }
}
