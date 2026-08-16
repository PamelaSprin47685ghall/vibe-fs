// Test-surface debt scanner (P1/P2 shared core).
//
// Scans requirements/**/tests/**/*.test.mjs (excluding e2e/ and integration/,
// which own separate entrypoints and physical-contract gates) and reports five
// debt classes a semantic test must not carry (TASK.md §1):
//
//   A. deep production import   import '...dist/<internal>.js' (not fable_modules)
//   B. Fable export discovery   Object.keys / Object.entries on F# modules
//   C. Fable representation     .tag / .fields / .cases() / FSharp* names / fable_modules
//   D. legacy interop authority member( / bind( / fableInstanceMethod( / prod( /
//                              toList( / caseOf( / payloadOf( / resultOf( / unwrapOption(
//
// Compiler/build verification files (guide-contract emitted-surface pin,
// domain.meta facade self-contract, distribution artifact tests, real
// provider-wire canaries) are exempt by explicit path allowlist: their subject
// IS the compiled artifact, so they are entitled to know dist (TASK.md §1.E).

import { existsSync, readFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from './walk.mjs'

export const REQUIREMENTS_ROOT = join(
  join(join(join(fileURLToPath(import.meta.url), '..'), '..'), '..'),
  'requirements',
)

/** Compiler/build verification: subject is the emitted artifact, not semantics. */
export const BUILD_VERIFICATION_FILES = new Set([
  'requirements/verification-system/tests/guide-contract.test.mjs',
  'requirements/verification-system/tests/domain.meta.test.mjs',
  // Its subject is the coverage/build runner itself, including the literal
  // fable_modules exclusion that keeps the coverage denominator honest.
  'requirements/verification-system/tests/coverage-gate.test.mjs',
  'requirements/distribution/tests/pack-closure.test.mjs',
  'requirements/distribution/tests/cwd-independent-resources.test.mjs',
  // Charter self-test: its subject IS the forbidden patterns (representation
  // validator), so it must be able to spell them out.
  'requirements/js-semantic-surface/tests/surface-charter.test.mjs',
  // Representation validator: its subject IS the forbidden Fable shapes
  // (du-shape / fsharp-type / export-discovery), so it must spell them out.
  'requirements/verification-system/tests/support/js-contract.mjs',
  // Test runner: its subject IS the coverage/build denominator itself,
  // including the literal fable_modules exclusion that keeps coverage honest.
  'requirements/verification-system/tests/run.mjs',
])

/**
 * Registered semantic-surface manifest (JS-SEMANTIC-SURFACE-002/003).
 *
 * A semantic test may import a registered surface directly: the surface IS the
 * legal entry point (owner boundary translation, JSON-shaped in/out). Deep
 * imports of any other dist module remain debt. Register here when a surface
 * is established — registration requires the full manifest below (owner
 * package, governing laws, production source, representation, kind), so a
 * surface exists because a semantic component owns a contract, never because
 * a test wants access (TASK.md §9/§10, PR 4).
 */
export const SURFACE_MANIFEST = [
  {
    module: 'Context/Companion/Blogger/TomlSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-009'],
    source: 'src/Wanxiangshu/Context/Companion/Blogger/TomlSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/DelegatedToolEstimateSurface.js',
    owner: 'delegation',
    laws: ['DELEG-022'],
    source: 'src/Wanxiangshu/Execution/Delegation/DelegatedToolEstimateSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/Surface.js',
    owner: 'delegation',
    laws: ['DELEG-019'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Fission/Surface.js',
    owner: 'intra-participant-parallelism',
    laws: [
      'INTRA-PARTICIPANT-PARALLELISM-001',
      'INTRA-PARTICIPANT-PARALLELISM-002',
      'INTRA-PARTICIPANT-PARALLELISM-003',
      'INTRA-PARTICIPANT-PARALLELISM-004',
      'INTRA-PARTICIPANT-PARALLELISM-005',
      'INTRA-PARTICIPANT-PARALLELISM-006',
      'INTRA-PARTICIPANT-PARALLELISM-007',
      'INTRA-PARTICIPANT-PARALLELISM-008',
      'INTRA-PARTICIPANT-PARALLELISM-009',
      'INTRA-PARTICIPANT-PARALLELISM-011',
      'INTRA-PARTICIPANT-PARALLELISM-013',
    ],
    source: 'src/Wanxiangshu/Execution/Fission/Surface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Foundation/RolesSurface.js',
    owner: 'capability-enforcement',
    laws: ['ENF-002'],
    source: 'src/Wanxiangshu/Foundation/RolesSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Foundation/SyntheticTomlSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-008', 'PROVIDER-PROJECTION-010', 'PROVIDER-PROJECTION-012'],
    source: 'src/Wanxiangshu/Foundation/SyntheticTomlSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Host/Contract/ToolResultBound.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-015'],
    source: 'src/Wanxiangshu/Host/Contract/ToolResultBound.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/QuiescenceSurface.js',
    owner: 'crash-reconciliation',
    laws: ['CRASH-001', 'CRASH-006', 'CRASH-008'],
    source: 'src/Wanxiangshu/OpenCode/Host/QuiescenceSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Tools/ExecutorToolSurface.js',
    owner: 'process-execution',
    laws: ['PROC-011'],
    source: 'src/Wanxiangshu/OpenCode/Tools/ExecutorToolSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/Manager/FinalitySurface.js',
    owner: 'finality',
    laws: [
      'FINALITY-002',
      'FINALITY-003',
      'FINALITY-004',
      'FINALITY-005',
      'FINALITY-006',
      'FINALITY-009',
      'FINALITY-017',
      'FINALITY-019',
      'FINALITY-022',
      'FINALITY-026',
    ],
    source: 'src/Wanxiangshu/Mission/Manager/FinalitySurface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'Repository/Knowledge/Casebook/Surface.js',
    owner: 'knowledge-reuse',
    laws: ['KNOWLEDGE-REUSE-002', 'KNOWLEDGE-REUSE-003', 'KNOWLEDGE-REUSE-004', 'KNOWLEDGE-REUSE-008', 'KNOWLEDGE-REUSE-010'],
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
]

