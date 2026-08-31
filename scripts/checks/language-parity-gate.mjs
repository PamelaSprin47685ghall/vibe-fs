#!/usr/bin/env node
/**
 * ARCH-016 Gate C — Language Parity (HOST-026 / PROMPT-017/019/020).
 * Gate F — Office Capability Integrity (ARCH-017).
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
import {
  OFFICE_CAPABILITY_ANCHORS,
  OFFICE_CAPABILITY_NEGATIVES,
  ROLE_SEMANTIC_ANCHORS,
  TOOL_DESCRIPTION_ANCHORS,
} from './semantic-anchors.mjs'

export const PROVIDER_ROOT = 'resources/provider'
export const ENFORCER_ROOT = 'resources/enforcer'
export const LOCALE_FILES = Object.freeze(['en.md', 'zh-CN.md'])
export const PROVIDER_RESOURCES_REL = 'src/Wanxiangshu/Participant/Provider/ProviderResources.fs'
export const PROVIDER_LANGUAGE_BINDING_REL =
  'src/Wanxiangshu/OpenCode/Host/ProviderLanguageBinding.fs'
export const LEGACY_PROVIDER_RESOURCES_REL = 'src/Wanxiangshu/Resources/ProviderResources.fs'
export const LEGACY_PROVIDER_PROSE_REL = 'src/Wanxiangshu/Resources/ProviderProse.fs'
export const STATIC_TOOLS_REL = 'src/Wanxiangshu/OpenCode/Tools/StaticTools.fs'

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

const PLACEHOLDER_RE = /\{\{([A-Za-z][A-Za-z0-9_]*)\}\}/g

/**
 * Named `{{placeholder}}` operands (PROMPT-019). Values are language-invariant;
 * the placeholder *set* must be identical across EN / zh-CN.
 * @param {string} text
 * @returns {Set<string>}
 */
export const extractPlaceholders = (text) => {
  /** @type {Set<string>} */
  const names = new Set()
  PLACEHOLDER_RE.lastIndex = 0
  let m
  while ((m = PLACEHOLDER_RE.exec(text)) !== null) names.add(m[1])
  return names
}

/**
 * EN/zh-CN `{{name}}` sets must be equal (PROMPT-019 structural parity).
 * @param {string[]} semanticDirs
 * @param {string} providerAbs
 * @returns {Violation[]}
 */
export const scanPlaceholderParity = (semanticDirs, providerAbs) => {
  /** @type {Violation[]} */
  const violations = []
  for (const semantic of semanticDirs) {
    const enAbs = join(providerAbs, semantic, 'en.md')
    const zhAbs = join(providerAbs, semantic, 'zh-CN.md')
    if (!existsSync(enAbs) || !existsSync(zhAbs)) continue
    const enPh = extractPlaceholders(readFileSync(enAbs, 'utf8'))
    const zhPh = extractPlaceholders(readFileSync(zhAbs, 'utf8'))
    const { onlyA: onlyEn, onlyB: onlyZh } = setDiff(enPh, zhPh)
    if (onlyEn.length === 0 && onlyZh.length === 0) continue
    violations.push({
      code: 'placeholder-parity',
      path: norm(join(PROVIDER_ROOT, semantic)),
      detail:
        `placeholders differ — only-en: [${onlyEn.join(', ')}]; ` +
        `only-zh-CN: [${onlyZh.join(', ')}]`,
    })
  }
  return violations
}

/**
 * Role Law EN/zh-CN must hit the same semantic-anchor ids (PROMPT-019).
 * @param {string} providerAbs
 * @param {typeof ROLE_SEMANTIC_ANCHORS} [catalog]
 * @returns {Violation[]}
 */
