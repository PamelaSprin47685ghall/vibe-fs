#!/usr/bin/env node
// JS-SEMANTIC-SURFACE-003/005 manifest gate.
//
// Registration grants no authority by itself. Every registered module must be
// owned by a current requirement, governed by current WHAT laws,
// implemented by a compiled source file, and imported by a real
// executable contract test.

import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'

import { SURFACE_MANIFEST } from '../lib/test-surface-scan.mjs'
import { parseModule, walkSyntax } from '../lib/js-syntax.mjs'
import { walk } from '../lib/walk.mjs'

export const WHAT_ID = /^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9]+)?)\b/gm

const normalize = (path) => path.replace(/\\/g, '/')
const read = (root, path) => readFileSync(join(root, path), 'utf8')

const WHAT_TAG = /WHAT\[([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9]+)?)\]/g

export const whatIds = (text) => {
  WHAT_ID.lastIndex = 0
  WHAT_TAG.lastIndex = 0
  const legacy = [
    ...[...text.matchAll(WHAT_ID)].map((match) => match[1]),
    ...[...text.matchAll(WHAT_TAG)].map((match) => match[1]),
  ]
  const bracketNums = [...text.matchAll(/^#{1,6}\s+\[(\d{3}[a-z]?)\]/gm)].map((match) => match[1])
  return [...new Set([...legacy, ...bracketNums])]
}

const sourceCompileStem = (source) => {
  const prefix = 'src/Wanxiangshu/'
  if (!source.startsWith(prefix) || !source.endsWith('.fs')) return undefined
  return source.slice(prefix.length, -'.fs'.length)
}

export const validateSurfaceManifest = (manifest = SURFACE_MANIFEST, root = process.cwd()) => {
  const failures = []
  const fail = (message) => failures.push(message)
  const requirements = join(root, 'requirements')
  // W5 cutover: the wrapper aggregate is deleted. Production sources are
  // enumerated by walking shard .fsproj declarations — that single set is
  // what a test source label must name before the code below accepts it.
  const productionSources = new Set()
  const shardRoot = join(root, 'src/Wanxiangshu')
  for (const file of walk(shardRoot, ['.fsproj'])) {
    const text = readFileSync(file, 'utf8')
    for (const match of text.matchAll(/<Compile\s+Include="([^"]+\.fs)"\s*\/?\s*>/g)) {
      productionSources.add(match[1].replace(/\\/g, '/'))
    }
  }
  const testFiles = walk(requirements, ['.test.mjs'])
  const testSources = testFiles.map((file) => {
    const source = readFileSync(file, 'utf8')
    const syntax = parseModule(source, file)
    return { file, source, syntax }
  })
  const seenModules = new Set()

  if (!Array.isArray(manifest)) {
    return ['surface manifest must be an array']
  }

  // Pre-collect all dist imports across all test files
  const importedDistPaths = new Set()
  for (const { syntax } of testSources) {
    if (!syntax) continue
    walkSyntax(syntax, (node) => {
      if (
        (node.type === 'ImportDeclaration' || node.type === 'ImportExpression') &&
        typeof node.source?.value === 'string'
      ) {
        const val = node.source.value
        const idx = val.indexOf('dist/')
        if (idx !== -1) {
          importedDistPaths.add(val.slice(idx + 5))
        }
      }
    })
  }

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
    if (!/^[a-z0-9-]+$/.test(entry.owner ?? '')) fail(`${label}: owner must be a package slug`)
    if (!Array.isArray(entry.laws) || entry.laws.length === 0 || entry.laws.some((law) => typeof law !== 'string')) {
      fail(`${label}: laws must be a non-empty list of law ids`)
    }
    if (!['json', 'opaque-capability'].includes(entry.representation)) {
      fail(`${label}: invalid representation ${entry.representation}`)
    }
    if (!['pure', 'resource'].includes(entry.kind)) fail(`${label}: invalid kind ${entry.kind}`)

    const ownerWhatPath = `requirements/${entry.owner}/WHAT.md`
    if (!existsSync(join(root, ownerWhatPath))) {
      fail(`${label}: missing owner WHAT.md (${ownerWhatPath})`)
      continue
    }

    const laws = Array.isArray(entry.laws) ? entry.laws : []
    const lawOwners = entry.lawOwners && typeof entry.lawOwners === 'object' ? entry.lawOwners : {}
    for (const law of laws) {
      const lawOwner = typeof lawOwners[law] === 'string' ? lawOwners[law] : entry.owner
      const lawWhatPath = `requirements/${lawOwner}/WHAT.md`
      if (!existsSync(join(root, lawWhatPath))) {
        fail(`${label}: law ${law} owner WHAT is missing (${lawWhatPath})`)
        continue
      }
      const lawIds = new Set(whatIds(read(root, lawWhatPath)))
      const numMatch = /-(\d{3}[a-z]?)$/.exec(law)
      const lawNum = numMatch ? numMatch[1] : null
      if (!lawIds.has(law) && !(lawNum && lawIds.has(lawNum))) fail(`${label}: law ${law} is absent from ${lawWhatPath}`)
    }

    if (typeof entry.source !== 'string' || !existsSync(join(root, entry.source))) {
      fail(`${label}: missing production source ${entry.source}`)
    }

    if (typeof entry.module === 'string' && !existsSync(join(root, 'dist', entry.module))) {
      fail(`${label}: missing emitted surface dist/${entry.module}`)
    }
    const compileStem = sourceCompileStem(entry.source ?? '')
    if (!compileStem) {
      fail(`${label}: source must be a src/Wanxiangshu .fs path`)
    } else {
      // compileStem is already repo-root-relative under src/Wanxiangshu/.
      const rel = `${compileStem}.fs`.replace(/\\/g, '/')
      if (!productionSources.has(rel)) {
        fail(`${label}: ${compileStem}.fs is not compiled by the canonical source inventory`)
      }
    }

    if (typeof entry.module === 'string' && !importedDistPaths.has(entry.module)) {
      fail(`${label}: no .test.mjs imports the registered surface`)
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
  console.log(`js-surface-manifest: OK — ${manifest.length} registered surfaces, laws and contract imports closed`)
  return 0
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href
if (isMain) process.exit(run())
