#!/usr/bin/env node
// Role-matrix consistency gate (AGENT-001…004 / C5).
//
// Static source check: Canonical Role DU, ManagedAgentCatalog lists, and residual
// duplicate legacy sets must stay in step. No build artifact required.
//
//   node scripts/role-matrix-gate.mjs

import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = process.cwd()
const violations = []
const fail = (message) => violations.push(message)

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const ROLES_FS = 'src/Wanxiangshu/Kernel/Roles.fs'
const CATALOG_FS = 'src/Wanxiangshu/Domain/ManagedAgentCatalog.fs'
const PROMPT_AUTH_FS = 'src/Wanxiangshu/Domain/PromptAuthority.fs'
const MANAGED_AGENT_FS = 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ManagedAgent.fs'
const MANAGED_CONFIG_FS = 'src/Wanxiangshu/Infrastructure/OpenCode/Host/ManagedAgentConfig.fs'

// AGENT-002 fixed matrix (spec/02). Order is presentation only; membership is the check.
const EXPECTED_ROLES = [
  'Manager',
  'Orchestrator',
  'Coder',
  'Inspector',
  'Browser',
  'Meditator',
  'Reviewer',
  'DevOps',
  'Executor',
  'Blogger',
]

const EXPECTED_PUBLIC = [
  'Orchestrator',
  'Manager',
  'Coder',
  'Inspector',
  'DevOps',
  'Browser',
  'Meditator',
  'Reviewer',
]

const EXPECTED_INTERNAL = ['Blogger', 'Executor']

const EXPECTED_FORKABLE = ['Coder', 'Inspector', 'DevOps', 'Browser', 'Meditator', 'Reviewer']

const EXPECTED_LEGACY = new Set([
  'orchestrator',
  'manager',
  'build',
  'plan',
  'coder',
  'inspector',
  'devops',
  'browser',
  'meditator',
  'reviewer',
  'blogger',
  'executor',
  'fast',
  'deep',
])

// Version-agnostic legacy rejection prose, emitted by the catalog formatters.
// Held as a plain constant: the VERIFY-004 path-criterion harness reads only
// includes/startsWith/endsWith call shapes with quoted literals, so a constant
// never looks like a repo-relative path (C5 harness failure).
const VERSION_AGNOSTIC_REJECTION_PROSE = 'Managed agents require explicit fast-/deep- names.'

