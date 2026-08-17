import assert from 'node:assert/strict'
import test from 'node:test'

import { assessIntegrationEntryCoverage } from './support/integration-entry-coverage.mjs'

const assess = (discoveredTests, wiredTests, childOwnedPrefixes = []) =>
  assessIntegrationEntryCoverage({ discoveredTests, wiredTests, childOwnedPrefixes })

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

test('WHAT[VERIFICATION-SYSTEM-009] integration entry coverage delegates declared child-owned suites', () => {
  assert.deepEqual(
    assess(
      [
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/distribution/tests/integration/package/install.test.mjs',
      ],
      ['requirements/a/tests/integration/a.test.mjs'],
      ['requirements/distribution/tests/integration/package/'],
    ),
    { ok: true, missingFromEntry: [], staleEntry: [], duplicateWiring: [] },
  )
})
