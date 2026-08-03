#!/usr/bin/env node
// Repository layout gate. Layer 0 static check, first segment of `gate:static`.
//
// Enforces invariants that architecture-gate.mjs cannot: architecture-gate
// scans only its PRODUCTION_ROOT (`src/Wanxiangshu.Next/`), so a source file dropped anywhere
// else — the repository root, a stale copy directory — is invisible to every
// gate that lives in that scan. This gate owns the boundary of the boundary:
// where sources may live at all, what the root may contain, and how a file's
// name relates to the module it declares.
//
//   node scripts/repository-layout-gate.mjs

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { basename, dirname, extname, join, normalize, relative } from 'node:path'
import { walk } from './repo-scan.mjs'

const ROOT = process.cwd()

// ── gate 1: root whitelist ─────────────────────────────────────────────────
// The repository root is a control surface, not a source tree. Every file that
// may appear there must be named explicitly; "discover and accept" is how a
// misplaced source becomes invisible. Directories are open (their contents are
// governed by the other gates), but a directory at the root is still subject to
// gate 2 for source extensions.
const ROOT_ALLOWLIST = new Set([
  'README.md',
  'CHANGELOG.md',
  'LICENSE',
  'AGENTS.md',
  'package.json',
  'package-lock.json',
  'Directory.Build.props',  'dotnet-tools.json',
  '.gitignore',
  'global.json',
])

const ROOT_FORBIDDEN_EXTENSIONS = new Set(['.fs', '.fsx', '.fsproj', '.js', '.mjs', '.ts', '.toml'])

// ── gate 2: production source root ──────────────────────────────────────────
// All production `.fs` / `.fsproj` must live under exactly one root. The root
// itself is a named constant so any future relocation is a one-line change that
// the gate then verifies.
const PRODUCTION_SOURCE_ROOT = 'src/Wanxiangshu.Next'
const SOURCE_EXTENSIONS = ['.fs', '.fsproj']

// ── gate 4: file/module name agreement ─────────────────────────────────────
// `Foo.fs` declaring `module Bar` is a drift signal: the path promises one
// concept and the code delivers another. The check applies only to files whose
// top level IS a module (no `namespace` declaration): for those, the module
// name should match the file stem. A namespace-only file may freely contain
// helper modules (`module NodeFsBoot` inside `Boot.fs`) — the namespace governs,
// and matching every inner module against the stem would flag legitimate
// structure. Multi-type namespace files need no binding either.
const MODULE_DECLARATION = /^module\s+(?:private\s+|internal\s+|public\s+)*([A-Za-z_][A-Za-z0-9_]*)\s*=/m
const NAMESPACE_DECLARATION = /^namespace\s+/m

// ── gate 5: duplicate source detection ─────────────────────────────────────
// Cheap structural duplicate probes: identical (namespace + module) pairs, and
// byte-identical normalized bodies. No AST — the point is catching "same file
// copied into two roots", which byte identity detects outright.

const violations = []
const fail = (gate, message) => violations.push({ gate, message })

// ── gate 1 ─────────────────────────────────────────────────────────────────

const rootEntries = readdirSync(ROOT, { withFileTypes: true })
for (const entry of rootEntries) {
  if (entry.name.startsWith('.') && entry.name !== '.gitignore') continue
  if (entry.isDirectory()) continue
  if (ROOT_ALLOWLIST.has(entry.name)) continue

  const ext = extname(entry.name)
  if (ROOT_FORBIDDEN_EXTENSIONS.has(ext)) {
    fail('root-whitelist', `forbidden at repository root: '${entry.name}' (extension ${ext})`)
  } else {
    fail('root-whitelist', `unlisted file at repository root: '${entry.name}' — add to ROOT_ALLOWLIST with a reason`)
  }
}

// ── gate 2 ─────────────────────────────────────────────────────────────────

const productionRootAbs = join(ROOT, PRODUCTION_SOURCE_ROOT)
const allSourceFiles = walk(ROOT, SOURCE_EXTENSIONS).filter((file) => {
  const norm = normalize(file)
  return !norm.startsWith(`${ROOT}/node_modules/`) && !norm.startsWith(`${ROOT}/build/`) && !norm.startsWith(`${ROOT}/.git/`)
})