export const scanSemanticAnchorParity = (providerAbs, catalog = ROLE_SEMANTIC_ANCHORS) => {
  /** @type {Violation[]} */
  const violations = []
  for (const [role, anchors] of Object.entries(catalog)) {
    const enAbs = join(providerAbs, 'role', role, 'en.md')
    const zhAbs = join(providerAbs, 'role', role, 'zh-CN.md')
    if (!existsSync(enAbs) || !existsSync(zhAbs)) continue
    const enText = readFileSync(enAbs, 'utf8')
    const zhText = readFileSync(zhAbs, 'utf8')
    for (const { id, en, zh } of anchors) {
      if (!en.test(enText)) {
        violations.push({
          code: 'semantic-anchor',
          path: norm(join(PROVIDER_ROOT, 'role', role, 'en.md')),
          detail: `missing ${id}`,
        })
      }
      if (!zh.test(zhText)) {
        violations.push({
          code: 'semantic-anchor',
          path: norm(join(PROVIDER_ROOT, 'role', role, 'zh-CN.md')),
          detail: `missing ${id}`,
        })
      }
    }
  }
  return violations
}

/**
 * Every Role Law directory with locale leaves must appear in the catalog.
 * @param {string[]} semanticDirs
 * @param {typeof ROLE_SEMANTIC_ANCHORS} [catalog]
 * @returns {Violation[]}
 */
export const scanSemanticAnchorCatalog = (semanticDirs, catalog = ROLE_SEMANTIC_ANCHORS) => {
  /** @type {Violation[]} */
  const violations = []
  for (const semantic of semanticDirs) {
    const parts = semantic.split('/')
    if (parts.length !== 2 || parts[0] !== 'role') continue
    const role = parts[1]
    if (catalog[role]) continue
    violations.push({
      code: 'semantic-anchor-catalog',
      path: norm(join(PROVIDER_ROOT, semantic)),
      detail: 'Role Law missing semantic-anchor catalog',
    })
  }
  return violations
}

/**
 * inspect / fork tool descriptions must hit the same semantic-anchor ids (PROMPT-019).
 * @param {string} providerAbs
 * @param {typeof TOOL_DESCRIPTION_ANCHORS} [catalog]
 * @returns {Violation[]}
 */
export const scanToolDescriptionAnchorParity = (providerAbs, catalog = TOOL_DESCRIPTION_ANCHORS) => {
  /** @type {Violation[]} */
  const violations = []
  for (const [tool, anchors] of Object.entries(catalog)) {
    const enAbs = join(providerAbs, 'tool', tool, 'description', 'en.md')
    const zhAbs = join(providerAbs, 'tool', tool, 'description', 'zh-CN.md')
    if (!existsSync(enAbs) || !existsSync(zhAbs)) continue
    const enText = readFileSync(enAbs, 'utf8')
    const zhText = readFileSync(zhAbs, 'utf8')
    for (const { id, en, zh } of anchors) {
      if (!en.test(enText)) {
        violations.push({
          code: 'tool-description-anchor',
          path: norm(join(PROVIDER_ROOT, 'tool', tool, 'description', 'en.md')),
          detail: `missing ${id}`,
        })
      }
      if (!zh.test(zhText)) {
        violations.push({
          code: 'tool-description-anchor',
          path: norm(join(PROVIDER_ROOT, 'tool', tool, 'description', 'zh-CN.md')),
          detail: `missing ${id}`,
        })
      }
    }
  }
  return violations
}

/**
 * Catalogued tool descriptions must exist as locale-pair directories.
 * @param {string[]} semanticDirs
 * @param {typeof TOOL_DESCRIPTION_ANCHORS} [catalog]
 * @returns {Violation[]}
 */
export const scanToolDescriptionAnchorCatalog = (semanticDirs, catalog = TOOL_DESCRIPTION_ANCHORS) => {
  /** @type {Violation[]} */
  const violations = []
  for (const tool of Object.keys(catalog)) {
    const semantic = `tool/${tool}/description`
    if (semanticDirs.includes(semantic)) continue
    violations.push({
      code: 'tool-description-anchor-catalog',
      path: norm(join(PROVIDER_ROOT, semantic)),
      detail: 'tool description missing from resources',
    })
  }
  return violations
}

