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

const WHAT_TAG = /WHAT\[([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9]+)?)\]/g

export const whatIds = (text) => {
  WHAT_ID.lastIndex = 0
  WHAT_TAG.lastIndex = 0
  return [
    ...new Set([
      ...[...text.matchAll(WHAT_ID)].map((match) => match[1]),
      ...[...text.matchAll(WHAT_TAG)].map((match) => match[1]),
    ]),
  ]
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

/**
 * A contract import is evidence only when its binding is used after the import.
 * Merely importing an emitted module from a dead helper does not prove an
 * executable semantic contract.
 */
export const usesSurface = (source, module) => {
  const text = stripComments(source)
  const target = escapeRegExp(module)
  const staticPattern = new RegExp(`\\bimport\\s+([\\s\\S]+?)\\s+from\\s*['"][^'"]*dist/${target}['"]`, 'g')
  let match
  while ((match = staticPattern.exec(text)) !== null) {
    const clause = match[1].trim()
    const bindings = []
    const namespace = clause.match(/\*\s+as\s+([A-Za-z_$][\w$]*)/)
    if (namespace) bindings.push(namespace[1])
    const named = clause.match(/\{([\s\S]*)\}/)
    if (named) {
      for (const item of named[1].split(',')) {
        const local = item.trim().split(/\s+as\s+/).at(-1)?.trim()
        if (local && /^[A-Za-z_$][\w$]*$/.test(local)) bindings.push(local)
      }
    }
    const defaultBinding = clause.match(/^([A-Za-z_$][\\w$]*)/)
    if (defaultBinding) bindings.push(defaultBinding[1])
    const rest = text.slice(match.index + match[0].length)
    if (bindings.some((binding) => new RegExp(`\\b${escapeRegExp(binding)}\\b`).test(rest))) return true
  }

  const dynamicPattern = new RegExp(`\\b(?:const|let|var)\\s+([A-Za-z_$][\\w$]*)\\s*=\\s*(?:await\\s+)?import\\s*\\(\\s*['"][^'"]*dist/${target}['"]\\s*\\)`, 'g')
  while ((match = dynamicPattern.exec(text)) !== null) {
    const rest = text.slice(match.index + match[0].length)
    if (new RegExp(`\\b${escapeRegExp(match[1])}\\b`).test(rest)) return true
  }

  const destructuredPattern = new RegExp(`\\b(?:const|let|var)\\s+\\{([^}]+)\\}\\s*=\\s*(?:await\\s+)?import\\s*\\(\\s*['"][^'"]*dist/${target}['"]\\s*\\)`, 'g')
  while ((match = destructuredPattern.exec(text)) !== null) {
    const rest = text.slice(match.index + match[0].length)
    const bindings = match[1].split(',').map((item) => item.trim().split(/\s+as\s+|\s*:\s*/).at(-1)?.trim()).filter(Boolean)
    if (bindings.some((binding) => new RegExp(`\\b${escapeRegExp(binding)}\\b`).test(rest))) return true
  }
  return false
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
  const executableFiles = walk(requirements, ['.test.mjs', '.mjs', '.js']).map(normalize)
  const executableSources = executableFiles.map((file) => ({ file, source: readFileSync(file, 'utf8') }))
  const testSources = executableSources.filter(({ file }) => file.endsWith('.test.mjs'))
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
    const lawOwners = entry.lawOwners && typeof entry.lawOwners === 'object' ? entry.lawOwners : {}
    for (const law of laws) {
      const lawOwner = typeof lawOwners[law] === 'string' ? lawOwners[law] : entry.owner
      const lawWhatPath = `requirements/${lawOwner}/WHAT.md`
      const lawProofPath = `requirements/${lawOwner}/PROOF.md`
      if (!existsSync(join(root, lawWhatPath))) {
        fail(`${label}: law ${law} owner WHAT is missing (${lawWhatPath})`)
        continue
      }
      if (!existsSync(join(root, lawProofPath))) {
        fail(`${label}: law ${law} owner PROOF is missing (${lawProofPath})`)
        continue
      }
      const lawIds = new Set(whatIds(read(root, lawWhatPath)))
      const lawProof = read(root, lawProofPath)
      if (!lawIds.has(law)) fail(`${label}: law ${law} is absent from ${lawWhatPath}`)
      if (!proofHasLaw(lawProof, law)) fail(`${label}: law ${law} has no owner PROOF row`)
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
    } else if (!fsproj.includes(`<Compile Include="${compileStem}.fs"/>`)) {
      fail(`${label}: ${compileStem}.fs is not compiled by Wanxiangshu.fsproj`)
    }

    const importedBy = typeof entry.module === 'string' ? executableSources.filter(({ source }) => importsSurface(source, entry.module)) : []
    const activeBy = typeof entry.module === 'string' ? executableSources.filter(({ source }) => usesSurface(source, entry.module)) : []
    const authorizedBy = activeBy.filter(({ file, source }) => {
      const sourceLaws = new Set(whatIds(source))
      return laws.some((law) => {
        const lawOwner = typeof lawOwners[law] === 'string' ? lawOwners[law] : entry.owner
        return sourceLaws.has(law) && file.startsWith(`${requirements}/${lawOwner}/tests/`)
      })
    })
    if (typeof entry.module === 'string' && importedBy.length === 0) {
      fail(`${label}: no .test.mjs imports the registered surface`)
    } else if (typeof entry.module === 'string' && activeBy.length === 0) {
      fail(`${label}: surface import has no active executable use in a .test.mjs`)
    } else if (typeof entry.module === 'string' && authorizedBy.length === 0) {
      fail(`${label}: no active contract test WHAT law authorizes this surface`)
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