/** Flat module-path allowlist derived from the manifest (scanner regex input). */
export const SURFACE_MODULES = SURFACE_MANIFEST.map((entry) => entry.module)

const SURFACE_ALT = SURFACE_MODULES.map((m) => m.replace(/[.]/g, '\\.')).join('|')

const A_DEEP_IMPORT = new RegExp(
  `(?:from\\s*|import\\s*\\(\\s*)['"][^'"]*dist/(?!fable_modules)(?!(?:${SURFACE_ALT})['"])[^'"]+\\.js['"]`,
)
const B_EXPORT_DISCOVERY = /Object\.(?:keys|entries|values)\(/
const C1_DU_SHAPE = /\.cases\(\)|\.fields\b|\.tag\b/
const C2_FSHARP = /\bFSharp(?:List|Map|Set|Option|Result)\b/
const C3_FABLE_MODULES = /fable_modules/
const D_HELPERS = /\b(?:member|bind|fableInstanceMethod|prod|toList|caseOf|payloadOf|resultOf|unwrapOption)\(/

const RULES = [
  ['deep-dist-import', A_DEEP_IMPORT],
  ['export-discovery', B_EXPORT_DISCOVERY],
  ['du-shape', C1_DU_SHAPE],
  ['fsharp-type', C2_FSHARP],
  ['fable-modules', C3_FABLE_MODULES],
  ['interop-helper', D_HELPERS],
]

/** Scan one file; return [{ line, rule, text }]. */
export const scanFile = (absPath, relPath) => {
  const lines = readFileSync(absPath, 'utf8').split('\n')
  const hits = []
  for (let i = 0; i < lines.length; i++) {
    const text = lines[i]
    for (const [rule, re] of RULES) {
      if (re.test(text)) hits.push({ file: relPath, line: i + 1, rule, text: text.trim() })
    }
  }
  return hits
}

/**
 * Semantic-test zone files under requirements: every .mjs under a package's
 * tests directory (excluding e2e/ and integration/, which own separate
 * entrypoints and physical-contract gates) — *.test.mjs AND support files,
 * fixtures, helpers, *-contract.mjs.
 *
 * TASK.md §7/§21: forbidden knowledge moved from a test file into test
 * support does not reduce debt; the whole semantic-test dependency zone is
 * scanned. The transition facade (support/domain.mjs and its sublayers) and
 * package-local contract adapters remain legal today only as baseline debt
 * that must monotonically shrink.
 */
export const semanticTestFiles = (root = REQUIREMENTS_ROOT) =>
  walk(root, ['.mjs']).filter((abs) => {
    const rel = relative(process.cwd(), abs).replace(/\\/g, '/')
    return rel.includes('/tests/') && !rel.includes('/tests/e2e/') && !rel.includes('/tests/integration/')
  })

/** Full inventory: { <rel-file>: [ {line, rule, text}, ... ] } minus allowlist. */
export const scanAll = (root = REQUIREMENTS_ROOT) => {
  const out = {}
  for (const abs of semanticTestFiles(root)) {
    const rel = relative(process.cwd(), abs).replace(/\\/g, '/')
    if (BUILD_VERIFICATION_FILES.has(rel)) continue
    const hits = scanFile(abs, rel)
    if (hits.length > 0) out[rel] = hits
  }
  return out
}