#!/usr/bin/env node
// Normative docs pure-text contract checks (GOV-005 / GOV-003).
//
// Checks:
//   1. Clause IDs defined once under docs/{why,what,shape,how,proof}
//   2. Formal/fluid/README references must resolve (including slash lists/range endpoints)
//   3. Prefix ownership (PREFIX_OWNER → relative path under docs/)
//   4. docs/README.md navigates every formal file that defines clauses
//   5. status/ and proposal/ must not define Clause IDs
//   6. clause-looking references use a known exact PREFIX-NNN (no pseudo suffixes)
//   7. docs/README.md covers the exact active status and proposal file sets
//
// Usage: node scripts/checks/spec.mjs

import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { join, relative } from 'node:path'
import {
  clauseReferences,
  fluidNavigationProblems,
  statusNavigationProblems,
  unknownClauseReferences,
} from './spec-rules.mjs'

const DOCS = 'docs'
const FORMAL_DIRS = ['why', 'what', 'shape', 'how', 'proof']
const FLUID_DIRS = ['status', 'proposal']
const NAV_FILE = join(DOCS, 'README.md')
const STATUS_DIR = join(DOCS, 'status')
const PROPOSAL_DIR = join(DOCS, 'proposal')

/**
 * Active prefix → owning formal file (path relative to docs/).
 * Hard-coded by design: part of the contract.
 * A prefix may only be defined in its owner file.
 */
const PREFIX_OWNER = {
  GOV: 'what/document-governance.md',
  ARCH: 'shape/architecture.md',
  AGENT: 'what/agent.md',
  PROMPT: 'what/prompt.md',
  FALLBACK: 'what/fallback.md',
  REVIEW: 'what/review.md',
  ORCH: 'what/orchestrator.md',
  HOST: 'what/host.md',
  COMPANION: 'what/companion.md',
  EXEC: 'what/execution.md',
  VERIFY: 'proof/verify.md',
  PERSIST: 'what/persist.md',
  CTX: 'what/context.md',
  FLOW: 'what/flow.md',
  ENFORCER: 'what/enforcer.md',
  PROJ: 'what/projection.md',
  LOOP: 'what/loop.md',
}

/** Prefixes allowed to split definitions across listed files (still unique IDs). */
const PREFIX_SPLIT_OWNERS = {
  ARCH: ['what/architecture.md', 'shape/architecture.md'],
  AGENT: ['what/agent.md', 'shape/agent.md'],
  PROMPT: ['what/prompt.md', 'shape/prompt.md', 'how/prompt.md'],
  FALLBACK: ['what/fallback.md', 'shape/fallback.md', 'how/fallback.md'],
  REVIEW: ['what/review.md', 'shape/review.md', 'how/review.md'],
  ORCH: ['what/orchestrator.md', 'shape/orchestrator.md', 'how/orchestrator.md'],
  HOST: ['what/host.md', 'shape/host.md', 'how/host.md'],
  COMPANION: ['what/companion.md', 'shape/companion.md', 'how/companion.md'],
  EXEC: ['what/execution.md', 'shape/execution.md', 'how/execution.md'],
  PERSIST: ['what/persist.md', 'shape/persist.md', 'how/persist.md'],
  CTX: ['what/context.md', 'shape/context.md', 'how/context.md'],
  FLOW: ['what/flow.md', 'shape/flow.md', 'how/flow.md', 'proof/flow.md', 'why/flow.md'],
  ENFORCER: [
    'what/enforcer.md',
    'shape/enforcer.md',
    'how/enforcer.md',
    'proof/enforcer.md',
    'why/enforcer.md',
  ],
  PROJ: ['what/projection.md', 'shape/projection.md', 'how/projection.md'],
  LOOP: ['what/loop.md', 'shape/loop.md', 'how/loop.md', 'proof/loop.md'],
  GOV: ['what/document-governance.md'],
  VERIFY: ['proof/verify.md'],
}

const PREFIX_ALTERNATION = Object.keys(PREFIX_OWNER).join('|')
const DEFINITION_RE = new RegExp(`^##\\s+((?:${PREFIX_ALTERNATION})-\\d{3})\\b`, 'gm')

const failures = []
const fail = (file, line, msg) => failures.push({ file, line, msg })

const walkMarkdown = (dir, acc = []) => {
  if (!existsSync(dir)) return acc
  for (const name of readdirSync(dir)) {
    const full = join(dir, name)
    const st = statSync(full)
    if (st.isDirectory()) walkMarkdown(full, acc)
    else if (name.endsWith('.md')) acc.push(full)
  }
  return acc
}

const formalFiles = FORMAL_DIRS.flatMap((d) => walkMarkdown(join(DOCS, d)))
const fluidFiles = FLUID_DIRS.flatMap((d) => walkMarkdown(join(DOCS, d)))

/** @type {Map<string, {file: string, line: number}>} */
const definitions = new Map()
/** @type {{id: string, file: string, line: number}[]} */
const references = []
const sources = new Map()

