import assert from 'node:assert/strict'
import test from 'node:test'

import {
  extractProductionCapabilityFactsV1,
} from '../../../scripts/lib/production-capability-extractor-v1.mjs'
import {
  javascriptSourceIdV1,
} from '../../../scripts/lib/capability-observations-v1.mjs'
import {
  buildGeneratedArtifactRowV1,
} from '../../../scripts/lib/generated-artifact-v1.mjs'

const compilerSite = (sourcePath, semanticDeclarationAnchor, sameAnchorOccurrenceOrdinal = 0) => ({
  sourcePath,
  semanticDeclarationAnchor,
  sameAnchorOccurrenceOrdinal,
})

const observationSite = (site) => ({
  localityId: 'fixture-contract',
  ...site,
})

const linkage = {
  import_specifier: '#fixture-generated',
  package_import_target: './dist/FixtureGenerated.js',
  generator_path: 'scripts/generate-fixture.mjs',
  generator_entry: 'writeFixture',
  input_selector_path: 'scripts/select-fixture-inputs.mjs',
  input_selector_entry: 'fixtureInputFiles',
  build_path: 'scripts/build.mjs',
  build_entry: 'verifyArtifacts',
}

const inventory = {
  aggregatePath: 'src/Wanxiangshu/Wanxiangshu.fsproj',
  productionFiles: ['src/Fixture.fs'],
  signatureFiles: ['src/Fixture.fsi'],
  localities: [{
    id: 'fixture-contract',
    owner: 'fixture',
    kind: 'contract',
    projectPath: 'src/Wanxiangshu/Wanxiangshu.Owner.fixture.fixture-contract.fsproj',
    sources: [{
      implementationPath: 'src/Fixture.fs',
      signaturePath: 'src/Fixture.fsi',
    }],
    references: [],
  }],
  projectReferences: [],
}

const compilerObservations = (fableInterop, diagnostics = []) => ({
  schemaVersion: 1,
  projectFile: 'src/Wanxiangshu/Wanxiangshu.fsproj',
  productionFiles: ['src/Fixture.fs'],
  signatureFiles: ['src/Fixture.fsi'],
  declarationUses: [],
  fsharpNodes: [{
    nodeKind: 'const',
    semanticIdentity: 'Fixture.answer',
    site: compilerSite('src/Fixture.fs', 'Fixture.answer'),
  }],
  externalSymbolUses: [{
    assembly: 'node',
    fullyQualifiedSymbol: 'node:path/posix.join',
    symbolKind: 'FSharpMemberOrFunctionOrValue',
    site: compilerSite('src/Fixture.fs', 'Fixture.path'),
  }],
  fableInterop,
  signatureExports: [{
    exportKind: 'pure-value',
    declarationIdentity: 'Fixture.answer',
    site: compilerSite('src/Fixture.fsi', 'Fixture.answer'),
  }],
  diagnostics,
  elapsedMilliseconds: 1,
})

const generated = () => {
  const artifactBytes = Buffer.from("import { join } from 'node:path/posix'\nexport { join }\nexport { readFile } from 'node:fs'\n", 'utf8')
  return {
    artifactBytes,
    artifact: buildGeneratedArtifactRowV1({
      artifact_path: 'dist/FixtureGenerated.js',
      artifact_bytes: artifactBytes,
      selected_inputs: [],
      linkage,
    }),
  }
}

const fixture = ({ expressions = [], generatedArtifact = generated() } = {}) => {
  const imports = [{
    kind: 'fable-import',
    moduleSpecifier: '#fixture-generated',
    selector: 'join',
    site: compilerSite('src/Fixture.fs', 'Fixture.generated'),
  }]
  const emits = expressions.map(({ expression, anchor, ordinal = 0, kind = 'fable-emit' }) => ({
    kind,
    expression,
    site: compilerSite('src/Fixture.fs', anchor, ordinal),
  }))
  const units = emits.map((row) => {
    const site = observationSite(row.site)
    const rawSite = {
      locality_id: site.localityId,
      source_path: site.sourcePath,
      semantic_declaration_anchor: site.semanticDeclarationAnchor,
      same_anchor_occurrence_ordinal: site.sameAnchorOccurrenceOrdinal,
    }
    return {
      sourceKind: row.kind,
      sourceId: javascriptSourceIdV1(row.expression, rawSite),
      sourceBytes: Buffer.from(row.expression, 'utf8'),
      observationSite: site,
    }
  })
  units.push({
    sourceKind: 'generated-artifact',
    sourceId: generatedArtifact.artifact.id,
    sourceBytes: generatedArtifact.artifactBytes,
    observationSite: observationSite(compilerSite('src/Fixture.fs', 'Fixture.generated')),
  })
  return {
    inventory: structuredClone(inventory),
    compilerObservations: compilerObservations([...imports, ...emits]),
    javascriptUnits: units,
    generatedArtifacts: [generatedArtifact.artifact],
  }
}

