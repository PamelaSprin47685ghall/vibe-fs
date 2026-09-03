import { parse, parseExpressionAt, tokenizer } from 'acorn'

import {
  compareCanonicalTextV1,
  encodeCanonicalJsonV1,
  sha256BytesV1,
} from './canonical-json-v1.mjs'
import {
  enumerateJavaScriptAstNodesV1,
  extractObservedCapabilityFactsV1,
  javascriptSourceIdV1,
  javascriptTraversalIdV1,
  projectJavaScriptCapabilityObservationsV1,
  validateJavaScriptTraversalV1,
  visitJavaScriptNodeV1,
} from './capability-observations-v1.mjs'
import { createJavaScriptScopeResolverV1 } from './javascript-scope-v1.mjs'
import { walkSyntax } from './js-syntax.mjs'
import { generatedArtifactIdV1 } from './generated-artifact-v1.mjs'

const exactKeys = (value, keys) => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

const text = (value) => typeof value === 'string' && value.length > 0
const integer = (value) => Number.isSafeInteger(value) && value >= 0
const bytes = (value) => Buffer.isBuffer(value) || value instanceof Uint8Array
const asBytes = (value) => Buffer.isBuffer(value)
  ? Buffer.from(value)
  : Buffer.from(value.buffer, value.byteOffset, value.byteLength)

const compilerSiteValid = (site) => exactKeys(site, [
  'sourcePath',
  'semanticDeclarationAnchor',
  'sameAnchorOccurrenceOrdinal',
])
  && text(site.sourcePath)
  && text(site.semanticDeclarationAnchor)
  && integer(site.sameAnchorOccurrenceOrdinal)

const observationSiteValid = (site) => exactKeys(site, [
  'localityId',
  'sourcePath',
  'semanticDeclarationAnchor',
  'sameAnchorOccurrenceOrdinal',
])
  && text(site.localityId)
  && compilerSiteValid({
    sourcePath: site.sourcePath,
    semanticDeclarationAnchor: site.semanticDeclarationAnchor,
    sameAnchorOccurrenceOrdinal: site.sameAnchorOccurrenceOrdinal,
  })

const rawSite = (site, localityId) => ({
  locality_id: localityId,
  source_path: site.sourcePath,
  semantic_declaration_anchor: site.semanticDeclarationAnchor,
  same_anchor_occurrence_ordinal: site.sameAnchorOccurrenceOrdinal,
})

const diagnosticValid = (row) => exactKeys(row, [
  'code',
  'sourcePath',
  'semanticDeclarationAnchor',
  'syntaxKind',
  'line',
  'column',
  'rawIdentity',
])
  && [row.code, row.syntaxKind, row.rawIdentity].every(text)
  && (row.sourcePath === null || text(row.sourcePath))
  && (row.semanticDeclarationAnchor === null || text(row.semanticDeclarationAnchor))
  && integer(row.line)
  && integer(row.column)

const diagnostic = (code, site, syntaxKind, rawIdentity) => ({
  code,
  sourcePath: site?.sourcePath ?? '<production-capability-extractor>',
  semanticDeclarationAnchor: site?.semanticDeclarationAnchor ?? '<input>',
  syntaxKind,
  line: 0,
  column: 0,
  rawIdentity,
})

