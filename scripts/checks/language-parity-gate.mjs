#!/usr/bin/env node
/**
 * ARCH-016 Gate C — Language Parity (HOST-026 / PROMPT-017).
 * Every provider semantic directory must contain en.md + zh-CN.md locale leaves.
 *
 * Usage: node scripts/checks/language-parity-gate.mjs
 */

import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PROVIDER_ROOT = 'resources/provider'
export const LOCALE_FILES = Object.freeze(['en.md', 'zh-CN.md'])
export const PROVIDER_RESOURCES_REL = 'src/Wanxiangshu/Infrastructure/Resources/ProviderResources.fs'

const norm = (p) => p.replace(/\\/g, '/')

/**
 * Directories under provider root that host at least one locale leaf.
 * @param {string} providerAbs
 * @returns {string[]} semantic paths relative to provider root (e.g. role/manager)
 */
export const listSemanticResourceDirs = (providerAbs) => {
  if (!existsSync(providerAbs)) return []
  const dirs = new Set()
  for (const abs of walk(providerAbs)) {
    const base = abs.replace(/\\/g, '/').split('/').pop() ?? ''
    if (!LOCALE_FILES.includes(base)) continue
    dirs.add(norm(relative(providerAbs, dirname(abs))))
  }
  return [...dirs].sort()
}

/**
 * @typedef {{ code: string, path: string, detail?: string }} Violation
 */

/**
 * @param {string[]} semanticDirs paths relative to provider root
 * @param {string} providerAbs absolute provider root
 * @returns {Violation[]}
 */
export const scanParity = (semanticDirs, providerAbs) => {
  /** @type {Violation[]} */
  const violations = []
  for (const semantic of semanticDirs) {
    for (const locale of LOCALE_FILES) {
      const rel = join(PROVIDER_ROOT, semantic, locale)
      const abs = join(providerAbs, semantic, locale)
      if (!existsSync(abs)) {
        violations.push({
          code: locale === 'en.md' ? 'missing-en' : 'missing-zh-cn',
          path: rel,
          detail: `locale leaf missing for semantic resource ${semantic}`,
        })
      }
    }
  }
  return violations
}

/**
 * @param {string} text
 * @returns {Violation[]}
 */
export const scanProviderResourcesHook = (text) => {
  /** @type {Violation[]} */
  const violations = []
  if (!text.includes('requireLanguagePair')) {
    violations.push({
      code: 'missing-require-language-pair',
      path: PROVIDER_RESOURCES_REL,
      detail: 'ProviderResources must expose requireLanguagePair (ARCH-016 Gate C hook)',
    })
  }
  if (!text.includes('ProviderLanguage.English') || !text.includes('ProviderLanguage.SimplifiedChinese')) {
    violations.push({
      code: 'missing-language-pair-loop',
      path: PROVIDER_RESOURCES_REL,
      detail: 'ProviderResources must check English + SimplifiedChinese',
    })
  }
  if (!text.includes('resourceFileName')) {
    violations.push({
      code: 'missing-resource-file-name',
      path: PROVIDER_RESOURCES_REL,
      detail: 'ProviderResources must resolve locale leaf via ProviderLanguage.resourceFileName',
    })
  }
  return violations
}

/**
 * @param {string} [repoRoot]
 * @returns {{ ok: boolean, violations: Violation[], semanticDirs: string[] }}
 */
export const scanRepo = (repoRoot = process.cwd()) => {
  /** @type {Violation[]} */
  const violations = []
  const providerAbs = resolve(repoRoot, PROVIDER_ROOT)

  if (!existsSync(providerAbs)) {
    violations.push({ code: 'missing-provider-root', path: PROVIDER_ROOT, detail: 'provider root missing' })
    return { ok: false, violations, semanticDirs: [] }
  }

  const semanticDirs = listSemanticResourceDirs(providerAbs)
  if (semanticDirs.length === 0) {
    violations.push({
      code: 'no-semantic-resources',
      path: PROVIDER_ROOT,
      detail: 'no semantic resource directories with locale leaves found',
    })
  } else {
    violations.push(...scanParity(semanticDirs, providerAbs))
  }

  const hookAbs = resolve(repoRoot, PROVIDER_RESOURCES_REL)
  if (!existsSync(hookAbs)) {
    violations.push({ code: 'missing-file', path: PROVIDER_RESOURCES_REL, detail: 'ProviderResources.fs missing' })
  } else {
    violations.push(...scanProviderResourcesHook(readFileSync(hookAbs, 'utf8')))
  }

  return { ok: violations.length === 0, violations, semanticDirs }
}

const formatViolation = (v) => {
  const detail = v.detail ? ` — ${v.detail}` : ''
  return `  ${v.path}: ${v.code}${detail}`
}

const runCli = () => {
  const result = scanRepo()
  if (result.ok) {
    console.log(
      `language-parity-gate: OK — ${result.semanticDirs.length} semantic resource(s); ` +
        'each has en.md + zh-CN.md; ProviderResources.requireLanguagePair present',
    )
    process.exit(0)
  }
  console.error(`language-parity-gate: ${result.violations.length} violation(s)\n`)
  for (const v of result.violations) console.error(formatViolation(v))
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