const javascriptFacts = (result) => result.capabilityFacts.filter(({ observation }) => observation.case === 'javascript-capability')

test('WHAT[STRUCTURED-WORKFLOW-014] production extractor derives scope-aware facts and same-run traversal evidence from raw source', () => {
  const freeSite = compilerSite('src/Fixture.fs', 'Fixture.freeEnvironment')
  const freeExpression = 'process.env'
  const nestedExpression = '(() => { let lastErr; lastErr = 1; return $0; })()'
  const shadowedExpression = '((process) => process.env)({})'
  const result = extractProductionCapabilityFactsV1(fixture({ expressions: [
    { expression: freeExpression, anchor: freeSite.semanticDeclarationAnchor },
    { expression: nestedExpression, anchor: 'Fixture.nestedScratch' },
    { expression: shadowedExpression, anchor: 'Fixture.shadowed' },
  ] }))

  assert.deepEqual(result.diagnostics, [])
  assert.deepEqual(result.violations, [])
  assert.equal(result.javascriptTraversals.length, 4)
  assert.equal(result.traversalObservationSets.length, 1)

  const rawSite = {
    locality_id: 'fixture-contract',
    source_path: freeSite.sourcePath,
    semantic_declaration_anchor: freeSite.semanticDeclarationAnchor,
    same_anchor_occurrence_ordinal: 0,
  }
  const expectedEnvironmentObservation = {
    case: 'javascript-capability',
    payload: {
      source_kind: 'fable-emit',
      source_id: javascriptSourceIdV1(freeExpression, rawSite),
      generated_artifact_id: null,
      javascript_observation: {
        kind: 'member-read',
        root: 'process',
        member_path: ['env'],
        binding_provenance: 'free',
      },
      site: rawSite,
    },
  }
  const environment = javascriptFacts(result).find(({ observation }) =>
    JSON.stringify(observation) === JSON.stringify(expectedEnvironmentObservation))
  assert.ok(environment)
  assert.deepEqual(environment.disposition.payload.authorities, ['environment'])

  const nestedFacts = javascriptFacts(result).filter(({ observation }) =>
    observation.payload.site.semantic_declaration_anchor === 'Fixture.nestedScratch')
  assert.equal(nestedFacts.some(({ observation }) => ['mutable-binding', 'member-write', 'update']
    .includes(observation.payload.javascript_observation.kind)), false)
  assert.equal(javascriptFacts(result).some(({ observation }) =>
    observation.payload.site.semantic_declaration_anchor === 'Fixture.shadowed'
    && observation.payload.javascript_observation.root === 'process'), false)

  const generatedFacts = javascriptFacts(result).filter(({ observation }) =>
    observation.payload.source_kind === 'generated-artifact')
  assert.deepEqual(generatedFacts.map(({ observation }) => observation.payload.javascript_observation), [
    {
      kind: 'static-import',
      root: 'node:fs',
      member_path: [],
      binding_provenance: 'imported',
    },
    {
      kind: 'static-import',
      root: 'node:path/posix',
      member_path: [],
      binding_provenance: 'imported',
    },
  ])
})

test('WHAT[STRUCTURED-WORKFLOW-014] production extractor distinguishes program mutation Date worlds and invalid JavaScript', () => {
  const topLevel = fixture({ expressions: [
    { expression: 'let cache = 0; cache++', anchor: 'Fixture.topLevel' },
    { expression: 'new Date()', anchor: 'Fixture.now' },
    { expression: 'new Date(0)', anchor: 'Fixture.epoch' },
    { expression: '(() => { const local = {}; return local[ambientKey]; })()', anchor: 'Fixture.dynamicMember' },
    { expression: '($0 ? (() => 1) : (() => 2))()', anchor: 'Fixture.dynamicCall' },
  ] })
  const result = extractProductionCapabilityFactsV1(topLevel)
  const facts = javascriptFacts(result)
  assert.ok(facts.some(({ observation, disposition }) =>
    observation.payload.site.semantic_declaration_anchor === 'Fixture.topLevel'
    && disposition.payload?.mutable_resources?.includes('top-level-mutable')))
  assert.ok(facts.some(({ observation, disposition }) =>
    observation.payload.site.semantic_declaration_anchor === 'Fixture.now'
    && disposition.payload?.authorities?.includes('clock')))
  assert.ok(facts.some(({ observation, disposition }) =>
    observation.payload.site.semantic_declaration_anchor === 'Fixture.epoch'
    && disposition.payload?.authorities?.length === 0
    && disposition.payload?.semantic_classes?.includes('pure-representation')))
  assert.ok(facts.some(({ observation, disposition }) =>
    observation.payload.site.semantic_declaration_anchor === 'Fixture.dynamicMember'
    && observation.payload.javascript_observation.root === 'ambientKey'
    && disposition.case === 'unknown'))
  assert.ok(facts.some(({ observation, disposition }) =>
    observation.payload.site.semantic_declaration_anchor === 'Fixture.dynamicCall'
    && observation.payload.javascript_observation.root === '<dynamic>'
    && disposition.case === 'unknown'))

  const malformed = fixture({ expressions: [{ expression: '(()', anchor: 'Fixture.invalid' }] })
  const malformedResult = extractProductionCapabilityFactsV1(malformed)
  assert.deepEqual(malformedResult.diagnostics.map(({ code, rawIdentity }) => ({ code, rawIdentity })), [{
    code: 'capability-extraction-incomplete',
    rawIdentity: `javascript-parse-failed:${malformed.javascriptUnits[0].sourceId}`,
  }])
  assert.deepEqual(malformedResult.violations.map(({ code }) => code), [
    'capability-extraction-incomplete',
    'unknown-capability-classification',
  ])
  assert.ok(malformedResult.capabilityFacts.some(({ observation, disposition }) =>
    observation.case === 'fable-emit'
    && observation.payload.javascript_traversal_id === null
    && disposition.case === 'unknown'
    && disposition.payload.unknown_class === 'unparsed-interop'))
})

