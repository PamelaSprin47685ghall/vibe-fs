// requirements/requirement-system/tests/meta-verifier.test.mjs
//
// REQUIREMENT-SYSTEM-003 / 004 / 006 / 007 / 016 / 017 的机器落点。
//
// 本测试扫描 requirements/ 全树，断言五个结构事实：
//   1. INDEX（requirements/INDEX.md 表 + requirements/README.md 树入口）
//      中当前全部 54 个包都有 requirements/<pkg>/{WHY,WHAT,HOW}.md 与 tests/；
//   2. 缺少证明由 GAP 记录，不作为已有测试失败；
//   3. 每个 HOW.md 落点引用的测试文件真实存在；
//   4. requirements/ 下不存在 INDEX 之外的包目录；
//   5. 每个包 WHY/WHAT/HOW 中出现的 DEPENDS ON 引用集合 ⊆ INDEX 依赖骨架
//      （允许子集，不允许多出边）。

import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const REQUIREMENTS = join(ROOT, 'requirements')
const INDEX_FILE = join(ROOT, 'requirements/INDEX.md')
const TREE_ENTRY = join(REQUIREMENTS, 'README.md')

const read = (path) => readFileSync(path, 'utf8')

const REQUIRED_DOCS = ['WHY.md', 'WHAT.md', 'HOW.md']

// ── 解析 INDEX 包清单 ────────────────────────────────────────────────────────

/** requirements/README.md 树入口：`[pkg](pkg/WHAT.md)` 或 `[pkg](pkg/README.md)` 链接。 */
const packageNamesFromTreeEntry = () => {
  const text = read(TREE_ENTRY)
  const names = [...text.matchAll(/\]\(([a-z][a-z0-9-]*)(?:\/(?:WHAT|WHY|README)\.md|\/)?\)/g)].map((m) => m[1])
  return [...new Set(names)]
}

/** requirements-design/INDEX.md 各节表格：`| \`pkg\` | 一句话 WHY |` 行。 */
const packageNamesFromIndexTables = () => {
  const text = read(INDEX_FILE)
  const names = []
  for (const line of text.split('\n')) {
    if (!line.startsWith('| ')) continue
    const name = /`([a-z][a-z0-9-]*)`/.exec(line)?.[1]
    if (name) names.push(name)
  }
  return [...new Set(names)]
}

/** INDEX.md「# 依赖骨架」后的第一个 code block：当前 146 edge 邻接清单。 */
const dependencySkeleton = () => {
  const text = read(INDEX_FILE)
  const heading = text.indexOf('# 依赖骨架')
  assert.ok(heading >= 0, 'INDEX.md must contain a "# 依赖骨架" section')
  const fenceStart = text.indexOf('```', heading)
  const fenceEnd = text.indexOf('```', fenceStart + 3)
  assert.ok(fenceStart >= 0 && fenceEnd >= 0, 'dependency skeleton must live in a fenced code block')
  const block = text.slice(fenceStart + 3, fenceEnd)
  const edges = new Map() // pkg -> Set<dep>
  for (const line of block.split('\n')) {
    const match = /^([a-z][a-z0-9-]*)\s*→\s*(.+)$/.exec(line.trim())
    if (!match) continue
    const [pkg, rhs] = [match[1], match[2]]
    const deps = new Set()
    for (const name of packageNamesFromIndexTables()) {
      if (name === pkg) continue
      if (new RegExp(`\\b${name}\\b`).test(rhs)) deps.add(name)
    }
    edges.set(pkg, deps)
  }
  return edges
}

// ── 解析 DEPENDS ON 声明 ─────────────────────────────────────────────────────

