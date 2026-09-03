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
