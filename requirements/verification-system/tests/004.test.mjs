import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parseConcurrency, assertConcurrency } = await import("../../../scripts/lib/concurrency-cap.mjs");


test('WHAT[verification-system-004] parseConcurrency accepts unset, empty, and boolean values', () => {
  assert.equal(parseConcurrency(undefined), true)
  assert.equal(parseConcurrency(null), true)
  assert.equal(parseConcurrency(''), true)
  assert.equal(parseConcurrency(true), true)
  assert.equal(parseConcurrency('true'), true)
})
test('WHAT[verification-system-004] parseConcurrency treats false as 1 alias', () => {
  assert.equal(parseConcurrency(false), 1)
  assert.equal(parseConcurrency('false'), 1)
})
test('WHAT[verification-system-004] parseConcurrency accepts positive integers', () => {
  assert.equal(parseConcurrency(1), 1)
  assert.equal(parseConcurrency('1'), 1)
  assert.equal(parseConcurrency(4), 4)
  assert.equal(parseConcurrency('8'), 8)
})
test('WHAT[verification-system-004] parseConcurrency throws input error on non-positive, NaN, or non-integer', () => {
  assert.throws(() => parseConcurrency(0), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('0'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(-1), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('-5'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(1.5), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('2.7'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(NaN), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('invalid'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(Infinity), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('-Infinity'), /Invalid NODE_TEST_CONCURRENCY/)
})
test('WHAT[verification-system-004] assertConcurrency forwards to parseConcurrency', () => {
  assert.equal(assertConcurrency('4'), 4)
  assert.throws(() => assertConcurrency('bad'), /Invalid NODE_TEST_CONCURRENCY/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, mkdirSync, writeFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { assessIntegrationEntryCoverage } = await import("./support/integration-entry-coverage.mjs");
const { discoverSuiteTests } = await import("./support/discover-suite-tests.mjs");
const { integrationNodeTestSteps, selectIntegrationSteps } = await import("./support/integration-node-test-steps.mjs");
const { walk } = await import("../../../scripts/lib/walk.mjs");

const assess = (discoveredTests, wiredTests, childOwnedTests = []) =>
  assessIntegrationEntryCoverage({ discoveredTests, wiredTests, childOwnedTests })
const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../..')
const packageIntegrationDir = path.join(root, 'requirements/distribution/tests/integration/package')
const normalize = (file) => path.relative(root, file).split(path.sep).join('/')

test('WHAT[verification-system-004] integration entry coverage goes red for an unwired integration test', () => {
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
test('WHAT[verification-system-004] integration entry coverage goes red for stale or duplicate wiring', () => {
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
test('WHAT[verification-system-004] integration entry coverage goes red when a child-owned test is not declared', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { duplicateClauseDefinitions } = await import("../../../scripts/lib/spec-rules.mjs");


test('WHAT[verification-system-004] spec gate rejects duplicate CHATEXEC identifiers', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'spec-duplicate-id-'))

  try {
    const entries = []
    for (const packageName of ['chat-execution-a', 'chat-execution-b']) {
      const packageDirectory = join(fixture, 'requirements', packageName)
      mkdirSync(packageDirectory, { recursive: true })
      const file = join(packageDirectory, 'WHAT.md')
      writeFileSync(
        file,
        `# ${packageName} — WHAT\n\n## CHATEXEC-001: duplicate fixture clause\n`,
      )
      entries.push({ file, pkg: packageName, text: readFileSync(file, 'utf8') })
    }

    const findings = duplicateClauseDefinitions(entries)
    assert.ok(
      findings.length > 0,
      'duplicateClauseDefinitions accepted duplicate CHATEXEC-001 definitions',
    )
    assert.match(
      findings.map((finding) => finding.msg).join('\n'),
      /条款 ID 重复定义：CHATEXEC-001/,
    )

    assert.deepEqual(
      duplicateClauseDefinitions([entries[0]]),
      [],
      'a single CHATEXEC-001 definition must not fail closed',
    )
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { check, run } = await import("../../../scripts/checks/js-boundary-gate.mjs");

test('WHAT[verification-system-004] js-boundary-gate verifier is red on controlled A-D semantic debt', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'boundary-gate-red-'))

  try {
    const pkgDir = join(fixture, 'requirements', 'sample-pkg', 'tests')
    mkdirSync(pkgDir, { recursive: true })

    // Build debt fixture content dynamically at runtime to avoid embedding static forbidden patterns in test source
    const distPath = ['..', '..', '..', 'dist', 'Internal', 'UnregisteredModule.js'].join('/')
    const importKeyword = 'import * as mod'
    const fromKeyword = 'from'
    const importLine = `${importKeyword} ${fromKeyword} '${distPath}'`
    const exportDiscoveryLine = 'const names = ' + 'Object.' + 'keys(mod)'
    const debtFile = join(pkgDir, 'debt.test.mjs')
    writeFileSync(
      debtFile,
      `${importLine}\n${exportDiscoveryLine}\n`,
      'utf8',
    )

    const checkResult = check({ root: fixture })
    assert.ok(checkResult.debt > 0, 'check must report non-zero debt on A-D violations')
    assert.ok(
      checkResult.issues.some((issue) => issue.code === 'deep-dist-import'),
      'check must report deep-dist-import violation',
    )
    assert.ok(
      checkResult.issues.some((issue) => issue.code === 'export-discovery'),
      'check must report export-discovery violation',
    )

    const exitCode = run({ root: fixture })
    assert.equal(exitCode, 1, 'run must return exit code 1 on semantic debt')

    rmSync(debtFile)
    const cleanExitCode = run({ root: fixture })
    assert.equal(cleanExitCode, 0, 'run must return exit code 0 on zero debt')
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
}