const OFFICE_SURFACES = Object.freeze([
  ['managerEn', 'role/manager/en.md'],
  ['managerZh', 'role/manager/zh-CN.md'],
  ['forkEn', 'tool/fork/description/en.md'],
  ['forkZh', 'tool/fork/description/zh-CN.md'],
])

/**
 * Gate F — five office consequences must hit Manager Role Law and fork description.
 * @param {string} providerAbs
 * @param {typeof OFFICE_CAPABILITY_ANCHORS} [catalog]
 * @param {typeof OFFICE_CAPABILITY_NEGATIVES} [negatives]
 * @returns {Violation[]}
 */
export const scanOfficeCapabilityIntegrity = (
  providerAbs,
  catalog = OFFICE_CAPABILITY_ANCHORS,
  negatives = OFFICE_CAPABILITY_NEGATIVES,
) => {
  /** @type {Violation[]} */
  const violations = []
  /** @type {Record<string, string | undefined>} */
  const texts = {}
  for (const [key, rel] of OFFICE_SURFACES) {
    const abs = join(providerAbs, rel)
    const path = norm(join(PROVIDER_ROOT, rel))
    if (!existsSync(abs)) {
      violations.push({
        code: 'office-capability',
        path,
        detail: 'missing locale leaf',
      })
      continue
    }
    texts[key] = readFileSync(abs, 'utf8')
  }
  for (const spec of Object.values(catalog)) {
    for (const [key, rel] of OFFICE_SURFACES) {
      const text = texts[key]
      if (text === undefined) continue
      if (!spec[key].test(text)) {
        violations.push({
          code: 'office-capability',
          path: norm(join(PROVIDER_ROOT, rel)),
          detail: `missing ${spec.id}`,
        })
      }
    }
  }
  if (texts.managerEn !== undefined && !negatives.managerEnRequired.test(texts.managerEn)) {
    violations.push({
      code: 'office-capability',
      path: norm(join(PROVIDER_ROOT, 'role/manager/en.md')),
      detail: 'missing not-interchangeable',
    })
  }
  if (texts.managerZh !== undefined && !negatives.managerZhRequired.test(texts.managerZh)) {
    violations.push({
      code: 'office-capability',
      path: norm(join(PROVIDER_ROOT, 'role/manager/zh-CN.md')),
      detail: 'missing not-interchangeable',
    })
  }
  for (const key of ['forkEn', 'forkZh']) {
    const text = texts[key]
    if (text === undefined) continue
    if (negatives.forkForbidden.test(text)) {
      const rel = key === 'forkEn' ? 'tool/fork/description/en.md' : 'tool/fork/description/zh-CN.md'
      violations.push({
        code: 'office-capability',
        path: norm(join(PROVIDER_ROOT, rel)),
        detail: 'fork must not match Commission another witness',
      })
    }
  }
  return violations
}

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
 * Host may observe the raw environment value, but provider language defaulting
 * and parsing belong to the Participant/Provider owner.
 * @param {string} text ProviderLanguageBinding.fs source
 * @returns {Violation[]}
 */
