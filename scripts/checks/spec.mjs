#!/usr/bin/env node
// Document-governance contract checks (GOV-003 / GOV-005 / GOV-006 / GOV-010).

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import {
  changeDependencyReferences,
  clauseDefinitionHeadings,
  clauseReferences,
  formalClauseDefinitionHeadings,
  legacyWorkflowPathReferences,
  markdownLocalLinks,
  navigationProblems,
  unknownClauseReferences,
} from './spec-rules.mjs'

const DOCS = 'docs'
const CHANGES = 'changes'
const FORMAL_DIRS = ['why', 'what', 'shape', 'how', 'proof']
const CHANGE_DIRS = ['proposed', 'active', 'completed']
const NAV_FILE = join(DOCS, 'README.md')
const CHANGE_NAV_FILE = join(CHANGES, 'README.md')
const AGENTS_FILE = 'AGENTS.md'

/** Active prefix → owning formal file, relative to docs/. */
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
  DSL: 'what/dsl-structured-program.md',
  GLORY: 'what/glory.md',
  SURFACE: 'what/glory.md',
}

/** Prefixes allowed to split definitions across listed files; individual IDs remain unique. */
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
  DSL: [
    'what/dsl-structured-program.md',
    'shape/dsl-structured-program.md',
    'how/dsl-structured-program.md',
    'proof/dsl-structured-program.md',
    'why/dsl-structured-program.md',
  ],
  GOV: ['what/document-governance.md'],
  VERIFY: ['proof/verify.md'],
  GLORY: ['what/glory.md', 'shape/glory.md', 'how/glory.md', 'proof/glory.md', 'why/glory.md'],
  SURFACE: ['what/glory.md'],
}

const PREFIXES = Object.keys(PREFIX_OWNER)
const PREFIX_ALTERNATION = PREFIXES.join('|')
const DEFINITION_RE = new RegExp(`^##\\s+((?:${PREFIX_ALTERNATION})-\\d{3})\\b`, 'gm')
const failures = []
const fail = (file, line, msg) => failures.push({ file, line, msg })

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
const formalFiles = FORMAL_DIRS.flatMap((dir) => walkMarkdown(join(DOCS, dir)))
const changeFilesByDir = Object.fromEntries(
  CHANGE_DIRS.map((dir) => [dir, walkMarkdown(join(CHANGES, dir))]),
)
const changeFiles = CHANGE_DIRS.flatMap((dir) => changeFilesByDir[dir])
const relDocs = (file) => relative(DOCS, file).replace(/\\/g, '/')
const display = (file) => file.replace(/\\/g, '/')

/** @type {Map<string, {file: string, line: number}>} */
const definitions = new Map()
/** @type {{id: string, file: string, line: number}[]} */
const references = []

const rejectUnknownClauseLikeReferences = (key, text) => {
  for (const { token, line } of unknownClauseReferences(text, PREFIXES))
    fail(key, line, `未知或伪条款引用：${token}`)
}

const collectReferences = (key, text) => {
  for (const { id, line } of clauseReferences(text, PREFIXES))
    references.push({ id, file: key, line })
}

for (const file of formalFiles) {
  const text = readFileSync(file, 'utf8')
  const key = relDocs(file)
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
    if (!allowed.includes(key))
      fail(key, line, `条款 ${id} 定义位置越权；允许：${allowed.map((p) => `docs/${p}`).join(', ')}`)
  }
}

for (const dir of CHANGE_DIRS) {
  const path = join(CHANGES, dir)
  if (!existsSync(path)) fail('../changes/README.md', 0, `缺少生命周期目录 ${path}/`)
}
if (!existsSync(CHANGE_NAV_FILE)) fail('../changes/README.md', 0, '缺少 changes/README.md')

for (const file of changeFiles) {
  const text = readFileSync(file, 'utf8')
  collectReferences(`../${display(file)}`, text)
  for (const { id, line } of formalClauseDefinitionHeadings(text, PREFIXES))
    fail(`../${display(file)}`, line, `Change 文件禁止定义正式 Clause 标题：${id}`)
}

const navigation = existsSync(NAV_FILE) ? readFileSync(NAV_FILE, 'utf8') : ''
const agents = existsSync(AGENTS_FILE) ? readFileSync(AGENTS_FILE, 'utf8') : ''
if (!navigation) fail('README.md', 0, '缺少 docs/README.md 导航')
else {
  rejectUnknownClauseLikeReferences('README.md', navigation)
  collectReferences('README.md', navigation)
}
if (agents) {
  rejectUnknownClauseLikeReferences('../AGENTS.md', agents)
  collectReferences('../AGENTS.md', agents)
}