const sourceOutsideRoot = allSourceFiles.filter((file) => {
  const rel = relative(ROOT, file)
  return rel !== PRODUCTION_SOURCE_ROOT && !rel.startsWith(`${PRODUCTION_SOURCE_ROOT}/`)
})

for (const file of sourceOutsideRoot) {
  fail('source-root', `production source outside ${PRODUCTION_SOURCE_ROOT}/: ${relative(ROOT, file)}`)
}

// Gate 2 also demands the source root itself exist; an empty scan must not pass.
if (!statSync(productionRootAbs, { throwIfNoEntry: false })?.isDirectory()) {
  fail('source-root', `production source root '${PRODUCTION_SOURCE_ROOT}/' does not exist`)
} else {
  const underRoot = allSourceFiles.filter((file) => relative(ROOT, file).startsWith(`${PRODUCTION_SOURCE_ROOT}/`))
  if (underRoot.length < 10) {
    fail('source-root', `source root scan returned only ${underRoot.length} files — empty scan would make this gate vacuous`)
  }
}

// ── gate 4 ─────────────────────────────────────────────────────────────────

for (const file of allSourceFiles) {
  if (extname(file) !== '.fs') continue
  const rel = relative(ROOT, file)
  if (!rel.startsWith(`${PRODUCTION_SOURCE_ROOT}/`)) continue

  const text = readFileSync(file, 'utf8')
  if (NAMESPACE_DECLARATION.test(text)) continue // namespace file: inner modules are legitimate
  const match = MODULE_DECLARATION.exec(text)
  if (!match) continue // multi-type file with no module binding

  const declared = match[1]
  const stem = basename(file, '.fs')
  const expected = stem.split('.')[0] // Orchestrator.Types.fs → Orchestrator (namespace governs)

  if (declared !== expected && declared !== stem) {
    fail(
      'file-module-name',
      `${rel}: file stem '${stem}' but declares 'module ${declared}'`,
    )
  }
}

// ── gate 5 ─────────────────────────────────────────────────────────────────

const byModule = new Map()
for (const file of allSourceFiles) {
  if (extname(file) !== '.fs') continue
  const rel = relative(ROOT, file)
  if (!rel.startsWith(`${PRODUCTION_SOURCE_ROOT}/`)) continue

  const text = readFileSync(file, 'utf8')
  // Only files whose top level is a module participate: namespace files freely
  // aggregate (that is their purpose), so keying them would flag the normal case.
  if (NAMESPACE_DECLARATION.test(text)) continue
  const moduleMatch = /^module\s+(?:private\s+|internal\s+|public\s+)*([A-Za-z_][A-Za-z0-9_.]*)/m.exec(text)
  const key = moduleMatch?.[1] ?? ''
  if (!key) continue

  if (!byModule.has(key)) byModule.set(key, [])
  byModule.get(key).push(rel)
}

for (const [key, files] of byModule) {
  if (files.length > 1) {
    fail('duplicate-source', `same namespace::module '${key}' in ${files.join(', ')}`)
  }
}

// byte-identical bodies (normalized whitespace) across the source root
const byBody = new Map()
for (const file of allSourceFiles) {
  if (extname(file) !== '.fs') continue
  const rel = relative(ROOT, file)
  if (!rel.startsWith(`${PRODUCTION_SOURCE_ROOT}/`)) continue
  const body = readFileSync(file, 'utf8').replace(/\s+/g, ' ').trim()
  if (body.length < 200) continue // ignore trivial files
  if (!byBody.has(body)) byBody.set(body, [])
  byBody.get(body).push(rel)
}

for (const [body, files] of byBody) {
  if (files.length > 1) {
    fail('duplicate-source', `byte-identical bodies in ${files.join(', ')}`)
  }
}

// ── report ─────────────────────────────────────────────────────────────────

if (violations.length === 0) {
  console.log(`repository-layout-gate: OK — ${allSourceFiles.length} source files under ${PRODUCTION_SOURCE_ROOT}/`)
  process.exit(0)
}

const byGate = new Map()
for (const { gate, message } of violations) {
  if (!byGate.has(gate)) byGate.set(gate, [])
  byGate.get(gate).push(message)
}

console.error(`repository-layout-gate: ${violations.length} violation(s)\n`)
for (const [gate, messages] of byGate) {
  console.error(`${gate} (${messages.length})`)
  for (const message of messages) console.error(`  ${message}`)
  console.error('')
}
process.exit(1)
