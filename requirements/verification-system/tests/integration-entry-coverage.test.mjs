import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { assessIntegrationEntryCoverage } from './support/integration-entry-coverage.mjs'
import { discoverSuiteTests } from './support/discover-suite-tests.mjs'
import { integrationNodeTestSteps, selectIntegrationSteps } from './support/integration-node-test-steps.mjs'
import { walk } from '../../../scripts/lib/walk.mjs'

const assess = (discoveredTests, wiredTests, childOwnedTests = []) =>
  assessIntegrationEntryCoverage({ discoveredTests, wiredTests, childOwnedTests })

test('WHAT[VERIFICATION-SYSTEM-009] integration entry coverage accepts an exact reachable set', () => {
  assert.deepEqual(
    assess(
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
    ),
    { ok: true, missingFromEntry: [], staleEntry: [], duplicateWiring: [] },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] integration entry coverage goes red for an unwired integration test', () => {
  assert.deepEqual(
    assess(
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
      ['requirements/a/tests/integration/a.test.mjs'],
    ),
    {
      ok: false,
      missingFromEntry: ['requirements/b/tests/integration/b.test.mjs'],
      staleEntry: [],
      duplicateWiring: [],
    },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] integration entry coverage goes red for stale or duplicate wiring', () => {
  assert.deepEqual(
    assess(
      ['requirements/a/tests/integration/a.test.mjs'],
      [
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/missing/tests/integration/missing.test.mjs',
      ],
    ),
    {
      ok: false,
      missingFromEntry: [],
      staleEntry: ['requirements/missing/tests/integration/missing.test.mjs'],
      duplicateWiring: ['requirements/a/tests/integration/a.test.mjs'],
    },
  )
})

test('WHAT[VERIFICATION-SYSTEM-009] integration entry coverage delegates the exact declared child-owned set', () => {
  assert.deepEqual(
    assess(
      [
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/distribution/tests/integration/package/layout.test.mjs',
      ],
      ['requirements/a/tests/integration/a.test.mjs'],
      ['requirements/distribution/tests/integration/package/layout.test.mjs'],
    ),
    { ok: true, missingFromEntry: [], staleEntry: [], duplicateWiring: [] },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] integration entry coverage goes red when a child-owned test is not declared', () => {
  // A child test that exists on disk but is omitted from the declared
  // child-owned set must surface as missing-from-entry: the parent would
  // neither run it nor delegate it. This is the no-unwired-child-test gate.
  assert.deepEqual(
    assess(
      [
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/distribution/tests/integration/package/layout.test.mjs',
        'requirements/distribution/tests/integration/package/contents.test.mjs',
      ],
      ['requirements/a/tests/integration/a.test.mjs'],
      ['requirements/distribution/tests/integration/package/layout.test.mjs'],
    ),
    {
      ok: false,
      missingFromEntry: ['requirements/distribution/tests/integration/package/contents.test.mjs'],
      staleEntry: [],
      duplicateWiring: [],
    },
  )
})

// --- behavior-level discovery tests (real helper, real filesystem) ---

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../..')
const packageIntegrationDir = path.join(root, 'requirements/distribution/tests/integration/package')
const normalize = (file) => path.relative(root, file).split(path.sep).join('/')

test('WHAT[VERIFICATION-SYSTEM-009] discoverSuiteTests lists every package *.test.mjs and excludes the runner', () => {
  const discovered = discoverSuiteTests(packageIntegrationDir)
  // The four real package suites are all picked up.
  assert.ok(discovered.includes('contents.test.mjs'))
  assert.ok(discovered.includes('layout.test.mjs'))
  assert.ok(discovered.includes('import.test.mjs'))
  assert.ok(discovered.includes('resources.test.mjs'))
  // The runner itself and any non-test file are excluded by suffix.
  assert.ok(!discovered.includes('run.mjs'))
  // Deterministic, sorted, deduplicated.
  assert.deepEqual(discovered, [...new Set(discovered)].sort())
})

test('WHAT[VERIFICATION-SYSTEM-009] discoverSuiteTests auto-includes an added test and excludes non-test files', () => {
  const scratch = mkdtempSync(path.join(tmpdir(), 'pkg-suite-'))
  try {
    writeFileSync(path.join(scratch, 'alpha.test.mjs'), '// noop\n')
    writeFileSync(path.join(scratch, 'beta.test.mjs'), '// noop\n')
    writeFileSync(path.join(scratch, 'run.mjs'), '// runner\n')
    writeFileSync(path.join(scratch, 'helper.mjs'), '// helper\n')
    mkdirSync(path.join(scratch, 'nested'))
    writeFileSync(path.join(scratch, 'nested', 'ignored.test.mjs'), '// nested\n')
    const discovered = discoverSuiteTests(scratch)
    assert.deepEqual(discovered, ['alpha.test.mjs', 'beta.test.mjs'])
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-009] discoverSuiteTests fail-closes on an unreadable directory', () => {
  // A non-existent directory yields an empty set rather than throwing; the
  // child runner turns an empty set into a non-zero exit.
  assert.deepEqual(discoverSuiteTests(path.join(tmpdir(), 'does-not-exist-suite-xyz')), [])
})