const inventoryIndex = (inventory, diagnostics) => {
  if (!exactKeys(inventory, [
    'aggregatePath',
    'productionFiles',
    'signatureFiles',
    'localities',
    'projectReferences',
  ])
    || !text(inventory.aggregatePath)
    || !Array.isArray(inventory.productionFiles)
    || !inventory.productionFiles.every(text)
    || !Array.isArray(inventory.signatureFiles)
    || !inventory.signatureFiles.every(text)
    || !Array.isArray(inventory.localities)
    || !Array.isArray(inventory.projectReferences)) {
    diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'inventory', 'invalid-inventory-shape'))
    return new Map()
  }
  const localityBySource = new Map()
  const localityIds = new Set()
  const validLocalities = []
  const validProjectReferences = []
  for (const locality of inventory.localities) {
    if (!exactKeys(locality, ['id', 'owner', 'kind', 'projectPath', 'sources', 'references'])
      || ![locality.id, locality.owner, locality.kind, locality.projectPath].every(text)
      || !Array.isArray(locality.sources)
      || !Array.isArray(locality.references)
      || !locality.references.every(text)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'inventory-locality', 'invalid-locality-row'))
      continue
    }
    validLocalities.push(locality)
    if (localityIds.has(locality.id)) diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'inventory-locality', locality.id))
    localityIds.add(locality.id)
    for (const source of locality.sources) {
      if (!exactKeys(source, ['implementationPath', 'signaturePath'])
        || !text(source.implementationPath)
        || !text(source.signaturePath)) {
        diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'inventory-source', locality.id))
        continue
      }
      for (const sourcePath of [source.implementationPath, source.signaturePath]) {
        if (localityBySource.has(sourcePath)) diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'inventory-source', sourcePath))
        localityBySource.set(sourcePath, locality.id)
      }
    }
  }
  for (const reference of inventory.projectReferences) {
    if (!exactKeys(reference, ['consumerLocality', 'providerLocality'])
      || !text(reference.consumerLocality)
      || !text(reference.providerLocality)
      || !localityIds.has(reference.consumerLocality)
      || !localityIds.has(reference.providerLocality)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'inventory-reference', 'invalid-project-reference'))
      continue
    }
    validProjectReferences.push(reference)
  }
  const projectedReferences = validLocalities
    .flatMap((locality) => Array.isArray(locality?.references)
      ? locality.references.map((providerLocality) => ({ consumerLocality: locality.id, providerLocality }))
      : [])
    .sort((left, right) => compareCanonicalTextV1(
      `${left.consumerLocality}\0${left.providerLocality}`,
      `${right.consumerLocality}\0${right.providerLocality}`,
    ))
  const canonicalReferences = validProjectReferences.sort((left, right) => compareCanonicalTextV1(
    `${left.consumerLocality}\0${left.providerLocality}`,
    `${right.consumerLocality}\0${right.providerLocality}`,
  ))
  if (encodeCanonicalJsonV1(projectedReferences) !== encodeCanonicalJsonV1(canonicalReferences)) {
    diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'inventory-reference', 'project-reference-set-mismatch'))
  }
  return localityBySource
}

const compilerObservationsValid = (value) => exactKeys(value, [
  'schemaVersion',
  'projectFile',
  'productionFiles',
  'signatureFiles',
  'declarationUses',
  'fsharpNodes',
  'externalSymbolUses',
  'fableInterop',
  'signatureExports',
  'diagnostics',
  'elapsedMilliseconds',
])
  && value.schemaVersion === 1
  && text(value.projectFile)
  && integer(value.elapsedMilliseconds)
  && ['productionFiles', 'signatureFiles', 'declarationUses', 'fsharpNodes', 'externalSymbolUses', 'fableInterop', 'signatureExports', 'diagnostics']
    .every((key) => Array.isArray(value[key]))

const interopValid = (row) => {
  if (row?.kind === 'fable-import') {
    return exactKeys(row, ['kind', 'moduleSpecifier', 'selector', 'site'])
      && [row.moduleSpecifier, row.selector].every(text)
      && compilerSiteValid(row.site)
  }
  if (row?.kind === 'fable-emit' || row?.kind === 'emit-js-expr') {
    return exactKeys(row, ['kind', 'expression', 'site'])
      && text(row.expression)
      && compilerSiteValid(row.site)
  }
  return false
}

const declarationUseValid = (row) => exactKeys(row, [
  'consumerPath',
  'providerPaths',
  'symbol',
  'symbolKind',
  'assembly',
  'isNamespace',
  'isModule',
  'line',
  'column',
  'isFromOpenStatement',
  'isFromPattern',
  'isFromType',
  'isFromUse',
])
  && [row.consumerPath, row.symbol, row.symbolKind, row.assembly].every(text)
  && Array.isArray(row.providerPaths)
  && row.providerPaths.every(text)
  && ['isNamespace', 'isModule', 'isFromOpenStatement', 'isFromPattern', 'isFromType', 'isFromUse']
    .every((key) => typeof row[key] === 'boolean')
  && integer(row.line)
  && integer(row.column)

const linkageValid = (linkage) => exactKeys(linkage, [
  'import_specifier',
  'package_import_target',
  'generator_path',
  'generator_entry',
  'input_selector_path',
  'input_selector_entry',
  'build_path',
  'build_entry',
]) && Object.values(linkage).every(text)

