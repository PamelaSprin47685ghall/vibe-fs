#!/usr/bin/env node
// Requirements-tree governance checks (REQUIREMENT-SYSTEM-005/006/007/008/009/010).
//
// After the 2026-08-14 cutover the archive/docs + archive/changes trees are gone;
// normative definitions live only in requirements/<package>/WHAT.md. This gate
// enforces that world and the archive-detachment contract:
//   1. formal clause definitions only in package WHAT.md
//   2. clause references with a known package prefix must resolve
//   3. no `archive/` path may remain anywhere (deleted tree = dead reference)
//   4. retired workflow paths stay banned
//   5. local markdown links must resolve

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import {
  archivePathReferences,
  clauseDefinitionHeadings,
  clauseReferences,
  formalClauseDefinitionHeadings,
  legacyWorkflowPathReferences,
  markdownLocalLinks,
} from './spec-rules.mjs'

const REQUIREMENTS = 'requirements'
const PROPOSALS = 'proposals'
const ROOT_MD = ['AGENTS.md', 'README.md', 'CHANGELOG.md']
const TEXT_EXTENSIONS = new Set(['.md', '.mjs', '.js', '.fs', '.fsproj', '.json', '.yml', '.yaml', '.toml'])
const isTextFile = (file) => TEXT_EXTENSIONS.has(file.slice(file.lastIndexOf('.')))
const isTestFile = (file) => display(file).includes('/tests/')

const failures = []
const fail = (file, line, msg) => failures.push({ file, line, msg })
const display = (file) => file.replace(/\\/g, '/')
const rel = (file) => relative('.', file).replace(/\\/g, '/')

const walkFiles = (dir, acc = []) => {
  if (!existsSync(dir)) return acc
  for (const name of readdirSync(dir)) {
    const full = join(dir, name)
    const st = statSync(full)
    if (st.isDirectory()) walkFiles(full, acc)
    else acc.push(full)
  }
  return acc
}

const walkMarkdown = (dir) => walkFiles(dir).filter((file) => file.endsWith('.md'))

// ── 1. definitions: `PREFIX-NNN` headings in package WHAT.md ────────────────

const definitions = new Map() // id -> { file, line }
const prefixOwner = new Map() // PREFIX -> package
for (const file of walkMarkdown(REQUIREMENTS).filter((f) => f.endsWith('/WHAT.md'))) {
  const text = readFileSync(file, 'utf8')
  const pkg = display(file).split('/')[1]
  for (const { id, line } of clauseDefinitionHeadings(text)) {
    const previous = definitions.get(id)
    if (previous) {
      fail(rel(file), line, `条款 ID 重复定义：${id}（已在 ${previous.file}:${previous.line} 定义）`)
      continue
    }
    definitions.set(id, { file: rel(file), line })
    const prefix = id.split('-')[0]
    const owner = prefixOwner.get(prefix)
    if (owner && owner !== pkg) fail(rel(file), line, `前缀 ${prefix}- 被多包定义：${owner} 与 ${pkg}`)
    else prefixOwner.set(prefix, pkg)
  }
}
const PREFIXES = [...prefixOwner.keys()]

// ── 2. no definitions outside WHAT.md（含 proposals/）────────────────────────

for (const file of [...walkMarkdown(REQUIREMENTS), ...walkMarkdown(PROPOSALS)]) {
  if (file.endsWith('/WHAT.md') || isTestFile(file)) continue
  const text = readFileSync(file, 'utf8')
  for (const { id, line } of formalClauseDefinitionHeadings(text, PREFIXES))
    fail(rel(file), line, `正式条款只能定义在 package WHAT.md：${id}`)
}

// ── 3. references with a known prefix must resolve ──────────────────────────

for (const file of [...walkMarkdown(REQUIREMENTS), ...walkMarkdown(PROPOSALS), ...ROOT_MD]) {
  if (isTestFile(file)) continue
  const text = readFileSync(file, 'utf8')
  for (const { id, line } of clauseReferences(text, PREFIXES))
    if (!definitions.has(id)) fail(rel(file), line, `悬空条款引用：${id} 无定义`)
}

// ── 4. archive/ is a deleted tree: no reference may remain ──────────────────

const scanRoots = [REQUIREMENTS, PROPOSALS, 'src', 'resources', 'scripts', '.github', ...ROOT_MD]
const scanFiles = new Set(
  scanRoots.flatMap((root) => (existsSync(root) && statSync(root).isFile() ? [root] : walkFiles(root))),
)
const selfExclusions = new Set([
  join('scripts', 'checks', 'spec.mjs'),
  join('scripts', 'checks', 'spec-rules.mjs'),
  join('requirements', 'requirement-system', 'tests', 'spec-rules.test.mjs'),
])

for (const file of scanFiles) {
  if (!isTextFile(file) || selfExclusions.has(file)) continue
  const text = readFileSync(file, 'utf8')
  for (const { token, line } of archivePathReferences(text))
    fail(rel(file), line, `引用已删除的 archive/ 树：${token}`)
}

// ── 5. retired workflow paths stay banned ───────────────────────────────────

for (const file of scanFiles) {
  if (!isTextFile(file) || selfExclusions.has(file)) continue
  const text = readFileSync(file, 'utf8')
  for (const { token, line } of legacyWorkflowPathReferences(text))
    fail(rel(file), line, `引用废止工作流路径：${token}`)
}

// ── 6. local markdown links resolve ─────────────────────────────────────────

for (const file of [...walkMarkdown(REQUIREMENTS), ...walkMarkdown(PROPOSALS), ...ROOT_MD]) {
  const text = readFileSync(file, 'utf8')
  for (const { target, line } of markdownLocalLinks(text))
    if (!existsSync(resolve(dirname(file), target)))
      fail(rel(file), line, `本地 Markdown 链接不存在：${target}`)
}

if (failures.length === 0) {
  console.log(
    `spec-check: OK — ${definitions.size} 条款 / ${PREFIXES.length} 前缀 / ${prefixOwner.size} 包；` +
      `archive 与废止路径零引用`,
  )
  process.exit(0)
}

console.error(`spec-check: ${failures.length} 处问题`)
for (const { file, line, msg } of failures) console.error(`  ${file}:${line}  ${msg}`)
process.exit(1)
