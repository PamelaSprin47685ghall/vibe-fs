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

import { SURFACE_CONSUMERS, SURFACE_MANIFEST } from '../lib/test-surface-scan.mjs'
import { walk } from '../lib/walk.mjs'

export const WHAT_ID = /^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9]+)?)\b/gm

const normalize = (path) => path.replace(/\\/g, '/')
const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
const read = (root, path) => readFileSync(join(root, path), 'utf8')

/** Extract the requirements package slug from a test file path. */
const packageOfTestFile = (file, requirementsRoot) => {
  const rel = normalize(file).replace(normalize(requirementsRoot) + '/', '')
  const segments = rel.split('/')
  return segments.length > 1 && segments[1] === 'tests' ? segments[0] : null
}

/** Render a path relative to root for error messages. */
const relativePath = (file, root) => normalize(file).replace(normalize(root) + '/', '')

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

/** Remove string literals so a binding name inside a string is not a use.
 *
 * Single/double-quoted strings do not span newlines in JS; template literals do.
 */
const stripStrings = (text) =>
  text
    .replace(/'(?:[^'\\\n]|\\.)*'/g, "''")
    .replace(/"(?:[^"\\\n]|\\.)*"/g, '""')
    .replace(/`(?:[^`\\]|\\.)*`/g, '``')

/** Require a direct static/dynamic import in a .test.mjs source, not a comment. */
export const importsSurface = (source, module) => {
  if (!source.includes(`dist/${module}`)) return false
  const target = escapeRegExp(module)
  const importPattern = new RegExp(`(?:\\bfrom\\s*|\\bimport\\s*\\(\\s*)['"][^'"]*dist/${target}['"]`)
  return importPattern.test(stripComments(source))
}

/**
 * A contract import is evidence only when its binding is used after the import.
 * Merely importing an emitted module from a dead helper does not prove an
 * executable semantic contract.
 *
 * Lexical binding-use check: strips comments/strings, then proves the imported
 * binding appears as an identifier in executable code after the import clause.
 * Recognizes default, namespace, and named import forms (including `as`).
 */
export const usesSurface = (source, module) => {
  if (!source.includes(`dist/${module}`)) return false
  const text = stripComments(source)
  const target = escapeRegExp(module)

  // Static import: `import <clause> from '...dist/<module>'`
  // The clause cannot contain another `import` keyword (that would be a
  // different statement), so we exclude it from the capture.
  const staticPattern = new RegExp(`\\bimport\\s+((?:(?!\\bimport\\b)[\\s\\S])+?)\\s+from\\s*['"][^'"]*dist/${target}['"]`, 'g')
  let match
  while ((match = staticPattern.exec(text)) !== null) {
    const clause = match[1].trim()
    const bindings = []
    // Namespace: `* as ns`
    const namespace = clause.match(/\*\s+as\s+([A-Za-z_$][\w$]*)/)
    if (namespace) bindings.push(namespace[1])
    // Named: `{ a, b as c }`
    const named = clause.match(/\{([\s\S]*)\}/)
    if (named) {
      for (const item of named[1].split(',')) {
        const local = item.trim().split(/\s+as\s+/).at(-1)?.trim()
        if (local && /^[A-Za-z_$][\w$]*$/.test(local)) bindings.push(local)
      }
    }
    // Default: the identifier before `{` or `*`, if any
    const defaultBinding = clause.match(/^([A-Za-z_$][\w$]*)\s*(?:,|$|\{|\*)/)
    if (defaultBinding) bindings.push(defaultBinding[1])
    const rest = text.slice(match.index + match[0].length)
    const codeAfter = stripStrings(rest)
    if (bindings.some((binding) => new RegExp(`\\b${escapeRegExp(binding)}\\b`).test(codeAfter))) return true
  }

  // Dynamic import assigned to a variable: `const m = import('...dist/<module>')`
  const dynamicPattern = new RegExp(`\\b(?:const|let|var)\\s+([A-Za-z_$][\\w$]*)\\s*=\\s*(?:await\\s+)?import\\s*\\(\\s*['"][^'"]*dist/${target}['"]\\s*\\)`, 'g')
  while ((match = dynamicPattern.exec(text)) !== null) {
    const rest = text.slice(match.index + match[0].length)
    if (new RegExp(`\\b${escapeRegExp(match[1])}\\b`).test(stripStrings(rest))) return true
  }

  // Destructured dynamic import: `const { a, b } = import('...dist/<module>')`
  const destructuredPattern = new RegExp(`\\b(?:const|let|var)\\s+\\{([^}]+)\\}\\s*=\\s*(?:await\\s+)?import\\s*\\(\\s*['"][^'"]*dist/${target}['"]\\s*\\)`, 'g')
  while ((match = destructuredPattern.exec(text)) !== null) {
    const rest = text.slice(match.index + match[0].length)
    const bindings = match[1].split(',').map((item) => item.trim().split(/\s+as\s+|\s*:\s*/).at(-1)?.trim()).filter(Boolean)
    const codeAfter = stripStrings(rest)
    if (bindings.some((binding) => new RegExp(`\\b${escapeRegExp(binding)}\\b`).test(codeAfter))) return true
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

  // Reject consumer metadata for modules no longer in the manifest. A stale
  // SURFACE_CONSUMERS entry grants phantom import authority to a surface that
  // no longer exists.
  const manifestModules = new Set(manifest.map((entry) => entry?.module).filter(Boolean))
  for (const consumerModule of Object.keys(SURFACE_CONSUMERS)) {
    if (!manifestModules.has(consumerModule)) {
      fail(`${consumerModule}: stale SURFACE_CONSUMERS entry for unregistered module`)
    }
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

    const importedBy = typeof entry.module === 'string' ? testSources.filter(({ source }) => importsSurface(source, entry.module)) : []
    const activeBy = typeof entry.module === 'string' ? testSources.filter(({ source }) => usesSurface(source, entry.module)) : []
    const consumerPackages = new Set(
      typeof entry.module === 'string' && Array.isArray(SURFACE_CONSUMERS[entry.module]) ? SURFACE_CONSUMERS[entry.module] : [],
    )

    /** A consumer is authorized when it carries a surface law WHAT tag and
     * lives under the law owner's tests directory, or when its package is
     * declared as an explicit cross-owner consumer. */
    const isAuthorizedConsumer = ({ file, source }) => {
      const sourceLaws = new Set(whatIds(source))
      const lawAuthorized = laws.some((law) => {
        const lawOwner = typeof lawOwners[law] === 'string' ? lawOwners[law] : entry.owner
        return sourceLaws.has(law) && file.startsWith(`${requirements}/${lawOwner}/tests/`)
      })
      if (lawAuthorized) return true
      const pkg = packageOfTestFile(file, requirements)
      return pkg !== null && consumerPackages.has(pkg)
    }

    const authorizedBy = activeBy.filter(isAuthorizedConsumer)
    if (typeof entry.module === 'string' && importedBy.length === 0) {
      fail(`${label}: no .test.mjs imports the registered surface`)
    } else if (typeof entry.module === 'string' && activeBy.length === 0) {
      fail(`${label}: surface import has no active executable use in a .test.mjs`)
    } else if (typeof entry.module === 'string' && authorizedBy.length === 0) {
      fail(`${label}: no active contract test WHAT law authorizes this surface`)
    }

    // Per-consumer rejection: every active import must be law-authorized or
    // declared as an explicit cross-owner consumer. An unrelated test that
    // merely imports the surface is a false green, not proof.
    if (typeof entry.module === 'string') {
      for (const consumer of activeBy) {
        if (!isAuthorizedConsumer(consumer)) {
          const pkg = packageOfTestFile(consumer.file, requirements) ?? '?'
          fail(`${label}: unauthorized active import from ${relativePath(consumer.file, root)} (package ${pkg} has no law or declared consumer edge)`)
        }
      }
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