const generatedArtifactValid = (row) => exactKeys(row, [
  'id',
  'artifact_path',
  'artifact_digest',
  'selected_inputs_digest',
  'linkage',
  'javascript_traversal_id',
])
  && [row.id, row.artifact_path, row.artifact_digest, row.selected_inputs_digest, row.javascript_traversal_id].every(text)
  && row.javascript_traversal_id === javascriptTraversalIdV1('generated-artifact', row.id)
  && linkageValid(row.linkage)
  && row.id === generatedArtifactIdV1(row)
  && /^sha256:[0-9a-f]{64}$/.test(row.artifact_digest)
  && /^sha256:[0-9a-f]{64}$/.test(row.selected_inputs_digest)

const decode = (value) => new TextDecoder('utf-8', { fatal: true }).decode(asBytes(value))

const parseExpression = (source) => {
  const ast = parseExpressionAt(source, 0, { ecmaVersion: 'latest', sourceType: 'module' })
  const trailing = [...tokenizer(source, { ecmaVersion: 'latest', sourceType: 'module' })]
    .filter((token) => token.start >= ast.end && token.type.label !== ';')
  if (trailing.length > 0) throw new SyntaxError('Fable interop contains trailing JavaScript syntax')
  return ast
}

const parseUnit = (source, sourceKind) => sourceKind === 'generated-artifact'
  ? parse(source, { allowHashBang: true, ecmaVersion: 'latest', sourceType: 'module' })
  : (() => {
      try {
        return parseExpression(source)
      } catch {
        return parse(source, { allowHashBang: true, ecmaVersion: 'latest', sourceType: 'module' })
      }
    })()

const preboundFableHoles = (ast) => {
  const result = new Set()
  walkSyntax(ast, (node) => {
    if (node.type === 'Identifier' && /^\$\d+$/.test(node.name)) result.add(node.name)
  })
  return [...result].sort(compareCanonicalTextV1)
}

const sourceKey = (sourceKind, sourceId) => `${sourceKind}\0${sourceId}`