for (const [key, text] of [['README.md', navigation], ['../AGENTS.md', agents]]) {
  for (const { id, line } of formalClauseDefinitionHeadings(text, PREFIXES))
    fail(key, line, `路由文件禁止定义正式 Clause 标题：${id}`)
}

for (const { id, file, line } of references) {
  if (!definitions.has(id)) fail(file, line, `悬空条款引用：${id} 无定义`)
}

for (const directory of FORMAL_DIRS) {
  const expected = formalFiles.map(relDocs).filter((file) => file.startsWith(`${directory}/`)).sort()
  const problems = navigationProblems(navigation, directory, expected)
  for (const file of problems.missing) fail('README.md', 0, `导航索引缺少正式文件 docs/${file}`)
  for (const { file, line } of problems.stale)
    fail('README.md', line, `导航引用不存在的正式文件 docs/${file}`)
}

for (const prefix of PREFIXES) {
  if (!navigation.includes(`${prefix}-`)) fail('README.md', 0, `导航索引缺少条款前缀 ${prefix}-`)
}

for (const legacy of [join(DOCS, 'proposal'), join(DOCS, 'status')]) {
  if (existsSync(legacy)) fail('README.md', 0, `废止目录不得存在：${legacy}/`)
}

const lifecycleOwners = new Map()
for (const dir of CHANGE_DIRS) {
  for (const file of changeFilesByDir[dir]) {
    const key = relative(join(CHANGES, dir), file).replace(/\\/g, '/')
    const previous = lifecycleOwners.get(key)
    if (previous) fail(`../${display(file)}`, 0, `同一工作项同时存在于 ${previous}/ 与 ${dir}/：${key}`)
    else lifecycleOwners.set(key, dir)
  }
}

for (const file of [AGENTS_FILE, NAV_FILE, CHANGE_NAV_FILE, ...formalFiles, ...changeFiles]) {
  if (!existsSync(file)) continue
  const text = readFileSync(file, 'utf8')
  for (const { target, line } of markdownLocalLinks(text)) {
    if (!existsSync(resolve(dirname(file), target)))
      fail(file === AGENTS_FILE ? '../AGENTS.md' : display(file), line, `本地 Markdown 链接不存在：${target}`)
  }
}

const TEXT_EXTENSIONS = new Set(['.md', '.mjs', '.js', '.fs', '.fsproj', '.json', '.yml', '.yaml', '.toml'])
const isTextFile = (file) => TEXT_EXTENSIONS.has(file.slice(file.lastIndexOf('.')))
const legacyScanFiles = [
  AGENTS_FILE,
  'README.md',
  'CHANGELOG.md',
  'package.json',
  ...formalFiles,
  ...['src', 'resources', 'tests', 'scripts', '.github'].flatMap((root) => walkFiles(root)).filter(isTextFile),
]
const scanExclusions = new Set([
  join('scripts', 'checks', 'spec.mjs'),
  join('scripts', 'checks', 'spec-rules.mjs'),
  join('tests', 'unit', 'verify', 'spec-rules.test.mjs'),
])
for (const file of new Set(legacyScanFiles)) {
  if (!existsSync(file) || scanExclusions.has(file)) continue
  const text = readFileSync(file, 'utf8')
  for (const { token, line } of legacyWorkflowPathReferences(text))
    fail(file.startsWith(`${DOCS}/`) ? relDocs(file) : `../${display(file)}`, line, `引用废止工作流路径：${token}`)
}

const dependencyFiles = [
  ...formalFiles.filter((file) => !file.endsWith('document-governance.md')),
  ...['src', 'resources', 'tests'].flatMap((root) => walkFiles(root)).filter(isTextFile),
]
for (const file of dependencyFiles) {
  if (file === join('tests', 'unit', 'verify', 'spec-rules.test.mjs')) continue
  const text = readFileSync(file, 'utf8')
  for (const { token, line } of changeDependencyReferences(text))
    fail(file.startsWith(`${DOCS}/`) ? relDocs(file) : `../${display(file)}`, line, `当前规范或实现禁止依赖 Change 历史：${token}`)
}

if (failures.length === 0) {
  const counts = Object.fromEntries(CHANGE_DIRS.map((dir) => [dir, changeFilesByDir[dir].length]))
  console.log(
    `spec-check: OK — ${definitions.size} 条款，${references.length} 处正式引用，${formalFiles.length} 个正式文件，` +
      `${counts.proposed} proposed / ${counts.active} active / ${counts.completed} completed changes`,
  )
  process.exit(0)
}

console.error(`spec-check: ${failures.length} 处问题`)
for (const { file, line, msg } of failures) console.error(`  docs/${file}:${line}  ${msg}`)
process.exit(1)