const relDocs = (abs) => relative(DOCS, abs).replace(/\\/g, '/')

const rejectUnknownClauseLikeReferences = (key, text) => {
  for (const { token, line } of unknownClauseReferences(text, Object.keys(PREFIX_OWNER))) {
    fail(key, line, `未知或伪条款引用：${token}`)
  }
}

const collectReferences = (key, text) => {
  for (const { id, line } of clauseReferences(text, Object.keys(PREFIX_OWNER))) {
    references.push({ id, file: key, line })
  }
}

for (const file of formalFiles) {
  const text = readFileSync(file, 'utf8')
  const key = relDocs(file)
  sources.set(key, text)
  rejectUnknownClauseLikeReferences(key, text)
  collectReferences(key, text)

  for (const match of text.matchAll(DEFINITION_RE)) {
    const id = match[1]
    const line = text.slice(0, match.index).split('\n').length
    const previous = definitions.get(id)
    if (previous) {
      fail(key, line, `条款 ID 重复定义：${id}（已在 ${previous.file}:${previous.line} 定义）`)
      continue
    }
    definitions.set(id, { file: key, line })

    const prefix = id.split('-')[0]
    const allowed = PREFIX_SPLIT_OWNERS[prefix] ?? [PREFIX_OWNER[prefix]]
    if (allowed && !allowed.includes(key)) {
      fail(
        key,
        line,
        `条款 ${id} 定义在 docs/${key}，但 PREFIX 归属允许：${allowed.map((p) => `docs/${p}`).join(', ')}`,
      )
    }
  }
}

for (const file of fluidFiles) {
  const text = readFileSync(file, 'utf8')
  const key = relDocs(file)
  collectReferences(key, text)
  for (const match of text.matchAll(DEFINITION_RE)) {
    const id = match[1]
    const line = text.slice(0, match.index).split('\n').length
    fail(key, line, `流动面禁止定义条款：${id}`)
  }
}

const navigation = existsSync(NAV_FILE) ? readFileSync(NAV_FILE, 'utf8') : ''
if (!navigation) {
  fail('README.md', 0, '缺少 docs/README.md 导航')
} else {
  rejectUnknownClauseLikeReferences('README.md', navigation)
  collectReferences('README.md', navigation)
}

for (const { id, file, line } of references) {
  if (!definitions.has(id)) {
    fail(file, line, `悬空条款引用：${id} 无定义`)
  }
}

const formalWithDefinitions = new Set([...definitions.values()].map((d) => d.file))
for (const file of formalWithDefinitions) {
  const needle = file.replace(/\\/g, '/')
  if (!navigation.includes(needle) && !navigation.includes(`](${needle})`)) {
    // also accept link text without path if basename listed in table cells
    const base = needle.split('/').pop()
    if (!navigation.includes(base.replace(/\.md$/, ''))) {
      fail('README.md', 0, `导航索引缺少正式文件 docs/${needle}`)
    }
  }
}

for (const prefix of Object.keys(PREFIX_OWNER)) {
  if (!navigation.includes(`\`${prefix}-\``) && !navigation.includes(`\`${prefix}\``)) {
    // README uses `ARCH-` style in table
    if (!navigation.includes(`${prefix}-`)) {
      fail('README.md', 0, `导航索引缺少条款前缀 ${prefix}-`)
    }
  }
}

const statusFiles = existsSync(STATUS_DIR)
  ? walkMarkdown(STATUS_DIR).map(relDocs).sort()
  : []
const statusProblems = statusNavigationProblems(navigation, statusFiles)
for (const file of statusProblems.missing) {
  fail('README.md', 0, `活跃 status 未进入导航：docs/${file}`)
}
for (const { file, line } of statusProblems.stale) {
  fail('README.md', line, `导航引用不存在的 status：docs/${file}`)
}

const proposalFiles = existsSync(PROPOSAL_DIR)
  ? walkMarkdown(PROPOSAL_DIR).map(relDocs).sort()
  : []
const proposalProblems = fluidNavigationProblems(navigation, 'proposal', proposalFiles)
for (const file of proposalProblems.missing) {
  fail('README.md', 0, `未裁决 proposal 未进入导航：docs/${file}`)
}
for (const { file, line } of proposalProblems.stale) {
  fail('README.md', line, `导航引用不存在的 proposal：docs/${file}`)
}

const definedCount = definitions.size
if (failures.length === 0) {
  console.log(
    `spec-check: OK — ${definedCount} 条款，${references.length} 处引用，${formalFiles.length} 个正式文件，${statusFiles.length} 个活跃 status，${proposalFiles.length} 个未裁决 proposal`,
  )
  process.exit(0)
}

console.error(`spec-check: ${failures.length} 处问题`)
for (const { file, line, msg } of failures) {
  console.error(`  docs/${file}:${line}  ${msg}`)
}
process.exit(1)
