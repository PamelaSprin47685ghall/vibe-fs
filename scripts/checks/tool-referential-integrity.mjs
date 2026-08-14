#!/usr/bin/env node
/**
 * ARCH-016 Gate A — Tool Referential Integrity.
 * Same tool name → one schema owner + one semantic contract owner.
 *
 * Usage: node scripts/checks/tool-referential-integrity.mjs
 */

import { existsSync, readFileSync } from 'node:fs'
import { basename, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const TOOLS_ROOT = 'src/Wanxiangshu'
export const STATIC_TOOLS_REL = 'src/Wanxiangshu/OpenCode/Tools/StaticTools.fs'
export const TOOL_REGISTRY_REL = 'src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs'

/** Legacy provider tool names that must not reappear as ToolSpec.Name owners. */
export const LEGACY_FORBIDDEN_NAMES = Object.freeze([
  'verdict',
  'list',
  'blog',
  'fork-manager',
  'executor',
  'return',
  'edit-qa',
  'fork-pty',
  'tdd',
])

const norm = (p) => p.replace(/\\/g, '/')

const TOOL_SPEC_NAME_RE = /\{\s*Name\s*=\s*"([^"]+)"/g
const BEHAVIOR_SPEC_NAME_RE = /behaviorSpec[\s\S]{0,120}?"([a-z][a-z0-9-]+)"/g
const KNOWN_TOOL_NAMES_RE = /let\s+knownToolNames\s*=\s*\[([\s\S]*?)\]/
const ROLE_PREDICATE_ARM_RE = /\|\s*"([^"]+)"(?:\s+->|\s*\|)/g

/** Registry gate logic compares spec.Name — not a ToolSpec owner site. */
const TOOL_OWNER_SKIP = new Set(['ToolRegistry.fs'])

/**
 * @typedef {{ code: string, path: string, detail?: string }} Violation
 */

/**
 * @param {string} file
 * @param {string} text
 * @returns {{ name: string, line: number, owner: string }[]}
 */
export const extractToolSpecNames = (file, text) => {
  if (TOOL_OWNER_SKIP.has(basename(file))) return []

  const owner = basename(file, '.fs')
  /** @type {{ name: string, line: number, owner: string }[]} */
  const names = []
  const lines = text.split('\n')
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    TOOL_SPEC_NAME_RE.lastIndex = 0
    let hit
    while ((hit = TOOL_SPEC_NAME_RE.exec(line)) !== null) {
      names.push({ name: hit[1], line: i + 1, owner })
    }
  }

  BEHAVIOR_SPEC_NAME_RE.lastIndex = 0
  let behaviorHit
  while ((behaviorHit = BEHAVIOR_SPEC_NAME_RE.exec(text)) !== null) {
    const name = behaviorHit[1]
    const before = text.slice(0, behaviorHit.index)
    const line = before.split('\n').length
    names.push({ name, line, owner })
  }

  return names
}

/**
 * @param {string} text
 * @returns {string[]}
 */
export const extractKnownToolNames = (text) => {
  const block = text.match(KNOWN_TOOL_NAMES_RE)
  if (!block) return []
  return [...block[1].matchAll(/"([^"]+)"/g)].map((m) => m[1])
}

/**
 * @param {string} text
 * @returns {string[]}
 */
export const extractRolePredicateArms = (text) => {
  const arms = []
  const match = text.match(/let\s+rolePredicate[\s\S]*?(?=let\s+\w|\Z)/)
  if (!match) return arms
  for (const hit of match[0].matchAll(ROLE_PREDICATE_ARM_RE)) {
    const name = hit[1]
    if (!name.startsWith('js-')) arms.push(name)
  }
  return arms
}

/**
 * @param {{ file: string, text: string }[]} entries
 * @param {{ staticTools?: string, toolRegistry?: string }} [extra]
 * @returns {Violation[]}
 */
