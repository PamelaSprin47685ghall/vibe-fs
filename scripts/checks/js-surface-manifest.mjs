#!/usr/bin/env node
// JS-SEMANTIC-SURFACE-003/005 manifest gate.
//
// Registration grants no authority by itself. Every registered module must be
// owned by a current requirement, governed by current WHAT laws with PROOF
// evidence, implemented by a compiled source file, and imported by a real
// executable contract test.

import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'

import { SURFACE_MANIFEST } from '../lib/test-surface-scan.mjs'
import { walk } from '../lib/walk.mjs'

export const WHAT_ID = /^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9]+)?)\b/gm

const normalize = (path) => path.replace(/\\/g, '/')
const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
const read = (root, path) => readFileSync(join(root, path), 'utf8')

export const whatIds = (text) => {
  WHAT_ID.lastIndex = 0
  return [...text.matchAll(WHAT_ID)].map((match) => match[1])
}

/** A PROOF row is executable evidence, not a prose mention in WHY/HOW. */
export const proofHasLaw = (text, law) =>
  text.split('\n').some((line) => line.includes('|') && new RegExp(`\\b${escapeRegExp(law)}\\b`).test(line))

const stripComments = (text) => text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:])\/\/.*$/gm, '$1')

/** Require a direct static/dynamic import in a .test.mjs source, not a comment. */
export const importsSurface = (source, module) => {
  const target = escapeRegExp(module)
  const importPattern = new RegExp(`(?:\\bfrom\\s*|\\bimport\\s*\\(\\s*)['"][^'"]*dist/${target}['"]`)
  return importPattern.test(stripComments(source))
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
  const fsprojPath = join(root, 'src/Wanxiangshu/Wanxiangshu.fsproj')
  const fsproj = existsSync(fsprojPath) ? readFileSync(fsprojPath, 'utf8') : ''
  const testFiles = walk(requirements, ['.test.mjs']).map(normalize)
  const testSources = testFiles.map((file) => ({ file, source: readFileSync(file, 'utf8') }))
  const seenModules = new Set()

  if (!Array.isArray(manifest)) {
    return ['surface manifest must be an array']
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
    const ownerProofPath = `requirements/${entry.owner}/PROOF.md`
    if (!existsSync(join(root, ownerWhatPath))) {
      fail(`${label}: missing owner WHAT.md (${ownerWhatPath})`)
      continue
    }
    if (!existsSync(join(root, ownerProofPath))) {
      fail(`${label}: missing owner PROOF.md (${ownerProofPath})`)
      continue
    }

    const ids = new Set(whatIds(read(root, ownerWhatPath)))
    const proof = read(root, ownerProofPath)
    const laws = Array.isArray(entry.laws) ? entry.laws : []
    for (const law of laws) {
      if (!ids.has(law)) fail(`${label}: law ${law} is absent from ${ownerWhatPath}`)
      if (!proofHasLaw(proof, law)) fail(`${label}: law ${law} has no owner PROOF row`)
    }

    if (typeof entry.source !== 'string' || !existsSync(join(root, entry.source))) {
      fail(`${label}: missing production source ${entry.source}`)
    }
    const compileStem = sourceCompileStem(entry.source ?? '')
    if (!compileStem) {
      fail(`${label}: source must be a src/Wanxiangshu .fs path`)
    } else if (!fsproj.includes(`<Compile Include="${compileStem}.fs"/>`)) {
      fail(`${label}: ${compileStem}.fs is not compiled by Wanxiangshu.fsproj`)
    }

    const importedBy = typeof entry.module === 'string' ? testSources.filter(({ source }) => importsSurface(source, entry.module)) : []
    if (typeof entry.module === 'string' && importedBy.length === 0) {
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
