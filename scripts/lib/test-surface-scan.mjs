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
  'requirements/execution-model-routing/tests/process-shared-routing.test.mjs',
  'requirements/distribution/tests/pack-closure.test.mjs',
  'requirements/distribution/tests/cwd-independent-resources.test.mjs',
  // Charter self-test: its subject IS the forbidden patterns (representation
  // validator), so it must be able to spell them out.
  'requirements/js-semantic-surface/tests/surface-charter.test.mjs',
])

/**
 * Registered semantic-surface modules (JS-SEMANTIC-SURFACE-002/003).
 *
 * A semantic test may import a registered surface directly: the surface IS the
 * legal entry point (owner boundary translation, JSON-shaped in/out). Deep
 * imports of any other dist module remain debt. Register here when a surface
 * is established (P3 pilot: ForkChildPayloadSurface).
 */
export const SURFACE_MODULES = [
  'Execution/Delegation/Fork/Surface.js',
  'OpenCode/Host/QuiescenceSurface.js',
]

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

/** All semantic test files under requirements/ (excludes e2e/ and integration/). */
export const semanticTestFiles = (root = REQUIREMENTS_ROOT) =>
  walk(root, ['.test.mjs']).filter((abs) => {
    const rel = relative(process.cwd(), abs).replace(/\\/g, '/')
    return !rel.includes('/tests/e2e/') && !rel.includes('/tests/integration/')
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