export const scanEntries = (entries, extra = {}) => {
  /** @type {Violation[]} */
  const violations = []
  /** @type {Map<string, { owner: string, path: string, line: number }[]>} */
  const byName = new Map()

  for (const { file, text } of entries) {
    for (const { name, line, owner } of extractToolSpecNames(file, text)) {
      if (LEGACY_FORBIDDEN_NAMES.includes(name)) {
        violations.push({
          code: 'legacy-tool-name',
          path: norm(file),
          detail: `line ${line}: legacy tool name '${name}' must not own a ToolSpec`,
        })
      }
      const list = byName.get(name) ?? []
      list.push({ owner, path: norm(file), line })
      byName.set(name, list)
    }
  }

  for (const [name, sites] of byName) {
    const owners = new Set(sites.map((s) => s.owner))
    if (owners.size > 1) {
      const detail = sites.map((s) => `${s.path}:${s.line} (${s.owner})`).join('; ')
      violations.push({
        code: 'duplicate-tool-owner',
        path: sites[0].path,
        detail: `tool '${name}' has conflicting owners: ${detail}`,
      })
    }
  }

  if (typeof extra.staticTools === 'string') {
    const known = extractKnownToolNames(extra.staticTools)
    const registered = [...byName.keys()].sort()
    const knownSet = new Set(known)
    const registeredSet = new Set(registered)

    for (const name of registered) {
      if (!knownSet.has(name)) {
        violations.push({
          code: 'unknown-tool-not-in-static',
          path: STATIC_TOOLS_REL,
          detail: `ToolSpec name '${name}' missing from StaticTools.knownToolNames`,
        })
      }
    }
  }

  if (typeof extra.toolRegistry === 'string') {
    const arms = extractRolePredicateArms(extra.toolRegistry)
    const registered = new Set(byName.keys())
    for (const arm of arms) {
      if (!registered.has(arm) && !arm.startsWith('open-terminal')) {
        const multi = ['open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal']
        if (multi.includes(arm) && registered.has(arm)) continue
        if (!registered.has(arm)) {
          violations.push({
            code: 'role-predicate-orphan',
            path: TOOL_REGISTRY_REL,
            detail: `rolePredicate arm '${arm}' has no ToolSpec owner`,
          })
        }
      }
    }
  }

  return violations
}

/**
 * @param {string} [repoRoot]
 * @returns {{ ok: boolean, violations: Violation[] }}
 */
export const isToolFile = (file) => {
  const rel = norm(file)
  return (
    rel.includes('/OpenCode/Tools/') ||
    (rel.includes('/OpenCode/') && /Tool(?:s)?\.fs$/.test(rel)) ||
    rel.endsWith('StaticTools.fs') ||
    rel.endsWith('ToolRegistry.fs')
  )
}

export const scanRepo = (repoRoot = process.cwd()) => {
  /** @type {Violation[]} */
  const violations = []
  const prodAbs = resolve(repoRoot, 'src/Wanxiangshu')
  if (!existsSync(prodAbs)) {
    return {
      ok: false,
      violations: [{ code: 'missing-tools-root', path: TOOLS_ROOT, detail: 'Tools directory missing' }],
    }
  }

  const files = walk(prodAbs, ['.fs']).filter(isToolFile)
  const entries = files.map((file) => ({
    file: norm(file.slice(repoRoot.length + 1)),
    text: readFileSync(file, 'utf8'),
  }))

  /** @type {{ staticTools?: string, toolRegistry?: string }} */
  const extra = {}
  const staticAbs = resolve(repoRoot, STATIC_TOOLS_REL)
  const registryAbs = resolve(repoRoot, TOOL_REGISTRY_REL)
  if (existsSync(staticAbs)) extra.staticTools = readFileSync(staticAbs, 'utf8')
  else violations.push({ code: 'missing-file', path: STATIC_TOOLS_REL, detail: 'StaticTools.fs missing' })
  if (existsSync(registryAbs)) extra.toolRegistry = readFileSync(registryAbs, 'utf8')
  else violations.push({ code: 'missing-file', path: TOOL_REGISTRY_REL, detail: 'ToolRegistry.fs missing' })

  violations.push(...scanEntries(entries, extra))
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
      'tool-referential-integrity-gate: OK — each ToolSpec name has a single owner; ' +
        'StaticTools.knownToolNames aligns with registry; no legacy tool names',
    )
    process.exit(0)
  }
  console.error(`tool-referential-integrity-gate: ${result.violations.length} violation(s)\n`)
  for (const v of result.violations) console.error(formatViolation(v))
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
