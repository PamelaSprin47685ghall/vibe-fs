import assert from 'node:assert/strict'
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { scanProjectSymbolUses } from '../../../../scripts/checks/owner-dependencies.mjs'

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
      [8, 9, 10, 11],
    )
    assert.match(readFileSync(join(fixture, 'Consumer.fs'), 'utf8'), /typeOnly \(value: Hidden\)/)
    assert.ok(makeUses.some((entry) => entry.line === 11))
    assert.ok(typeUses.some((entry) => entry.line === 12 && entry.isFromType && !entry.isFromUse))
    assert.ok(uses.every((entry) => !entry.isFromOpenStatement && !entry.isNamespace && !entry.isModule))
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