const roleCases = (source) => {
  const block = source.match(/type Role\s*=([\s\S]*?)(?:\ntype |\nmodule |\n\[|<RequireQualifiedAccess>|\n\/\/\/)/)
  if (!block) return null
  return [...block[1].matchAll(/\| (\w+)/g)].map((m) => m[1])
}

const listAfter = (source, binding) => {
  const re = new RegExp(`let ${binding}[^=]*=\\s*\\[([\\s\\S]*?)\\]`)
  const m = source.match(re)
  if (!m) return null
  return [...m[1].matchAll(/Role\.(\w+)/g)].map((x) => x[1])
}

const stringSetAfter = (source, binding) => {
  const re = new RegExp(`let ${binding}[^=]*=\\s*set\\s*\\[([\\s\\S]*?)\\]`)
  const m = source.match(re)
  if (!m) return null
  return new Set([...m[1].matchAll(/"([^"]+)"/g)].map((x) => x[1]))
}

const roleLabels = (source) => {
  const m = source.match(/let roleLabel[\s\S]*?=\s*match role with([\s\S]*?)let tryParseRole/)
  if (!m) return null
  const labels = new Map()
  for (const hit of m[1].matchAll(/\| Role\.(\w+)\s*->\s*"([^"]+)"/g)) {
    labels.set(hit[1], hit[2])
  }
  return labels
}

const rolesSource = read(ROLES_FS)
const catalogSource = read(CATALOG_FS)
const promptAuthSource = read(PROMPT_AUTH_FS)
const managedAgentSource = read(MANAGED_AGENT_FS)
const managedConfigSource = read(MANAGED_CONFIG_FS)

const duRoles = roleCases(rolesSource)
if (!duRoles) {
  fail(`${ROLES_FS}: could not parse Role DU cases`)
} else {
  const missing = EXPECTED_ROLES.filter((r) => !duRoles.includes(r))
  const extra = duRoles.filter((r) => !EXPECTED_ROLES.includes(r))
  if (missing.length) fail(`Role DU missing cases: ${missing.join(', ')}`)
  if (extra.length) fail(`Role DU unexpected cases: ${extra.join(', ')}`)
  if (duRoles.length !== 10) fail(`Role DU must have exactly 10 cases, got ${duRoles.length}`)
}

const publicRoles = listAfter(catalogSource, 'allPublicRoles')
const internalRoles = listAfter(catalogSource, 'allInternalRoles')
const forkableRoles = listAfter(catalogSource, 'publicForkableRoles')
const labels = roleLabels(catalogSource)
const legacy = stringSetAfter(catalogSource, 'legacyAgentNames')

if (!publicRoles) fail(`${CATALOG_FS}: missing allPublicRoles`)
else {
  for (const r of EXPECTED_PUBLIC) {
    if (!publicRoles.includes(r)) fail(`allPublicRoles missing ${r}`)
  }
  if (publicRoles.length !== EXPECTED_PUBLIC.length) {
    fail(`allPublicRoles length ${publicRoles.length} ≠ ${EXPECTED_PUBLIC.length}`)
  }
}

if (!internalRoles) fail(`${CATALOG_FS}: missing allInternalRoles`)
else {
  for (const r of EXPECTED_INTERNAL) {
    if (!internalRoles.includes(r)) fail(`allInternalRoles missing ${r}`)
  }
  if (internalRoles.length !== EXPECTED_INTERNAL.length) {
    fail(`allInternalRoles length ${internalRoles.length} ≠ ${EXPECTED_INTERNAL.length}`)
  }
}

if (!forkableRoles) fail(`${CATALOG_FS}: missing publicForkableRoles`)
else {
  for (const r of EXPECTED_FORKABLE) {
    if (!forkableRoles.includes(r)) fail(`publicForkableRoles missing ${r}`)
  }
}

if (!labels) fail(`${CATALOG_FS}: could not parse roleLabel`)
else {
  for (const role of EXPECTED_ROLES) {
    const label = labels.get(role)
    if (!label) fail(`roleLabel missing Role.${role}`)
    else if (label !== role.toLowerCase()) {
      fail(`roleLabel Role.${role} → '${label}', expected '${role.toLowerCase()}'`)
    }
  }
  if (labels.size !== 10) fail(`roleLabel must cover 10 roles, got ${labels.size}`)
}

if (!legacy) fail(`${CATALOG_FS}: missing legacyAgentNames`)
else {
  for (const name of EXPECTED_LEGACY) {
    if (!legacy.has(name)) fail(`legacyAgentNames missing '${name}'`)
  }
  for (const name of legacy) {
    if (!EXPECTED_LEGACY.has(name)) fail(`legacyAgentNames unexpected '${name}'`)
  }
}

// 10 roles × 2 tiers → 20 required names, derived from catalog formulas.
if (labels && publicRoles && internalRoles) {
  const all = [...publicRoles, ...internalRoles]
  const names = all.flatMap((role) => {
    const label = labels.get(role)
    return label ? [`fast-${label}`, `deep-${label}`] : []
  })
  if (names.length !== 20) fail(`derived requiredNames count ${names.length} ≠ 20`)
  const unique = new Set(names)
  if (unique.size !== 20) fail(`derived requiredNames not unique: ${names.join(', ')}`)

  for (const name of names) {
    const peer = name.startsWith('fast-')
      ? `deep-${name.slice('fast-'.length)}`
      : `fast-${name.slice('deep-'.length)}`
    if (!unique.has(peer)) fail(`peer of '${name}' missing from catalog matrix`)
  }
}

// No second inline legacy set in former owners.
for (const [path, source] of [
  [PROMPT_AUTH_FS, promptAuthSource],
  [MANAGED_AGENT_FS, managedAgentSource],
  [MANAGED_CONFIG_FS, managedConfigSource],
]) {
  if (/\blet private legacyAgentNames\b/.test(source) || /\blet private legacyHostNames\b/.test(source)) {
    fail(`${path}: residual private legacy name set — use ManagedAgentCatalog`)
  }
  if (/Wanxiangshu 0\.5\.0 only accepts/.test(source) || /not supported in Wanxiangshu 0\.5\.0/.test(source)) {
    fail(`${path}: versioned legacy rejection prose must be version-agnostic`)
  }
}

if (!catalogSource.includes('formatLegacyNameNotSupported')) {
  fail(`${CATALOG_FS}: missing formatLegacyNameNotSupported`)
}
if (!catalogSource.includes(VERSION_AGNOSTIC_REJECTION_PROSE)) {
  fail(`${CATALOG_FS}: missing version-agnostic legacy rejection prose`)
}
if (!managedAgentSource.includes('ManagedAgentCatalog.formatLegacyNameNotSupported')) {
  fail(`${MANAGED_AGENT_FS}: must emit catalog rejection prose via formatLegacyNameNotSupported`)
}
if (!managedConfigSource.includes('ManagedAgentCatalog.formatLegacyNameInConfig')) {
  fail(`${MANAGED_CONFIG_FS}: must emit catalog rejection prose via formatLegacyNameInConfig`)
}

// ManagedAgent must re-export catalog lists, not redefine Role arrays.
if (/let allPublicRoles\s*=\s*\[/.test(managedAgentSource)) {
  fail(`${MANAGED_AGENT_FS}: allPublicRoles must re-export ManagedAgentCatalog, not redefine`)
}
if (/let requiredNames[^=]*=\s*allRoles/.test(managedAgentSource)) {
  fail(`${MANAGED_AGENT_FS}: requiredNames must re-export ManagedAgentCatalog`)
}

if (violations.length === 0) {
  console.log('role-matrix-gate: ok (10 roles × 2 tiers, single catalog, no residual legacy sets)')
  process.exit(0)
}

console.error('role-matrix-gate: FAILED\n')
for (const v of violations) console.error(`  - ${v}`)
process.exit(1)
