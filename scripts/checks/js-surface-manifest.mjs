#!/usr/bin/env node
// Surface manifest gate (build step).
//
// The manifest (SURFACE_MANIFEST in ../lib/test-surface-scan.mjs) lists the
// dist modules used by semantic tests. This check compares the registry
// against the build: every registered module is actually emitted under dist/
// and its production source is part of the canonical compile inventory.

import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'

import { SURFACE_MANIFEST } from '../lib/test-surface-scan.mjs'
import { walk } from '../lib/walk.mjs'

const sourceCompileStem = (source) => {
  const prefix = 'src/Wanxiangshu/'
  if (!source.startsWith(prefix) || !source.endsWith('.fs')) return undefined
  return source.slice(prefix.length, -'.fs'.length)
}

export const validateSurfaceManifest = (manifest = SURFACE_MANIFEST, root = process.cwd()) => {
  const failures = []
  const fail = (message) => failures.push(message)

  // W5 cutover: the wrapper aggregate is deleted. Production sources are
  // enumerated by walking shard .fsproj declarations — that single set is
  // what a registered source must belong to.
  const productionSources = new Set()
  const shardRoot = join(root, 'src/Wanxiangshu')
  for (const file of walk(shardRoot, ['.fsproj'])) {
    const text = readFileSync(file, 'utf8')
    for (const match of text.matchAll(/<Compile\s+Include="([^"]+\.fs)"\s*\/?\s*>/g)) {
      productionSources.add(match[1].replace(/\\/g, '/'))
    }
  }

  if (!Array.isArray(manifest)) {
    return ['surface manifest must be an array']
  }

  const seenModules = new Set()
  for (const entry of manifest) {
    if (entry === null || typeof entry !== 'object' || Array.isArray(entry)) {
      fail('manifest entry must be an object')
      continue
    }
    const label = typeof entry.module === 'string' ? entry.module : '<missing module>'
    if (seenModules.has(label)) fail(`${label}: duplicate manifest module`)
    seenModules.add(label)

    if (!/^[A-Za-z0-9_-]+(?:\/[A-Za-z0-9_-]+)*\.js$/.test(entry.module ?? '')) {
      fail(`${label}: module must be a relative emitted .js path`)
    }

    if (typeof entry.source !== 'string' || !entry.source.startsWith('src/Wanxiangshu/') || !entry.source.endsWith('.fs')) {
      fail(`${label}: source must be a src/Wanxiangshu .fs path`)
      continue
    }

    const compileStem = sourceCompileStem(entry.source)
    if (!productionSources.has(`${compileStem}.fs`)) {
      fail(`${label}: ${compileStem}.fs is not compiled by the canonical source inventory`)
    }

    if (typeof entry.module === 'string' && !existsSync(join(root, 'dist', entry.module))) {
      fail(`${label}: missing emitted surface dist/${entry.module}`)
    }
  }
  return failures
}

export const run = ({ root = process.cwd(), manifest = SURFACE_MANIFEST } = {}) => {
  const failures = validateSurfaceManifest(manifest, root)
  if (failures.length > 0) {
    console.error(`js-surface-manifest: ${failures.length} violation(s)`)
    for (const failure of failures) console.error(`  ${failure}`)
    return 1
  }
  console.log(`js-surface-manifest: OK — ${manifest.length} registered surfaces emitted and compiled`)
  return 0
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href
if (isMain) process.exit(run())