export const scanProviderLanguageBinding = (text) => {
  /** @type {Violation[]} */
  const violations = []
  const reject = (detail) =>
    violations.push({
      code: 'provider-language-policy',
      path: PROVIDER_LANGUAGE_BINDING_REL,
      detail,
    })

  if (!text.includes('Environment.GetEnvironmentVariable')) {
    reject('Host binding must observe the raw provider-language environment value')
  }
  if (!/ProviderLanguage\s*\.\s*fromPreferenceObservation/.test(text)) {
    reject(
      'Host binding must delegate provider-language defaulting and parsing to ProviderLanguage.fromPreferenceObservation',
    )
  }
  if (/ProviderLanguage\s*\.\s*English/.test(text)) {
    reject('ProviderLanguage.English fallback belongs to Participant/Provider owner, not Host')
  }
  if (/ProviderLanguage\s*\.\s*tryParse/.test(text)) {
    reject('ProviderLanguage.tryParse belongs to Participant/Provider owner, not Host')
  }
  if (/String\s*\.\s*IsNullOrWhiteSpace|\.IsNullOrWhiteSpace\s*\(/.test(text)) {
    reject('provider-language whitespace/default policy belongs to Participant/Provider owner, not Host')
  }
  if (/\|\s*None\s*->\s*ProviderLanguage\b|Option\s*\.\s*default(?:Value|With)/.test(text)) {
    reject('provider-language default branches belong to Participant/Provider owner, not Host')
  }
  if (/["'](?:en|en-US|zh|zh-CN|zh_CN|English|SimplifiedChinese)["']/i.test(text)) {
    reject('provider-language aliases belong to Participant/Provider owner, not Host')
  }
  return violations
}

/**
 * Domain language policy must not remain under the legacy Resources boundary.
 * @param {string} text legacy ProviderResources.fs source
 * @returns {Violation[]}
 */
export const scanLegacyProviderResourcesPolicy = (text) => {
  if (!/\bProviderLanguage\b|\brequireLanguagePair\b|\bresourceFileName\b/.test(text)) return []
  return [
    {
      code: 'provider-language-policy',
      path: LEGACY_PROVIDER_RESOURCES_REL,
      detail: 'provider-language policy belongs to Participant/Provider/ProviderResources.fs',
    },
  ]
}

/**
 * @param {Iterable<string>} paths repository-relative paths
 * @returns {Violation[]}
 */
export const scanForbiddenLegacyProviderPaths = (paths) => {
  const present = new Set([...paths].map(norm))
  return [LEGACY_PROVIDER_RESOURCES_REL, LEGACY_PROVIDER_PROSE_REL]
    .filter((path) => present.has(path))
    .map((path) => ({
      code: 'forbidden-legacy-path',
      path,
      detail:
        path === LEGACY_PROVIDER_RESOURCES_REL
          ? 'legacy ProviderResources path must be absent'
          : 'legacy ProviderProse path must be absent',
    }))
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
    violations.push(...scanPlaceholderParity(semanticDirs, providerAbs))
    violations.push(...scanSemanticAnchorParity(providerAbs))
    violations.push(...scanSemanticAnchorCatalog(semanticDirs))
    violations.push(...scanToolDescriptionAnchorParity(providerAbs))
    violations.push(...scanToolDescriptionAnchorCatalog(semanticDirs))
    violations.push(...scanOfficeCapabilityIntegrity(providerAbs))
  }

  const hookAbs = resolve(repoRoot, PROVIDER_RESOURCES_REL)
  if (!existsSync(hookAbs)) {
    violations.push({ code: 'missing-file', path: PROVIDER_RESOURCES_REL, detail: 'ProviderResources.fs missing' })
  } else {
    violations.push(...scanProviderResourcesHook(readFileSync(hookAbs, 'utf8')))
  }

  const bindingAbs = resolve(repoRoot, PROVIDER_LANGUAGE_BINDING_REL)
  if (!existsSync(bindingAbs)) {
    violations.push({
      code: 'missing-file',
      path: PROVIDER_LANGUAGE_BINDING_REL,
      detail: 'ProviderLanguageBinding.fs missing',
    })
  } else {
    violations.push(...scanProviderLanguageBinding(readFileSync(bindingAbs, 'utf8')))
  }

  const legacyPaths = [LEGACY_PROVIDER_RESOURCES_REL, LEGACY_PROVIDER_PROSE_REL].filter((path) =>
    existsSync(resolve(repoRoot, path)),
  )
  violations.push(...scanForbiddenLegacyProviderPaths(legacyPaths))
  const legacyResourcesAbs = resolve(repoRoot, LEGACY_PROVIDER_RESOURCES_REL)
  if (existsSync(legacyResourcesAbs)) {
    violations.push(...scanLegacyProviderResourcesPolicy(readFileSync(legacyResourcesAbs, 'utf8')))
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
        'placeholders match; Role Law semantic anchors match; ' +
        'tool description semantic anchors match; ' +
        'office capability projections match; ' +
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