export const extractProductionCapabilityFactsV1 = (input, { collectUnknownViolations = true } = {}) => {
  const diagnostics = []
  const empty = () => ({
    capabilityFacts: [],
    capabilityExtraction: null,
    javascriptTraversals: [],
    traversalObservationSets: [],
    diagnostics,
    violations: [{ code: 'capability-extraction-incomplete' }],
  })
  if (!exactKeys(input, ['inventory', 'compilerObservations', 'javascriptUnits', 'generatedArtifacts'])
    || !Array.isArray(input?.javascriptUnits)
    || !Array.isArray(input?.generatedArtifacts)
    || !compilerObservationsValid(input?.compilerObservations)) {
    diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'extractor-input', 'invalid-input-shape'))
    return empty()
  }

  const localityBySource = inventoryIndex(input.inventory, diagnostics)
  const compiler = input.compilerObservations
  if (!compiler.productionFiles.every(text)
    || !compiler.signatureFiles.every(text)
    || !compiler.declarationUses.every(declarationUseValid)
    || !compiler.diagnostics.every(diagnosticValid)
    || new Set(compiler.productionFiles).size !== compiler.productionFiles.length
    || new Set(compiler.signatureFiles).size !== compiler.signatureFiles.length) {
    diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'compiler-observations', 'invalid-compiler-output'))
  }
  diagnostics.push(...compiler.diagnostics.filter(diagnosticValid))
  const inventoryLocalities = Array.isArray(input.inventory?.localities) ? input.inventory.localities : []
  const implementationPaths = inventoryLocalities
    .flatMap((locality) => Array.isArray(locality?.sources) ? locality.sources.map((source) => source?.implementationPath).filter(text) : [])
    .sort(compareCanonicalTextV1)
  const productionFiles = compiler.productionFiles.filter(text).sort(compareCanonicalTextV1)
  const inventoryProductionFiles = Array.isArray(input.inventory?.productionFiles)
    ? input.inventory.productionFiles.filter(text).sort(compareCanonicalTextV1)
    : []
  if (encodeCanonicalJsonV1(implementationPaths) !== encodeCanonicalJsonV1(productionFiles)
    || encodeCanonicalJsonV1(inventoryProductionFiles) !== encodeCanonicalJsonV1(productionFiles)) {
    diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'compiler-production-files', 'inventory-production-set-mismatch'))
  }
  const signaturePaths = inventoryLocalities
    .flatMap((locality) => Array.isArray(locality?.sources) ? locality.sources.map((source) => source?.signaturePath).filter(text) : [])
    .sort(compareCanonicalTextV1)
  const compilerSignatureFiles = compiler.signatureFiles.filter(text).sort(compareCanonicalTextV1)
  const inventorySignatureFiles = Array.isArray(input.inventory?.signatureFiles)
    ? input.inventory.signatureFiles.filter(text).sort(compareCanonicalTextV1)
    : []
  if (encodeCanonicalJsonV1(signaturePaths) !== encodeCanonicalJsonV1(compilerSignatureFiles)
    || encodeCanonicalJsonV1(inventorySignatureFiles) !== encodeCanonicalJsonV1(compilerSignatureFiles)) {
    diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'compiler-signature-files', 'inventory-signature-set-mismatch'))
  }

  const artifactsById = new Map()
  const artifactsBySpecifier = new Map()
  for (const artifact of input.generatedArtifacts) {
    if (!generatedArtifactValid(artifact) || artifactsById.has(artifact?.id)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'generated-artifact', artifact?.id ?? 'invalid-artifact'))
      continue
    }
    artifactsById.set(artifact.id, artifact)
    const specifier = artifact.linkage?.import_specifier
    if (!text(specifier)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'generated-artifact-linkage', artifact.id))
      continue
    }
    const matches = artifactsBySpecifier.get(specifier) ?? []
    matches.push(artifact)
    artifactsBySpecifier.set(specifier, matches)
  }

  const unitsBySource = new Map()
  for (const unit of input.javascriptUnits) {
    if (!exactKeys(unit, ['sourceKind', 'sourceId', 'sourceBytes', 'observationSite'])
      || !['fable-emit', 'emit-js-expr', 'generated-artifact'].includes(unit.sourceKind)
      || !text(unit.sourceId)
      || !bytes(unit.sourceBytes)
      || !observationSiteValid(unit.observationSite)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'javascript-unit', unit?.sourceId ?? 'invalid-unit'))
      continue
    }
    const key = sourceKey(unit.sourceKind, unit.sourceId)
    const matches = unitsBySource.get(key) ?? []
    matches.push(unit)
    unitsBySource.set(key, matches)
  }

  const rawObservations = []
  const expectedUnitKeys = new Set()
  const localitySite = (site, syntaxKind) => {
    if (!compilerSiteValid(site) || !localityBySource.has(site.sourcePath)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', site, syntaxKind, 'unowned-observation-site'))
      return null
    }
    return rawSite(site, localityBySource.get(site.sourcePath))
  }

  for (const row of compiler.fsharpNodes) {
    if (!exactKeys(row, ['nodeKind', 'semanticIdentity', 'site'])
      || ![row.nodeKind, row.semanticIdentity].every(text)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', row?.site, 'fsharp-node', 'invalid-fsharp-node'))
      continue
    }
    const site = localitySite(row.site, 'fsharp-node')
    if (site) rawObservations.push({ case: 'fsharp-node', payload: { node_kind: row.nodeKind, semantic_identity: row.semanticIdentity, site } })
  }
  for (const row of compiler.externalSymbolUses) {
    if (!exactKeys(row, ['assembly', 'fullyQualifiedSymbol', 'symbolKind', 'site'])
      || ![row.assembly, row.fullyQualifiedSymbol, row.symbolKind].every(text)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', row?.site, 'fcs-external-symbol-use', 'invalid-external-symbol-use'))
      continue
    }
    const site = localitySite(row.site, 'fcs-external-symbol-use')
    if (site) rawObservations.push({ case: 'fcs-external-symbol-use', payload: { assembly: row.assembly, fully_qualified_symbol: row.fullyQualifiedSymbol, site } })
  }
  for (const row of compiler.signatureExports) {
    if (!exactKeys(row, ['exportKind', 'declarationIdentity', 'site'])
      || ![row.exportKind, row.declarationIdentity].every(text)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', row?.site, 'public-signature-export', 'invalid-signature-export'))
      continue
    }
    const site = localitySite(row.site, 'public-signature-export')
    if (site) rawObservations.push({ case: 'public-signature-export', payload: { export_kind: row.exportKind, declaration_identity: row.declarationIdentity, site } })
  }

  const interopSources = []
  for (const row of compiler.fableInterop) {
    if (!interopValid(row)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', row?.site, row?.kind ?? 'fable-interop', 'invalid-fable-interop'))
      continue
    }
    const site = localitySite(row.site, row.kind)
    if (!site) continue
    if (row.kind === 'fable-import') {
      const artifacts = artifactsBySpecifier.get(row.moduleSpecifier) ?? []
      if (artifacts.length > 1) diagnostics.push(diagnostic('capability-extraction-incomplete', row.site, row.kind, 'ambiguous-generated-artifact'))
      rawObservations.push({
        case: 'fable-import',
        payload: {
          module_specifier: row.moduleSpecifier,
          selector: row.selector,
          generated_artifact_id: artifacts.length === 1 ? artifacts[0].id : null,
          site,
        },
      })
      continue
    }
    const sourceId = javascriptSourceIdV1(row.expression, site)
    const key = sourceKey(row.kind, sourceId)
    expectedUnitKeys.add(key)
    const units = unitsBySource.get(key) ?? []
    let parseable = units.length === 1
    let source = ''
    if (units.length !== 1) diagnostics.push(diagnostic('capability-extraction-incomplete', row.site, row.kind, units.length === 0 ? 'javascript-unit-missing' : 'javascript-unit-duplicate'))
    if (parseable) {
      const unit = units[0]
      try {
        source = decode(unit.sourceBytes)
      } catch {
        parseable = false
        diagnostics.push(diagnostic('capability-extraction-incomplete', row.site, row.kind, 'javascript-source-not-utf8'))
      }
      if (source !== row.expression || encodeCanonicalJsonV1(unit.observationSite) !== encodeCanonicalJsonV1({
        localityId: site.locality_id,
        sourcePath: site.source_path,
        semanticDeclarationAnchor: site.semantic_declaration_anchor,
        sameAnchorOccurrenceOrdinal: site.same_anchor_occurrence_ordinal,
      })) {
        parseable = false
        diagnostics.push(diagnostic('capability-extraction-incomplete', row.site, row.kind, 'javascript-unit-source-mismatch'))
      }
    }
    rawObservations.push({
      case: row.kind,
      payload: {
        expression: row.expression,
        javascript_traversal_id: parseable ? javascriptTraversalIdV1(row.kind, sourceId) : null,
        site,
      },
    })
    if (parseable) interopSources.push({ sourceKind: row.kind, sourceId, source, site, unit: units[0] })
  }

  for (const artifact of artifactsById.values()) {
    const key = sourceKey('generated-artifact', artifact.id)
    expectedUnitKeys.add(key)
    const units = unitsBySource.get(key) ?? []
    if (units.length !== 1) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'generated-artifact', units.length === 0 ? artifact.id : `duplicate:${artifact.id}`))
      continue
    }
    const unit = units[0]
    let source
    try {
      source = decode(unit.sourceBytes)
    } catch {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'generated-artifact', `non-utf8:${artifact.id}`))
      continue
    }
    if (sha256BytesV1(asBytes(unit.sourceBytes)) !== artifact.artifact_digest
      || unit.observationSite.localityId !== localityBySource.get(unit.observationSite.sourcePath)) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'generated-artifact', `source-mismatch:${artifact.id}`))
      continue
    }
    const attributable = rawObservations.some(({ case: rawCase, payload }) => rawCase === 'fable-import'
      && payload.generated_artifact_id === artifact.id
      && encodeCanonicalJsonV1(payload.site) === encodeCanonicalJsonV1(rawSite({
        sourcePath: unit.observationSite.sourcePath,
        semanticDeclarationAnchor: unit.observationSite.semanticDeclarationAnchor,
        sameAnchorOccurrenceOrdinal: unit.observationSite.sameAnchorOccurrenceOrdinal,
      }, unit.observationSite.localityId)))
    if (!attributable) {
      diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'generated-artifact', `unattributed:${artifact.id}`))
      continue
    }
    interopSources.push({
      sourceKind: 'generated-artifact',
      sourceId: artifact.id,
      source,
      site: rawSite({
        sourcePath: unit.observationSite.sourcePath,
        semanticDeclarationAnchor: unit.observationSite.semanticDeclarationAnchor,
        sameAnchorOccurrenceOrdinal: unit.observationSite.sameAnchorOccurrenceOrdinal,
      }, unit.observationSite.localityId),
      unit,
    })
  }
  for (const key of unitsBySource.keys()) if (!expectedUnitKeys.has(key)) {
    diagnostics.push(diagnostic('capability-extraction-incomplete', null, 'javascript-unit', `stale:${key}`))
  }

  const parsedSources = []
  for (const source of interopSources) {
    try {
      const ast = parseUnit(source.source, source.sourceKind)
      const resolver = createJavaScriptScopeResolverV1(ast, {
        preboundNames: source.sourceKind === 'generated-artifact' ? [] : preboundFableHoles(ast),
      })
      const nodes = enumerateJavaScriptAstNodesV1(ast, source.sourceId, resolver)
      const visits = nodes.map(visitJavaScriptNodeV1)
      const projected = projectJavaScriptCapabilityObservationsV1({
        source_kind: source.sourceKind,
        source_id: source.sourceId,
        observation_site: source.site,
        visits,
      })
      diagnostics.push(...projected.violations.map(({ code }) => diagnostic(code, {
        sourcePath: source.site.source_path,
        semanticDeclarationAnchor: source.site.semantic_declaration_anchor,
      }, source.sourceKind, source.sourceId)))
      rawObservations.push(...projected.observations)
      parsedSources.push({ ...source, ast, resolver, visits })
    } catch {
      diagnostics.push(diagnostic('capability-extraction-incomplete', {
        sourcePath: source.site.source_path,
        semanticDeclarationAnchor: source.site.semantic_declaration_anchor,
      }, source.sourceKind, `javascript-parse-failed:${source.sourceId}`))
      if (source.sourceKind !== 'generated-artifact') {
        const parent = rawObservations.find(({ case: rawCase, payload }) => rawCase === source.sourceKind
          && javascriptSourceIdV1(payload.expression, payload.site) === source.sourceId)
        if (parent) parent.payload.javascript_traversal_id = null
      }
    }
  }

  const extraction = extractObservedCapabilityFactsV1(rawObservations, diagnostics, { collectUnknownViolations })
  const javascriptFactsBySource = new Map()
  for (const fact of extraction.facts) {
    if (fact.observation.case !== 'javascript-capability') continue
    const key = sourceKey(
      fact.observation.payload.source_kind,
      fact.observation.payload.source_id,
    )
    const rows = javascriptFactsBySource.get(key) ?? []
    rows.push(fact)
    javascriptFactsBySource.set(key, rows)
  }
  const javascriptTraversals = []
  const traversalObservationSets = []
  const traversalViolations = []
  for (const source of parsedSources) {
    const traversal = validateJavaScriptTraversalV1({
      source_kind: source.sourceKind,
      source_id: source.sourceId,
      observation_site: source.site,
      ast: source.ast,
      binding_provenance_for_node: source.resolver,
      visits: source.visits,
      capability_facts: javascriptFactsBySource.get(sourceKey(source.sourceKind, source.sourceId)) ?? [],
    })
    if (traversal.coverage) javascriptTraversals.push(traversal.coverage)
    if (source.sourceKind === 'generated-artifact') {
      traversalObservationSets.push({
        traversal_id: javascriptTraversalIdV1(source.sourceKind, source.sourceId),
        emitted_observation_ids: traversal.emitted_observation_ids,
      })
    }
    traversalViolations.push(...traversal.violations)
  }
  javascriptTraversals.sort((left, right) => compareCanonicalTextV1(left.id, right.id))
  traversalObservationSets.sort((left, right) => compareCanonicalTextV1(left.traversal_id, right.traversal_id))
  const violations = [...extraction.violations, ...traversalViolations]
    .sort((left, right) => compareCanonicalTextV1(`${left.code}\0${encodeCanonicalJsonV1(left)}`, `${right.code}\0${encodeCanonicalJsonV1(right)}`))
  diagnostics.sort((left, right) => compareCanonicalTextV1(
    `${left.code}\0${encodeCanonicalJsonV1(left)}`,
    `${right.code}\0${encodeCanonicalJsonV1(right)}`,
  ))
  return {
    capabilityFacts: extraction.facts,
    capabilityExtraction: extraction.coverage,
    javascriptTraversals,
    traversalObservationSets,
    diagnostics,
    violations,
  }
}
