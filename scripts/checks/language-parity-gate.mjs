#!/usr/bin/env node
/**
 * ARCH-016 Gate C — Language Parity (HOST-026 / PROMPT-017).
 * Every provider semantic resource must exist in EN and zh-CN with matching structure.
 *
 * Usage: node scripts/checks/language-parity-gate.mjs
 */

import { existsSync, readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const EN_ROOT = 'resources/provider/en'
export const ZH_ROOT = 'resources/provider/zh-CN'
export const PROVIDER_RESOURCES_REL = 'src/Wanxiangshu/Infrastructure/Resources/ProviderResources.fs'

const norm = (p) => p.replace(/\\/g, '/')

/**
 * @typedef {{ code: string, path: string, detail?: string }} Violation
 */

/**
 * @param {string} root
 * @returns {string[]}
 */
export const listRelativeFiles = (root) => {
  if (!existsSync(root)) return []
  return walk(root).map((abs) => norm(relative(root, abs))).sort()
}

/**
 * @param {string[]} enFiles
 * @param {string[]} zhFiles
 * @returns {Violation[]}
 */
export const scanParity = (enFiles, zhFiles) => {
  /** @type {Violation[]} */
  const violations = []
  const enSet = new Set(enFiles)
  const zhSet = new Set(zhFiles)

  for (const rel of enFiles) {
    if (!zhSet.has(rel)) {
      violations.push({
        code: 'missing-zh-cn',
        path: join(ZH_ROOT, rel),
        detail: `zh-CN counterpart missing for en/${rel}`,
      })
    }
  }

  for (const rel of zhFiles) {
    if (!enSet.has(rel)) {
      violations.push({
        code: 'missing-en',
        path: join(EN_ROOT, rel),
        detail: `EN counterpart missing for zh-CN/${rel}`,
      })
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
  return violations
}

/**
 * @param {string} [repoRoot]
 * @returns {{ ok: boolean, violations: Violation[] }}
 */
export const scanRepo = (repoRoot = process.cwd()) => {
  /** @type {Violation[]} */
  const violations = []

  const enAbs = resolve(repoRoot, EN_ROOT)
  const zhAbs = resolve(repoRoot, ZH_ROOT)

  if (!existsSync(enAbs)) {
    violations.push({ code: 'missing-en-root', path: EN_ROOT, detail: 'EN provider root missing' })
  }
  if (!existsSync(zhAbs)) {
    violations.push({ code: 'missing-zh-root', path: ZH_ROOT, detail: 'zh-CN provider root missing' })
  }

  if (violations.length === 0) {
    violations.push(...scanParity(listRelativeFiles(enAbs), listRelativeFiles(zhAbs)))
  }

  const hookAbs = resolve(repoRoot, PROVIDER_RESOURCES_REL)
  if (!existsSync(hookAbs)) {
    violations.push({ code: 'missing-file', path: PROVIDER_RESOURCES_REL, detail: 'ProviderResources.fs missing' })
  } else {
    violations.push(...scanProviderResourcesHook(readFileSync(hookAbs, 'utf8')))
  }

  return { ok: violations.length === 0, violations }
}

const formatViolation = (v) => {
  const detail = v.detail ? ` — ${v.detail}` : ''
  return `  ${v.path}: ${v.code}${detail}`
}

const runCli = () => {
  const result = scanRepo()
  if (result.ok) {
    console.log(
      'language-parity-gate: OK — resources/provider/en ↔ zh-CN structure parity; ' +
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