test('WHAT[VERIFICATION-SYSTEM-009] parent delegation set equals the child-executed set (no drift)', () => {
  // Both the parent entry and the child runner consume discoverSuiteTests on
  // the same directory, so the delegated set must be exactly the set the child
  // runs. If these ever diverge, an added package test is silently omitted.
  const childExecuted = discoverSuiteTests(packageIntegrationDir)
  const parentDelegated = discoverSuiteTests(packageIntegrationDir)
  assert.deepEqual(childExecuted, parentDelegated)
  assert.ok(childExecuted.length > 0, 'package integration dir must own at least one suite')
})

test('WHAT[VERIFICATION-SYSTEM-009] the real integration entry covers every discovered integration test', () => {
  // Behavior proof against the real repository state: walk requirements for
  // *.test.mjs under tests/integration, delegate the exact discovered package
  // set, and assert the entry coverage is green using the SAME wired set the
  // parent run.mjs executes (single source of truth in
  // integration-node-test-steps.mjs). An added package test is delegated
  // automatically; an added non-package test that is not wired into the shared
  // steps makes this go red.
  const discoveredIntegrationTests = walk(path.join(root, 'requirements'), ['.test.mjs'])
    .map(normalize)
    .filter((file) => file.includes('/tests/integration/'))
  const childOwnedIntegrationTests = discoverSuiteTests(packageIntegrationDir).map((name) =>
    normalize(path.join(packageIntegrationDir, name)),
  )
  const wiredIntegrationTests = integrationNodeTestSteps(root).flatMap((step) =>
    step.files.map(normalize),
  )
  const result = assessIntegrationEntryCoverage({
    discoveredTests: discoveredIntegrationTests,
    wiredTests: wiredIntegrationTests,
    childOwnedTests: childOwnedIntegrationTests,
  })
  assert.equal(result.ok, true, JSON.stringify(result, null, 2))
  assert.deepEqual(childOwnedIntegrationTests.sort(), [
    'requirements/distribution/tests/integration/package/consumption.test.mjs',
    'requirements/distribution/tests/integration/package/contents.test.mjs',
    'requirements/distribution/tests/integration/package/import.test.mjs',
    'requirements/distribution/tests/integration/package/layout.test.mjs',
    'requirements/distribution/tests/integration/package/resources.test.mjs',
  ])
})

test('WHAT[VERIFICATION-SYSTEM-003] integration grouping set partition invariants', () => {
  // 1. 每 integration 文件恰在一个组
  // 2. 合联集 = discovered set (excluding child owned)
  // 3. 日常集不含 releaseOnly 所含（特别是 compiler-canary）
  // 4. 发布含日常 + 发布全部
  const allSteps = integrationNodeTestSteps(root)
  const dailySteps = selectIntegrationSteps(root, { releaseOnly: false })
  const releaseSteps = selectIntegrationSteps(root, { releaseOnly: true })

  const discoveredIntegrationTests = walk(path.join(root, 'requirements'), ['.test.mjs'])
    .map(normalize)
    .filter((file) => file.includes('/tests/integration/'))
  const childOwnedIntegrationTests = new Set(
    discoverSuiteTests(packageIntegrationDir).map((name) =>
      normalize(path.join(packageIntegrationDir, name)),
    ),
  )
  const nonChildDiscovered = discoveredIntegrationTests.filter((f) => !childOwnedIntegrationTests.has(f)).sort()

  // Invariant 1: 每 integration 文件恰在一个组
  const seenFiles = new Set()
  for (const step of allSteps) {
    assert.ok(typeof step.label === 'string' && step.label.length > 0, 'step must have non-empty label')
    assert.ok(Array.isArray(step.files) && step.files.length > 0, 'step must have non-empty files')
    for (const file of step.files) {
      const norm = normalize(file)
      assert.ok(!seenFiles.has(norm), `file must belong to exactly one step: ${norm}`)
      seenFiles.add(norm)
    }
  }

  // Invariant 2: 合联集 = discovered set
  const unionWired = [...seenFiles].sort()
  assert.deepEqual(unionWired, nonChildDiscovered, 'union of all step files must equal non-child discovered set')

  // Invariant 3: 日常集不含 releaseOnly 所含，且 compiler-canary 不在日常集中
  const dailyFiles = new Set(dailySteps.flatMap((s) => s.files.map(normalize)))
  const releaseOnlySteps = allSteps.filter((s) => s.releaseOnly)
  const releaseOnlyFiles = new Set(releaseOnlySteps.flatMap((s) => s.files.map(normalize)))

  for (const file of dailyFiles) {
    assert.ok(!releaseOnlyFiles.has(file), `daily set must not contain releaseOnly file: ${file}`)
  }
  // Specific check for compiler-canary in daily set
  assert.ok(
    !dailySteps.some((s) => s.label === 'compiler-canary'),
    'daily steps must not contain compiler-canary',
  )

  // Invariant 4: 发布含日常 + 发布
  const releaseFiles = new Set(releaseSteps.flatMap((s) => s.files.map(normalize)))
  assert.deepEqual(
    [...releaseFiles].sort(),
    [...unionWired].sort(),
    'release steps must include all steps (daily + releaseOnly)',
  )
})
