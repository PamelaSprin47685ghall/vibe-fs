import assert from 'node:assert/strict'
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { scanProjectSymbolUses } from '../../../../scripts/checks/owner-dependencies.mjs'
import { scanText } from '../../../../scripts/checks/dsl-ownership.mjs'

const fixtureRoot = fileURLToPath(new URL('../fixtures/owner-dependencies/', import.meta.url))
const repositoryScratchRoot = fileURLToPath(new URL('../../../../.fable-build/', import.meta.url))

test('WHAT[STRUCTURED-WORKFLOW-011] FCS resolves open, alias, qualified, and type-only dependencies', () => {
  mkdirSync(repositoryScratchRoot, { recursive: true })
  const scratchRoot = mkdtempSync(join(repositoryScratchRoot, 'owner-dependencies-fixture-'))
  const fixture = join(scratchRoot, 'fixture')
  const scan = join(scratchRoot, 'scan')

  try {
    cpSync(fixtureRoot, fixture, { recursive: true })
    const result = scanProjectSymbolUses({
      projectFile: join(fixture, 'Fixture.fsproj'),
      productionRoot: fixture,
      scratchRoot: scan,
      resultPath: join(scan, 'symbol-uses.json'),
    })
    const uses = result.symbolUses.filter((entry) => entry.consumerPath.endsWith('/Consumer.fs'))
    const makeUses = uses.filter((entry) => entry.symbol === 'OwnerDependencyFixture.Provider.make')
    const typeUses = uses.filter((entry) => entry.symbol === 'OwnerDependencyFixture.Provider.Hidden')

    assert.deepEqual(
      [...new Set(makeUses.map((entry) => entry.line))].sort((left, right) => left - right),
      [8, 9, 10, 11, 35, 38, 54, 55],
    )
    assert.match(readFileSync(join(fixture, 'Consumer.fs'), 'utf8'), /typeOnly \(value: Hidden\)/)
    assert.ok(makeUses.some((entry) => entry.line === 11))
    assert.ok(typeUses.some((entry) => entry.line === 12 && entry.isFromType && !entry.isFromUse))
    assert.ok(uses.every((entry) => !entry.isFromOpenStatement && !entry.isNamespace && !entry.isModule))

    const consumerApplications = result.applicationUses.filter((entry) =>
      entry.consumerPath.endsWith('/Consumer.fs'),
    )
    assert.deepEqual(
      consumerApplications
        .filter((entry) => entry.resolvedTarget === 'OwnerDependencyFixture.Foreign.Port.Send')
        .map((entry) => entry.startLine)
        .sort((left, right) => left - right),
      [24, 25, 29, 42, 44, 47, 49, 52],
    )
    assert.deepEqual(
      consumerApplications.filter((entry) =>
        /^OwnerDependencyFixture\.Foreign\.run[AB]$/.test(entry.resolvedTarget),
      ),
      [],
      'returned function values are symbol uses, not application expressions',
    )
    assert.ok(!consumerApplications.some((entry) => entry.resolvedTarget.endsWith('.Cursor.Address')))
    assert.ok(consumerApplications.some((entry) =>
      entry.startLine === 38
      && entry.sourceAnchor === 'make'
      && entry.resolvedTarget === 'OwnerDependencyFixture.Provider.make'))
    assert.equal(consumerApplications.filter((entry) =>
      entry.startLine === 54
      && entry.resolvedTarget === 'OwnerDependencyFixture.Provider.combine').length, 1)
    assert.equal(consumerApplications.filter((entry) =>
      entry.startLine === 54
      && entry.resolvedTarget === 'OwnerDependencyFixture.Provider.make').length, 2)
    assert.ok(consumerApplications.some((entry) =>
      entry.startLine === 10
      && entry.sourceAnchor === 'OwnerDependencyFixture.Provider.make'
      && entry.resolvedTarget === 'OwnerDependencyFixture.Provider.make'))
    const qualifiedCallableArgument = consumerApplications.filter((entry) => entry.startLine === 55)
    assert.equal(qualifiedCallableArgument.filter((entry) =>
      entry.resolvedTarget === 'OwnerDependencyFixture.Provider.invoke').length, 1)
    assert.equal(qualifiedCallableArgument.filter((entry) =>
      entry.resolvedTarget === 'OwnerDependencyFixture.Provider.make').length, 0)
    assert.ok(qualifiedCallableArgument[0].argumentIdentifiers.includes('make'))
    const buildApplications = consumerApplications.filter((entry) => entry.startLine === 56)
    assert.equal(buildApplications.filter((entry) =>
      entry.resolvedTarget === 'OwnerDependencyFixture.WorktreeCommands.create').length, 1)
    assert.equal(buildApplications.filter((entry) => entry.resolvedTarget.endsWith('.runner')).length, 0)
    assert.ok(buildApplications[0].argumentIdentifiers.includes('runner'))
    const nestedApplications = consumerApplications.filter((entry) => entry.startLine === 57)
    assert.equal(nestedApplications.filter((entry) =>
      entry.resolvedTarget === 'OwnerDependencyFixture.Provider.outer').length, 1)
    assert.equal(nestedApplications.filter((entry) =>
      entry.resolvedTarget === 'OwnerDependencyFixture.Provider.inner').length, 1)
    const verifiedMatch = result.matchExpressions.find((entry) =>
      entry.consumerPath.endsWith('/Consumer.fs')
      && entry.scrutinee.resolvedTarget === 'OwnerDependencyFixture.Provider.verify')
    assert.deepEqual(verifiedMatch?.clauses.map((clause) => clause.patternKind), ['Ok', 'Error'])
    assert.ok(verifiedMatch.clauses[0].startLine === 29 && verifiedMatch.clauses[1].startLine === 30)
    assert.ok(result.bindExpressions.some((entry) =>
      entry.consumerPath.endsWith('/Consumer.fs')
      && entry.builderKind === 'Async'
      && entry.binding.startLine === 34
      && entry.body.startLine === 35))
    assert.ok(result.conditionalExpressions.some((entry) =>
      entry.consumerPath.endsWith('/Consumer.fs')
      && entry.condition.startLine === 41
      && entry.branches.some((branch) => branch.kind === 'Then' && branch.startLine === 42)
      && entry.branches.some((branch) => branch.kind === 'Else' && branch.startLine === 44)))
    assert.ok(result.tryExpressions.some((entry) =>
      entry.consumerPath.endsWith('/Consumer.fs')
      && entry.kind === 'With'
      && entry.body.startLine === 47
      && entry.continuations.some((continuation) => continuation.startLine === 49)))
    assert.ok(result.loopExpressions.some((entry) =>
      entry.consumerPath.endsWith('/Consumer.fs')
      && entry.kind === 'ForEach'
      && entry.body.startLine === 52))
    const consumerLambdas = result.lambdaExpressions.filter((entry) => entry.consumerPath.endsWith('/Consumer.fs'))
    assert.ok(consumerLambdas.some((entry) => entry.startLine === 62 && entry.invokedBy.length === 0))
    assert.ok(consumerLambdas.some((entry) => entry.startLine === 64 && entry.invokedBy.length === 1))
    const localFunctions = result.localFunctionBindings.filter((entry) => entry.consumerPath.endsWith('/Consumer.fs'))
    const invokedLocal = localFunctions.find((entry) => entry.name === 'invoked')
    const escapedLocal = localFunctions.find((entry) => entry.name === 'escaped')
    assert.ok(invokedLocal.fullSymbol === 'invoked' && invokedLocal.invokedBy.length === 1)
    assert.ok(escapedLocal.fullSymbol === 'escaped' && escapedLocal.invokedBy.length === 0)
    for (const local of [invokedLocal, escapedLocal]) {
      assert.ok(local.scope.startLine <= local.startLine && local.endLine <= local.scope.endLine)
    }

    const consumerPath = uses[0].consumerPath
    const consumerText = readFileSync(join(fixture, 'Consumer.fs'), 'utf8')
    assert.ok(
      scanText(consumerText, consumerPath, result).some(({ gate }) => gate === 'program-counter'),
      'FCS-resolved Foreign.runA/runB and externally declared port.Send must expose Cursor.Address as a PC',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
