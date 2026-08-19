#!/usr/bin/env node
// Wave 1: Production Ownership Graph — report-only.
// Maps every production .fs file to exactly one primary semantic owner.
//
// APPLIES-TO = governance/observation scope, not primary ownership.
// Meta packages (structured-workflow, js-semantic-surface, crash-reconciliation)
// govern source-wide structural rules but do not own individual files.
//
// This script computes primary ownership by:
//   1. Excluding Meta governance packages from the owner match
//   2. Matching remaining packages' APPLIES-TO against each file
//   3. Reporting UNOWNED / MULTI-OWNER for human adjudication

import { readFileSync, writeFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const ROOT = fileURLToPath(new URL('../..', import.meta.url))
const PRODUCTION_ROOT = 'src/Wanxiangshu'
const REQUIREMENTS_ROOT = 'requirements'

// Meta packages: governance scope only, not primary file ownership.
// structured-workflow: governs all .fs for CE/PC structural rules
// js-semantic-surface: governs all *Surface.fs for JS boundary rules
// crash-reconciliation: governs *Recovery*/*Restart*/*Reconcile* for recovery rules
const META_PACKAGES = new Set([
  'structured-workflow',
  'js-semantic-surface',
  'crash-reconciliation',
])

// ── Pattern matching ────────────────────────────────────────────────────────

function globToRegex(pattern) {
  const negate = pattern.startsWith('!')
  const p = negate ? pattern.slice(1) : pattern
  let i = 0
  let re = ''
  if (p.startsWith('/')) {
    i = 1
  } else {
    re += '(?:^|.*/)'
  }
  while (i < p.length) {
    const c = p[i]
    if (c === '*') {
      if (p[i + 1] === '*') {
        i += 2
        if (p[i] === '/') i += 1
        re += '.*'
      } else {
        re += '[^/]*'
        i += 1
      }
    } else if (c === '?') {
      re += '[^/]'
      i += 1
    } else if ('.+^${}()|[]\\'.includes(c)) {
      re += '\\' + c
      i += 1
    } else {
      re += c
      i += 1
    }
  }
  re += '$'
  return { negate, regex: new RegExp(re) }
}

function parseAppliesTo(filePath) {
  const text = readFileSync(filePath, 'utf8')
  const patterns = []
  for (const line of text.split('\n')) {
    const trimmed = line.trim()
    if (!trimmed || trimmed.startsWith('#')) continue
    patterns.push(globToRegex(trimmed))
  }
  return patterns
}

function matchesPatterns(relPath, patterns) {
  let matched = false
  for (const { negate, regex } of patterns) {
    if (regex.test(relPath)) {
      matched = !negate
    }
  }
  return matched
}

// ── Build package → patterns map (excluding Meta) ───────────────────────────

const packages = []
const pkgDir = join(ROOT, REQUIREMENTS_ROOT)
for (const entry of walk(pkgDir)) {
  const rel = relative(pkgDir, entry).replace(/\\/g, '/')
  const segments = rel.split('/')
  if (segments.length === 2 && segments[1] === 'APPLIES-TO') {
    const pkg = segments[0]
    if (META_PACKAGES.has(pkg)) continue
    const patterns = parseAppliesTo(entry)
    packages.push({ pkg, patterns })
  }
}

// ── Scan production files ───────────────────────────────────────────────────

const productionFiles = walk(join(ROOT, PRODUCTION_ROOT), ['.fs'])
const unowned = []
const multiOwner = []
const owned = []

for (const file of productionFiles) {
  const relPath = relative(ROOT, file).replace(/\\/g, '/')
  const owners = []
  for (const { pkg, patterns } of packages) {
    if (matchesPatterns(relPath, patterns)) {
      owners.push(pkg)
    }
  }
  if (owners.length === 0) {
    unowned.push(relPath)
  } else if (owners.length > 1) {
    multiOwner.push({ path: relPath, owners })
  } else {
    owned.push({ path: relPath, owner: owners[0] })
  }
}

// ── Report ──────────────────────────────────────────────────────────────────

console.log(`Production files: ${productionFiles.length}`)
console.log(`Owned (single):   ${owned.length}`)
console.log(`Multi-owner:      ${multiOwner.length}`)
console.log(`Unowned:          ${unowned.length}`)

if (unowned.length > 0) {
  console.log('\n--- UNOWNED ---')
  for (const path of unowned.sort()) console.log(path)
}

if (multiOwner.length > 0) {
  console.log('\n--- MULTI-OWNER ---')
  for (const { path, owners } of multiOwner.sort((a, b) => a.path.localeCompare(b.path))) {
    console.log(`${path}  ←  [${owners.join(', ')}]`)
  }
}

// Output ownership graph as JSON
const graph = {
  total: productionFiles.length,
  owned: owned.length,
  multi: multiOwner.length,
  unowned: unowned.length,
  metaPackages: [...META_PACKAGES],
  unownedFiles: unowned.sort(),
  multiOwnerFiles: multiOwner
    .sort((a, b) => a.path.localeCompare(b.path))
    .map(({ path, owners }) => ({ path, owners })),
  ownership: owned
    .sort((a, b) => a.path.localeCompare(b.path))
    .map(({ path, owner }) => ({ path, owner })),
}

const outPath = join(ROOT, 'scripts/checks/ownership-graph.json')
writeFileSync(outPath, JSON.stringify(graph, null, 2) + '\n', 'utf8')
console.log(`\nGraph written to ${relative(ROOT, outPath)}`)
