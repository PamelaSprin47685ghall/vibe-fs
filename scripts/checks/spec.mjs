#!/usr/bin/env node
// Normative docs pure-text contract checks (GOV-005 / GOV-003).
//
// Checks:
//   1. Clause IDs defined once under docs/{why,what,shape,how,proof}
//   2. References in those trees must resolve
//   3. Prefix ownership (PREFIX_OWNER → relative path under docs/)
//   4. docs/README.md navigates every formal file that defines clauses
//   5. status/ and proposal/ must not define Clause IDs
//
// Usage: node scripts/checks/spec.mjs

import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { join, relative } from 'node:path'

const DOCS = 'docs'
const FORMAL_DIRS = ['why', 'what', 'shape', 'how', 'proof']
const FLUID_DIRS = ['status', 'proposal']
const NAV_FILE = join(DOCS, 'README.md')

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
const CLAUSE_RE = new RegExp(`\\b(${PREFIX_ALTERNATION})-(\\d{3})\\b`, 'g')
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

for (const file of formalFiles) {
  const text = readFileSync(file, 'utf8')
  const key = relDocs(file)
  sources.set(key, text)
  const lines = text.split('\n')

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

  lines.forEach((content, index) => {
    for (const match of content.matchAll(CLAUSE_RE)) {
      references.push({ id: match[0], file: key, line: index + 1 })
    }
  })
}

for (const file of fluidFiles) {
  const text = readFileSync(file, 'utf8')
  const key = relDocs(file)
  for (const match of text.matchAll(DEFINITION_RE)) {
    const id = match[1]
    const line = text.slice(0, match.index).split('\n').length
    fail(key, line, `流动面禁止定义条款：${id}`)
  }
}

for (const { id, file, line } of references) {
  if (!definitions.has(id)) {
    fail(file, line, `悬空条款引用：${id} 无定义`)
  }
}

const navigation = existsSync(NAV_FILE) ? readFileSync(NAV_FILE, 'utf8') : ''
if (!navigation) {
  fail('README.md', 0, '缺少 docs/README.md 导航')
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

const definedCount = definitions.size
if (failures.length === 0) {
  console.log(
    `spec-check: OK — ${definedCount} 条款，${references.length} 处引用，${formalFiles.length} 个正式文件`,
  )
  process.exit(0)
}

console.error(`spec-check: ${failures.length} 处问题`)
for (const { file, line, msg } of failures) {
  console.error(`  docs/${file}:${line}  ${msg}`)
}
process.exit(1)
