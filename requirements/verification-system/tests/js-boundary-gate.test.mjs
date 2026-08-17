// requirements/verification-system/tests/js-boundary-gate.test.mjs
//
// WHAT[VERIFICATION-SYSTEM-013] — JS semantic boundary terminal clean-state gate.
// The boundary ratchet has closed: product semantic tests carry zero Fable/
// deep-import/mangled-discovery/interop-helper debt. Only compiler/build
// verification and physical host canaries remain exempt, and only by explicit
// allowlist. The transition facade (interop.mjs / domain.mjs) is fully removed.
//
// This test pins the terminal invariants the gate already enforces so a
// regression is a named contract failure, not an anonymous gate exit code.

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { run as runBoundaryGate, packageLocalContracts } from '../../../scripts/checks/js-boundary-gate.mjs'
import {
  BUILD_VERIFICATION_FILES,
  HOST_PHYSICAL_CANARY_FILES,
  SURFACE_MANIFEST,
  scanAll,
  semanticTestFiles,
} from '../../../scripts/lib/test-surface-scan.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[VERIFICATION-SYSTEM-013] product_semantic_debt_is_zero', () => {
  assert.deepEqual(scanAll(), {}, 'no product semantic test may carry A-D debt')
})

test('WHAT[VERIFICATION-SYSTEM-013] boundary_gate_passes_at_terminal_state', () => {
  assert.equal(runBoundaryGate({ root: ROOT }), 0)
})

test('WHAT[VERIFICATION-SYSTEM-013] no_package_local_contract_adapters', () => {
  assert.deepEqual(packageLocalContracts(ROOT), [],
    'no *-contract.mjs adapter may exist outside verification-system')
})

test('WHAT[VERIFICATION-SYSTEM-013] exemptions_are_only_compiler_distribution_or_host_canary', () => {
  for (const file of [...BUILD_VERIFICATION_FILES, ...HOST_PHYSICAL_CANARY_FILES]) {
    if (!existsSync(join(ROOT, file))) continue
    const ok = file.startsWith('requirements/verification-system/')
      || file.startsWith('requirements/distribution/')
      || file.startsWith('requirements/host-boundary/tests/')
    assert.equal(ok, true, `exempt file ${file} must be compiler/distribution/host-canary`)
  }
})

test('WHAT[VERIFICATION-SYSTEM-013] no_interop_or_domain_facade_imports', () => {
  const facade = /(?:from\s*|import\s*\(\s*)['"][^'"]*(?:interop|domain)\.mjs['"]/
  const violators = semanticTestFiles()
    .filter((f) => facade.test(readFileSync(f, 'utf8')))
    .map((f) => relative(ROOT, f).replace(/\\/g, '/'))
  assert.deepEqual(violators, [], 'no interop.mjs/domain.mjs facade imports may remain')
})

test('WHAT[VERIFICATION-SYSTEM-013] surface_manifest_is_nonempty_and_closed', () => {
  assert.ok(SURFACE_MANIFEST.length > 0, 'the surface registry must not be empty')
})