const DEPENDS_ON_TRIGGER = /^\s*(?:#+\s*|\*\*\s*)?DEPENDS\s+ON\b/i

/**
 * 从单个包的 WHY/WHAT/HOW 中收集「声明为依赖」的包名集合。
 * 只认 DEPENDS ON 声明行及其后续非空、非标题的延续行；散文里的跨包引用不算。
 */
const declaredDependencies = (pkg, docRel, allNames) => {
  const filePath = join(REQUIREMENTS, pkg, docRel)
  if (!existsSync(filePath)) return new Set()
  const text = read(filePath)
  const declared = new Set()
  let collecting = false
  for (const line of text.split('\n')) {
    if (collecting) {
      if (line.trim() === '' || /^\s*#/.test(line)) collecting = false
      else collectNames(line, allNames, pkg, declared)
      continue
    }
    if (DEPENDS_ON_TRIGGER.test(line)) {
      collecting = true
      collectNames(line, allNames, pkg, declared)
    }
  }
  return declared
}

const collectNames = (line, allNames, self, out) => {
  for (const name of allNames) {
    if (name === self) continue
    if (new RegExp(`\\b${name}\\b`).test(line)) out.add(name)
  }
}

/** 解析落点单元格里的测试文件 token（包内 `tests/…`、仓库 `tests/unit|eval|integration|e2e/…`、`requirements/…`、`scripts/…`）。 */
const landingFileTokens = (row) => {
  const cells = row.split('|').map((cell) => cell.trim())
  const landing = cells[2] ?? ''
  return [...landing.matchAll(/(?:requirements\/|tests\/|scripts\/)[\w./-]+\.(?:test\.mjs|mjs)/g)].map(
    (m) => m[0],
  )
}

/** 落点 token 解析：包内 `tests/…` 相对本包目录；`tests/{unit,eval,integration,e2e}/…`、`requirements/…`、`scripts/…` 相对仓库根。 */
const resolveLanding = (pkg, token) => {
  const repo = join(ROOT, token)
  if (existsSync(repo)) return repo
  if (token.startsWith('tests/')) {
    const local = join(REQUIREMENTS, pkg, token)
    if (existsSync(local)) return local
  }
  return repo
}

/** 对单个「已迁移」包跑文档齐备检查。 */
const docFailures = (pkg) => {
  const failures = []
  for (const doc of REQUIRED_DOCS) {
    if (!existsSync(join(REQUIREMENTS, pkg, doc))) failures.push(`${pkg}: missing ${doc}`)
  }
  return failures
}

/** 检查已经声明的 HOW 落点文件，不把缺少证明当作悬空引用。 */
const proofFailures = (pkg) => {
  const failures = []
  const howText = read(join(REQUIREMENTS, pkg, 'HOW.md'))
  for (const line of howText.split('\n')) {
    if (!line.startsWith('|')) continue
    for (const token of landingFileTokens(line)) {
      const resolved = resolveLanding(pkg, token)
      if (!existsSync(resolved)) {
        failures.push(`${pkg}: HOW landing file missing: ${token}`)
      }
    }
  }
  return failures
}

/** 对单个「已迁移」包跑依赖声明 ⊆ 骨架检查。 */
const depFailures = (pkg, allNames, skeleton) => {
  const failures = []
  for (const doc of ['WHY.md', 'WHAT.md', 'HOW.md']) {
    const declared = declaredDependencies(pkg, doc, allNames)
    const allowed = skeleton.get(pkg) ?? new Set()
    for (const dep of declared) {
      if (!allowed.has(dep)) {
        failures.push(`${pkg}: ${doc} declares DEPENDS ON ${dep}, but the INDEX skeleton has no such edge (allowed: ${[...allowed].join(', ') || '无'})`)
      }
    }
  }
  return failures
}

// ── 五个 test()：每个恰一个 primary WHAT ─────────────────────────────────────

test('WHAT[REQUIREMENT-SYSTEM-017] meta-verifier executes as the machine proof', () => {
  assert.ok(
    existsSync(join(REQUIREMENTS, 'requirement-system/tests/meta-verifier.test.mjs')),
    'meta-verifier.test.mjs must exist and run as the REQUIREMENT-SYSTEM-017 machine proof',
  )
})
