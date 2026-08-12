#!/usr/bin/env node
/**
 * ARCH-016 Gate C — Language Parity (HOST-026 / PROMPT-017).
 * Every provider semantic directory must contain en.md + zh-CN.md locale leaves.
 * Protocol identifiers (code spans + TipIdentity / hyphenated tool names) must
 * be the same form in both locales.
 *
 * Usage: node scripts/checks/language-parity-gate.mjs
 */

import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PROVIDER_ROOT = 'resources/provider'
export const ENFORCER_ROOT = 'resources/enforcer'
export const LOCALE_FILES = Object.freeze(['en.md', 'zh-CN.md'])
export const PROVIDER_RESOURCES_REL = 'src/Wanxiangshu/Infrastructure/Resources/ProviderResources.fs'
export const STATIC_TOOLS_REL = 'src/Wanxiangshu/Tools/StaticTools.fs'

const norm = (p) => p.replace(/\\/g, '/')

const escapeRegExp = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

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
 * TipIdentity = enforcer tip directory basename.
 * @param {string} enforcerAbs
 * @returns {string[]}
 */
export const listTipIdentities = (enforcerAbs) => {
  if (!existsSync(enforcerAbs)) return []
  return readdirSync(enforcerAbs, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name)
    .sort()
}

/**
 * Hyphenated tool names only — short English verbs (`read`, `run`) are not
 * protocol markers when bare in prose.
 * @param {string} text StaticTools.fs source
 * @returns {string[]}
 */
export const listHyphenatedToolNames = (text) => {
  const block = text.match(/let\s+knownToolNames\s*=\s*\[([\s\S]*?)\]/)
  if (!block) return []
  return [...block[1].matchAll(/"([^"]+)"/g)]
    .map((m) => m[1])
    .filter((name) => name.includes('-'))
    .sort()
}

/**
 * @param {string} text
 * @returns {Set<string>}
 */
export const extractCodeSpans = (text) => {
  const withoutFences = text.replace(/```[\s\S]*?```/g, '')
  /** @type {Set<string>} */
  const spans = new Set()
  for (const hit of withoutFences.matchAll(/`([^`\n]+)`/g)) {
    spans.add(hit[1])
  }
  return spans
}

/**
 * @param {string} text
 * @param {Iterable<string>} catalog
 * @returns {Set<string>}
 */
export const extractCatalogHits = (text, catalog) => {
  /** @type {Set<string>} */
  const hits = new Set()
  for (const id of catalog) {
    if (!id) continue
    if (new RegExp(`\\b${escapeRegExp(id)}\\b`).test(text)) hits.add(id)
  }
  return hits
}

/**
 * @param {string} text
 * @param {{ tipIdentities?: Iterable<string>, toolNames?: Iterable<string> }} [catalogs]
 * @returns {Set<string>}
 */
export const extractProtocolIdentifiers = (text, catalogs = {}) => {
  /** @type {Set<string>} */
  const ids = new Set(extractCodeSpans(text))
  for (const id of extractCatalogHits(text, catalogs.tipIdentities ?? [])) ids.add(id)
  for (const id of extractCatalogHits(text, catalogs.toolNames ?? [])) ids.add(id)
  return ids
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
 * @param {Set<string>} a
 * @param {Set<string>} b
 * @returns {{ onlyA: string[], onlyB: string[] }}
 */
export const setDiff = (a, b) => ({
  onlyA: [...a].filter((x) => !b.has(x)).sort(),
  onlyB: [...b].filter((x) => !a.has(x)).sort(),
})

/**
 * EN/zh-CN protocol identifier sets must be equal (AC20).
 * @param {string[]} semanticDirs
 * @param {string} providerAbs
 * @param {{ tipIdentities?: Iterable<string>, toolNames?: Iterable<string> }} [catalogs]
 * @returns {Violation[]}
 */
export const scanIdentifierParity = (semanticDirs, providerAbs, catalogs = {}) => {
  /** @type {Violation[]} */
  const violations = []
  for (const semantic of semanticDirs) {
    const enAbs = join(providerAbs, semantic, 'en.md')
    const zhAbs = join(providerAbs, semantic, 'zh-CN.md')
    if (!existsSync(enAbs) || !existsSync(zhAbs)) continue
    const enIds = extractProtocolIdentifiers(readFileSync(enAbs, 'utf8'), catalogs)
    const zhIds = extractProtocolIdentifiers(readFileSync(zhAbs, 'utf8'), catalogs)
    const { onlyA: onlyEn, onlyB: onlyZh } = setDiff(enIds, zhIds)
    if (onlyEn.length === 0 && onlyZh.length === 0) continue
    violations.push({
      code: 'identifier-parity',
      path: norm(join(PROVIDER_ROOT, semantic)),
      detail:
        `protocol identifiers differ — only-en: [${onlyEn.join(', ')}]; ` +
        `only-zh-CN: [${onlyZh.join(', ')}]`,
    })
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
 * @param {{ tipIdentities?: Iterable<string>, toolNames?: Iterable<string> }} [catalogOverrides]
 * @returns {{ ok: boolean, violations: Violation[], semanticDirs: string[] }}
 */
export const scanRepo = (repoRoot = process.cwd(), catalogOverrides) => {
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

  const tipIdentities =
    catalogOverrides?.tipIdentities ?? listTipIdentities(resolve(repoRoot, ENFORCER_ROOT))
  const toolNames =
    catalogOverrides?.toolNames ??
    (() => {
      const abs = resolve(repoRoot, STATIC_TOOLS_REL)
      return existsSync(abs) ? listHyphenatedToolNames(readFileSync(abs, 'utf8')) : []
    })()

  if (semanticDirs.length > 0) {
    violations.push(...scanIdentifierParity(semanticDirs, providerAbs, { tipIdentities, toolNames }))
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
        'each has en.md + zh-CN.md; protocol identifiers match; ' +
        'ProviderResources.requireLanguagePair present',
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
