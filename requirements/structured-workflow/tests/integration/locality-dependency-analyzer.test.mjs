import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import test from 'node:test'

import {
  readOwnerProjectInventoryV1,
  runLocalityDependencyScan,
} from '../../../../scripts/checks/locality-dependencies.mjs'
import { classifyCapabilityObservationV1 } from '../../../../scripts/lib/capability-observations-v1.mjs'

const ROOT = resolve(import.meta.dirname, '../../../..')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/locality-dependencies')
const AGGREGATE = join(FIXTURE, 'Wanxiangshu.fsproj')

test('WHAT[STRUCTURED-WORKFLOW-011] compiler-resolved analyzer rejects an aggregate-green missing locality edge', { timeout: 120_000 }, () => {
  const output = mkdtempSync(join(tmpdir(), 'wanxiangshu-locality-fixture-'))
  try {
    const aggregate = spawnSync(
      'dotnet',
      ['tool', 'run', 'fable', '--', AGGREGATE, '-o', output, '--noGitignore', '--noCache'],
      { cwd: ROOT, encoding: 'utf8', timeout: 110_000, env: { ...process.env, NuGetAudit: 'false' } },
    )
    assert.equal(aggregate.status, 0, `flattened aggregate must compile green\n${aggregate.stdout}\n${aggregate.stderr}`)

    const result = runLocalityDependencyScan({ aggregate: AGGREGATE, productionRoot: FIXTURE })
    const inventory = readOwnerProjectInventoryV1({ sourceRoot: FIXTURE, aggregate: AGGREGATE })
    assert.equal(inventory.aggregatePath, 'requirements/structured-workflow/tests/fixtures/locality-dependencies/Wanxiangshu.fsproj')
    assert.deepEqual(
      inventory.localities.map(({ id, sources }) => ({ id, sources })),
      [
        {
          id: 'fixture-consumer',
          sources: [{
            implementationPath: 'requirements/structured-workflow/tests/fixtures/locality-dependencies/Consumer.fs',
            signaturePath: 'requirements/structured-workflow/tests/fixtures/locality-dependencies/Consumer.fsi',
          }],
        },
        {
          id: 'fixture-provider',
          sources: [{
            implementationPath: 'requirements/structured-workflow/tests/fixtures/locality-dependencies/Provider.fs',
            signaturePath: 'requirements/structured-workflow/tests/fixtures/locality-dependencies/Provider.fsi',
          }],
        },
      ],
    )
    assert.equal(result.compilerObservations.schemaVersion, 1)
    assert.equal(result.compilerObservations.projectFile, 'requirements/structured-workflow/tests/fixtures/locality-dependencies/Wanxiangshu.fsproj')
    assert.deepEqual(result.compilerObservations.productionFiles, inventory.productionFiles)
    assert.deepEqual(result.compilerObservations.signatureFiles, inventory.signatureFiles)
    assert.deepEqual(result.compilerObservations.diagnostics, [])
    assert.ok(result.compilerObservations.fsharpNodes.length > 0, 'typed implementation AST nodes must be observed')
    assert.ok(
      result.compilerObservations.fsharpNodes.some(({ nodeKind }) => nodeKind === 'if-then-else'),
      'closed FSharpExpr traversal must preserve ordinary control nodes',
    )
    const scannerConstant = result.compilerObservations.fsharpNodes.find(({ nodeKind }) => nodeKind === 'const')
    assert.ok(scannerConstant, 'the proof must use the production scanner taxonomy')
    assert.notEqual(classifyCapabilityObservationV1({
      case: 'fsharp-node',
      payload: {
        node_kind: scannerConstant.nodeKind,
        semantic_identity: scannerConstant.semanticIdentity,
        site: {
          locality_id: 'fixture-provider',
          source_path: scannerConstant.site.sourcePath,
          semantic_declaration_anchor: scannerConstant.site.semanticDeclarationAnchor,
          same_anchor_occurrence_ordinal: scannerConstant.site.sameAnchorOccurrenceOrdinal,
        },
      },
    }).case, 'unknown', 'a production scanner const node must not depend on a test-only node-kind alias')
    const duplicateConstants = result.compilerObservations.fsharpNodes.filter(({ nodeKind, semanticIdentity, site }) =>
      nodeKind === 'const'
      && semanticIdentity === 'fsharp:const'
      && site.semanticDeclarationAnchor.endsWith('.duplicateConstants'))
    assert.deepEqual(
      duplicateConstants.map(({ site }) => site.sameAnchorOccurrenceOrdinal).sort((left, right) => left - right),
      [0, 1],
      'same payload occurrences count independently of unrelated nodes under the same declaration anchor',
    )
    const duplicateExternal = result.compilerObservations.externalSymbolUses.filter(({ fullyQualifiedSymbol, site }) =>
      fullyQualifiedSymbol === 'Microsoft.FSharp.Collections.List.map'
      && site.sourcePath.endsWith('/locality-dependencies/Provider.fs'))
    assert.deepEqual(
      duplicateExternal.map(({ site }) => site.sameAnchorOccurrenceOrdinal).sort((left, right) => left - right),
      [0, 1],
      'two real same-symbol ranges stay distinct after duplicate extraction of one range is removed',
    )
    const observationGroups = new Map()
    const addObservation = (rawCase, payload, observedSite) => {
      const key = JSON.stringify([observedSite.sourcePath, observedSite.semanticDeclarationAnchor, rawCase, payload])
      const ordinals = observationGroups.get(key) ?? []
      ordinals.push(observedSite.sameAnchorOccurrenceOrdinal)
      observationGroups.set(key, ordinals)
    }
    for (const { nodeKind, semanticIdentity, site } of result.compilerObservations.fsharpNodes) {
      addObservation('fsharp-node', [nodeKind, semanticIdentity], site)
    }
    for (const { assembly, fullyQualifiedSymbol, site } of result.compilerObservations.externalSymbolUses) {
      addObservation('fcs-external-symbol-use', [assembly, fullyQualifiedSymbol], site)
    }
    for (const { kind, moduleSpecifier, selector, expression, site } of result.compilerObservations.fableInterop) {
      addObservation(kind, kind === 'fable-import' ? [moduleSpecifier, selector] : [expression], site)
    }
    for (const { exportKind, declarationIdentity, site } of result.compilerObservations.signatureExports) {
      addObservation('public-signature-export', [exportKind, declarationIdentity], site)
    }
    for (const ordinals of observationGroups.values()) {
      assert.deepEqual(
        ordinals.sort((left, right) => left - right),
        Array.from({ length: ordinals.length }, (_, ordinal) => ordinal),
        'each exact raw observation payload owns an independent contiguous occurrence sequence',
      )
    }
    const mutableScopeNodes = result.compilerObservations.fsharpNodes.filter(({ site }) =>
      site.semanticDeclarationAnchor.endsWith('.classifyMutableScope'))
    for (const nodeKind of [
      'local-mutable-value-read',
      'local-mutable-value-set',
      'module-mutable-value-read',
      'module-mutable-value-set',
      'mutable-field-get',
      'mutable-field-set',
    ]) {
      const node = mutableScopeNodes.find((candidate) => candidate.nodeKind === nodeKind)
      assert.ok(node, `production FCS facts must distinguish ${nodeKind}`)
      const disposition = classifyCapabilityObservationV1({
        case: 'fsharp-node',
        payload: {
          node_kind: node.nodeKind,
          semantic_identity: node.semanticIdentity,
          site: {
            locality_id: 'fixture-provider',
            source_path: node.site.sourcePath,
            semantic_declaration_anchor: node.site.semanticDeclarationAnchor,
            same_anchor_occurrence_ordinal: node.site.sameAnchorOccurrenceOrdinal,
          },
        },
      })
      if (nodeKind.startsWith('local-')) {
        assert.equal(disposition.case, 'unknown')
      } else {
        assert.equal(disposition.case, 'classified')
        assert.deepEqual(
          disposition.payload.mutable_resources,
          nodeKind.startsWith('module-') ? ['top-level-mutable'] : ['runtime-cell'],
        )
      }
    }
    const objectStateNodes = result.compilerObservations.fsharpNodes.filter(({ semanticIdentity }) =>
      semanticIdentity.includes('MutableOwner.state'))
    assert.ok(objectStateNodes.some(({ nodeKind }) => nodeKind === 'mutable-field-get'))
    assert.ok(objectStateNodes.some(({ nodeKind }) => nodeKind === 'mutable-field-set'))
    const capabilityBinding = result.compilerObservations.fsharpNodes.find(({ nodeKind, semanticIdentity }) =>
      nodeKind === 'capability-immutable-value' && semanticIdentity === 'capability')
    assert.ok(capabilityBinding, 'an immutable binding that carries a capability must never become irrelevant')
    const capabilityDisposition = classifyCapabilityObservationV1({
      case: 'fsharp-node',
      payload: {
        node_kind: capabilityBinding.nodeKind,
        semantic_identity: capabilityBinding.semanticIdentity,
        site: {
          locality_id: 'fixture-provider',
          source_path: capabilityBinding.site.sourcePath,
          semantic_declaration_anchor: capabilityBinding.site.semanticDeclarationAnchor,
          same_anchor_occurrence_ordinal: capabilityBinding.site.sameAnchorOccurrenceOrdinal,
        },
      },
    })
    assert.deepEqual(capabilityDisposition.payload.semantic_classes, ['capability-value'])
    const capturedMutable = result.compilerObservations.fsharpNodes.filter(({ nodeKind, semanticIdentity }) =>
      ['local-mutable-value-read', 'local-mutable-value-set'].includes(nodeKind)
      && semanticIdentity === 'count')
    assert.deepEqual([...new Set(capturedMutable.map(({ nodeKind }) => nodeKind))].sort(), [
      'local-mutable-value-read',
      'local-mutable-value-set',
    ])
    assert.ok(capturedMutable.every((node) => classifyCapabilityObservationV1({
      case: 'fsharp-node',
      payload: {
        node_kind: node.nodeKind,
        semantic_identity: node.semanticIdentity,
        site: {
          locality_id: 'fixture-provider',
          source_path: node.site.sourcePath,
          semantic_declaration_anchor: node.site.semanticDeclarationAnchor,
          same_anchor_occurrence_ordinal: node.site.sameAnchorOccurrenceOrdinal,
        },
      },
    }).case === 'unknown'), 'without a closed escape proof even a lexical local must stay Unknown')
    const immutableAlgebraNodes = result.compilerObservations.fsharpNodes.filter(({ site }) =>
      site.semanticDeclarationAnchor.endsWith('.classifyImmutableAlgebra'))
    const immutableAlgebraKind = (identity) => {
      const candidates = immutableAlgebraNodes.filter(({ semanticIdentity }) => semanticIdentity === identity)
      assert.ok(candidates.length > 0, `the immutable algebra fixture must observe ${identity}`)
      return [...new Set(candidates.map(({ nodeKind }) => nodeKind))]
    }
    for (const identity of [
      'purePrimitiveRecord',
      'pureLeaf',
      'pureTree',
      'pureTuple',
      'pureOption',
      'pureList',
      'pureMap',
      'pureSet',
      'pureResult',
      'pureGeneric',
    ]) {
      assert.deepEqual(
        immutableAlgebraKind(identity),
        ['pure-immutable-value'],
        `${identity} must be proven by the recursive immutable algebra`,
      )
    }
    const immutableAlgebraCoreSymbols = new Set(result.compilerObservations.externalSymbolUses
      .filter(({ assembly, fullyQualifiedSymbol, symbolKind }) =>
        assembly === 'FSharp.Core'
        && symbolKind === 'FSharpEntity'
        && (fullyQualifiedSymbol.includes('`')
          || (fullyQualifiedSymbol.includes('<') && fullyQualifiedSymbol.includes('>'))))
      .filter(({ assembly, fullyQualifiedSymbol, site }) => {
        const disposition = classifyCapabilityObservationV1({
          case: 'fcs-external-symbol-use',
          payload: {
            assembly,
            fully_qualified_symbol: fullyQualifiedSymbol,
            site: {
              locality_id: 'fixture-provider',
              source_path: site.sourcePath,
              semantic_declaration_anchor: site.semanticDeclarationAnchor,
              same_anchor_occurrence_ordinal: site.sameAnchorOccurrenceOrdinal,
            },
          },
        })
        return disposition.case === 'classified'
          && disposition.payload.semantic_classes.includes('pure-representation')
      })
      .map(({ fullyQualifiedSymbol }) => fullyQualifiedSymbol))
    assert.equal(
      immutableAlgebraCoreSymbols.size,
      3,
      'the pinned compiler closed generic container types must pass the production exact-assembly classifier',
    )
    for (const identity of [
      'recursiveCapability',
      'mutualCapability',
      'changingCycle',
      'capabilityGeneric',
      'nestedCapability',
      'nestedFunction',
    ]) {
      assert.deepEqual(
        immutableAlgebraKind(identity),
        ['capability-immutable-value'],
        `${identity} must retain its nested capability`,
      )
    }
    assert.deepEqual(immutableAlgebraKind('recursiveMutable'), ['mutable-container-value'])
    assert.deepEqual(immutableAlgebraKind('arrayGeneric'), ['mutable-container-value'])
    assert.deepEqual(immutableAlgebraKind('nestedArray'), ['mutable-container-value'])
    assert.deepEqual(immutableAlgebraKind('nestedMutableCapability'), ['capability-mutable-container-value'])
    assert.deepEqual(immutableAlgebraKind('plainClass'), ['immutable-value'])
    assert.deepEqual(immutableAlgebraKind('genericValue'), ['immutable-value'])
    const immutableFieldKinds = new Map(result.compilerObservations.fsharpNodes
      .filter(({ site }) => site.semanticDeclarationAnchor.endsWith('.inspectImmutableAlgebra'))
      .filter(({ semanticIdentity }) => semanticIdentity.includes('.'))
      .map(({ semanticIdentity, nodeKind }) => [semanticIdentity.split('.').at(-1), nodeKind]))
    assert.equal(immutableFieldKinds.get('Count'), 'immutable-field-get')
    assert.equal(immutableFieldKinds.get('Port'), 'capability-immutable-field-get')
    assert.equal(immutableFieldKinds.get('Values'), 'mutable-container-field-get')
    assert.equal(immutableFieldKinds.get('Callback'), 'capability-immutable-field-get')
    assert.equal(immutableFieldKinds.get('Ports'), 'capability-mutable-container-field-get')
    assert.deepEqual(
      [...new Set(result.compilerObservations.fableInterop.map(({ kind }) => kind))].sort(),
      ['emit-js-expr', 'fable-emit', 'fable-import'],
    )
    assert.ok(
      result.compilerObservations.fableInterop.every(({ site }) => site.sourcePath.endsWith('.fs') && !site.sourcePath.endsWith('.fsi')),
      'Fable interop belongs to the executable implementation site, never the sibling signature mirror',
    )
    assert.ok(
      result.compilerObservations.fsharpNodes.every(({ site }) => site.sourcePath.endsWith('.fs') && !site.sourcePath.endsWith('.fsi')),
      'typed executable nodes must belong to implementation files',
    )
    assert.ok(
      result.compilerObservations.signatureExports.every(({ site }) => site.sourcePath.endsWith('.fsi')),
      'public export inventory must belong to sibling signatures',
    )
    assert.ok(
      result.compilerObservations.signatureExports.some(
        ({ exportKind, declarationIdentity }) =>
          exportKind === 'pure-function' && declarationIdentity === 'LocalityDependencyFixture.Provider.make',
      ),
      'sibling signature public exports must be observed',
    )
    assert.ok(
      result.compilerObservations.signatureExports.some(
        ({ declarationIdentity }) => declarationIdentity === 'LocalityDependencyFixture.PublicValue.Text',
      ),
      'record-field exports must not disappear behind the containing type',
    )
    assert.ok(
      result.compilerObservations.signatureExports.some(
        ({ declarationIdentity }) => declarationIdentity === 'LocalityDependencyFixture.PublicChoice.PublicChoice',
      ),
      'union-case exports must not disappear behind the containing type',
    )
    assert.deepEqual(
      result.analysis.violations.map(({ code, consumerLocality, providerLocality }) => ({
        code,
        consumerLocality,
        providerLocality,
      })),
      [{ code: 'missing-closure-edge', consumerLocality: 'fixture-consumer', providerLocality: 'fixture-provider' }],
    )

    const providerUses = result.compilerObservations.declarationUses.filter(({ providerPaths }) =>
      providerPaths.some((path) => path.endsWith('/locality-dependencies/Provider.fs')),
    )
    assert.ok(providerUses.some(({ isFromOpenStatement }) => isFromOpenStatement), 'open use must resolve')
    assert.ok(providerUses.some(({ isFromType }) => isFromType), 'alias/generic type use must resolve')
    assert.ok(providerUses.some(({ isFromPattern }) => isFromPattern), 'union-case pattern use must resolve')
    assert.ok(providerUses.some(({ isFromUse }) => isFromUse), 'value use must resolve')
    assert.ok(
      !result.analysis.edges.some(({ providerSource }) => providerSource.includes('/.nuget/')),
      'external/package symbols must not become production locality edges',
    )
    assert.ok(
      result.compilerObservations.externalSymbolUses.some(({ assembly }) => assembly === 'FSharp.Core'),
      'external compiler symbols must remain capability observations instead of dependency edges',
    )
  } finally {
    rmSync(output, { recursive: true, force: true })
  }
})
