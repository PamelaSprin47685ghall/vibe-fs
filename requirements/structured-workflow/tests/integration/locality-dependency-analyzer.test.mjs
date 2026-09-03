import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import test from 'node:test'

import { runLocalityDependencyScan } from '../../../../scripts/checks/locality-dependencies.mjs'

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
    assert.deepEqual(
      result.analysis.violations.map(({ code, consumerLocality, providerLocality }) => ({
        code,
        consumerLocality,
        providerLocality,
      })),
      [{ code: 'missing-closure-edge', consumerLocality: 'fixture-consumer', providerLocality: 'fixture-provider' }],
    )

    const providerUses = result.compiler.symbolUses.filter(({ providerPaths }) =>
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
  } finally {
    rmSync(output, { recursive: true, force: true })
  }
})
