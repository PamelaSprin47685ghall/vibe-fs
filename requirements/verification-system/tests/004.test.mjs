import test from 'node:test'

{
const { default: assert } = await import('node:assert/strict')
const { mkdtempSync, mkdirSync, rmSync, writeFileSync } = await import('node:fs')
const { tmpdir } = await import('node:os')
const { join } = await import('node:path')
const { validateModuleLoadability } = await import('../../../scripts/checks/js-module-linkage.mjs')

test('WHAT[verification-system-004] module loadability verifier is red when a dist module fails to load', async () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-module-load-red-'))
  const distRoot = join(temporaryRoot, 'dist')
  mkdirSync(distRoot, { recursive: true })

  try {
    writeFileSync(join(distRoot, 'broken.js'), 'throw new Error("top-level explosion in broken emitted module")\n')
    const failures = await validateModuleLoadability(distRoot)
    assert.ok(failures.length > 0, 'validateModuleLoadability must fail for modules that throw on load')
    assert.match(failures[0], /broken\.js: failed to load emitted module/)
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})
}

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