test('WHAT[STRUCTURED-WORKFLOW-014] production extractor rejects caller mirrors stale units and artifact byte drift', () => {
  const legal = fixture({ expressions: [{ expression: 'Date.now()', anchor: 'Fixture.clock' }] })
  const mirrored = structuredClone(legal)
  mirrored.javascriptUnits[0].ast = { type: 'Program', body: [] }
  assert.deepEqual(extractProductionCapabilityFactsV1(mirrored).violations.map(({ code }) => code), [
    'capability-extraction-incomplete',
    'unknown-capability-classification',
  ])

  const stale = structuredClone(legal)
  stale.javascriptUnits.push({
    sourceKind: 'fable-emit',
    sourceId: 'stale',
    sourceBytes: Buffer.from('1'),
    observationSite: observationSite(compilerSite('src/Fixture.fs', 'Fixture.stale')),
  })
  assert.deepEqual(extractProductionCapabilityFactsV1(stale).diagnostics.map(({ code, rawIdentity }) => ({ code, rawIdentity })), [{
    code: 'capability-extraction-incomplete',
    rawIdentity: 'stale:fable-emit\0stale',
  }])

  const drift = structuredClone(legal)
  const generatedUnit = drift.javascriptUnits.find(({ sourceKind }) => sourceKind === 'generated-artifact')
  generatedUnit.sourceBytes = Buffer.from('export const changed = true\n')
  assert.deepEqual(extractProductionCapabilityFactsV1(drift).diagnostics.map(({ code, rawIdentity }) => ({ code, rawIdentity })), [{
    code: 'capability-extraction-incomplete',
    rawIdentity: `source-mismatch:${drift.generatedArtifacts[0].id}`,
  }])

  const unattributed = structuredClone(legal)
  unattributed.javascriptUnits.find(({ sourceKind }) => sourceKind === 'generated-artifact')
    .observationSite.semanticDeclarationAnchor = 'Fixture.unrelated'
  assert.deepEqual(extractProductionCapabilityFactsV1(unattributed).diagnostics.map(({ code, rawIdentity }) => ({ code, rawIdentity })), [{
    code: 'capability-extraction-incomplete',
    rawIdentity: `unattributed:${unattributed.generatedArtifacts[0].id}`,
  }])

  const malformedInventory = structuredClone(legal)
  malformedInventory.inventory.projectReferences = [null]
  assert.deepEqual(extractProductionCapabilityFactsV1(malformedInventory).diagnostics.map(({ code, rawIdentity }) => ({ code, rawIdentity })), [{
    code: 'capability-extraction-incomplete',
    rawIdentity: 'invalid-project-reference',
  }])

  const malformedSourceSet = structuredClone(legal)
  malformedSourceSet.compilerObservations.productionFiles = [null]
  assert.doesNotThrow(() => extractProductionCapabilityFactsV1(malformedSourceSet))
  assert.deepEqual(extractProductionCapabilityFactsV1(malformedSourceSet).violations.map(({ code }) => code), [
    'capability-extraction-incomplete',
  ])

  const diagnosticRow = {
    code: 'compiler-parser-diagnostic',
    sourcePath: 'src/Fixture.fs',
    semanticDeclarationAnchor: 'Fixture.clock',
    syntaxKind: 'future-node',
    line: 7,
    column: 9,
    rawIdentity: 'future-node',
  }
  const withDiagnostic = structuredClone(legal)
  withDiagnostic.compilerObservations.diagnostics = [diagnosticRow]
  const diagnosed = extractProductionCapabilityFactsV1(withDiagnostic)
  assert.deepEqual(
    diagnosed.capabilityFacts.map(({ fact_id: factId }) => factId),
    extractProductionCapabilityFactsV1(legal).capabilityFacts.map(({ fact_id: factId }) => factId),
  )
  assert.ok(diagnosed.violations.some(({ code }) => code === 'capability-extraction-incomplete'))
})
