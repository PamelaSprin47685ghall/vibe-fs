#!/usr/bin/env node
// Wave 1: Production Ownership Graph — semantic-owners gate.
//
// Every production .fs file must have exactly one primary semantic owner.
// The manifest at scripts/checks/semantic-owners.json is the adjudicated baseline.
// New files must be added to the manifest; the gate fails on unmanifested files.
//
// APPLIES-TO = governance scope (Meta packages observe source-wide rules).
// semantic-owners.json = primary ownership (one owner per file).

import { readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const ROOT = fileURLToPath(new URL('../..', import.meta.url))
const PRODUCTION_ROOT = 'src/Wanxiangshu'
const MANIFEST = join(ROOT, 'scripts/checks/semantic-owners.json')

const manifest = JSON.parse(readFileSync(MANIFEST, 'utf8'))
const manifestMap = new Map(manifest.ownership.map((e) => [e.path, e.owner]))

const productionFiles = walk(join(ROOT, PRODUCTION_ROOT), ['.fs'])
const unmanifested = []
const stale = []

for (const file of productionFiles) {
  const relPath = relative(ROOT, file).replace(/\\/g, '/')
  if (!manifestMap.has(relPath)) {
    unmanifested.push(relPath)
  }
}

for (const { path } of manifest.ownership) {
  const full = join(ROOT, path)
  try {
    statSync(full)
  } catch {
    stale.push(path)
  }
}

if (unmanifested.length > 0) {
  console.error('semantic-owners: UNMANIFESTED production files:')
  for (const path of unmanifested.sort()) console.error(`  ${path}`)
  console.error(`\nAdd each file to scripts/checks/semantic-owners.json with its primary owner.`)
  process.exit(1)
}

if (stale.length > 0) {
  console.error('semantic-owners: STALE manifest entries (file deleted):')
  for (const path of stale.sort()) console.error(`  ${path}`)
  console.error(`\nRemove deleted files from scripts/checks/semantic-owners.json.`)
  process.exit(1)
}

console.log(`semantic-owners: OK — ${productionFiles.length} files, ${new Set(manifestMap.values()).size} owners`)
