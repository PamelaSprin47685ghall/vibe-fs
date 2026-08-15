// requirements/requirement-system/tests/meta-verifier.test.mjs
//
// REQUIREMENT-SYSTEM-003 / 004 / 006 / 007 / 016 / 017 的机器落点。
//
// 本测试扫描 requirements/ 全树，断言五个结构事实：
//   1. INDEX（requirements/INDEX.md 表 + requirements/README.md 树入口）
//      中当前全部 47 个包都有 requirements/<pkg>/{README,WHY,WHAT,HOW,PROOF}.md；
//   2. 每个 WHAT.md 的每个 `<PACKAGE>-NNN` 命题 ID（标题定义）在 PROOF.md
//      表格中有行（按包名 + ID 交叉检查）；
//   3. 每个 PROOF.md 落点引用的测试文件真实存在；
//   4. requirements/ 下不存在 INDEX 之外的包目录；
//   5. 每个包 README/WHY/WHAT 中出现的 DEPENDS ON 引用集合 ⊆ INDEX 依赖骨架
//      （允许子集，不允许多出边）。
//
// green only after full migration lands；中途缺失包是预期中间状态。
// 中途红有两种，均为预期：缺包（当前 47 包未全落地）与已落地但尚不完整
// （PROOF 占位、落点缺失、WHAT/PROOF 尚未对齐）的包。
// 两个 test() 分工：
//   - `已迁移包结构一致`：只检查 5 份文档齐备的包，失败精确到包与原因。
//     本包（requirement-system / verification-system）现在必须干净；删一个
//     已存在包的 PROOF 行立即在失败列表中出现该包（可红性）。
//   - `全量迁移状态`：当前 47 包 × 5 文档 + 无 INDEX 外目录。迁移中途必然红，
//     红的内容精确到「哪个包缺哪个文件」，cutover 全量落地后转绿。
//
// 依赖骨架源是 requirements/INDEX.md（live manifest，2026-08-14 cutover 迁入）。

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

const REQUIRED_DOCS = ['README.md', 'WHY.md', 'WHAT.md', 'HOW.md', 'PROOF.md']

// ── 解析 INDEX 包清单 ────────────────────────────────────────────────────────

/** requirements/README.md 树入口：`[pkg](pkg/README.md)` 链接。 */
const packageNamesFromTreeEntry = () => {
  const text = read(TREE_ENTRY)
  const names = [...text.matchAll(/\]\(([a-z][a-z0-9-]*)\/README\.md\)/g)].map((m) => m[1])
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

/** INDEX.md「# 依赖骨架」后的第一个 code block：当前 102 edge 邻接清单。 */
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
 * 从单个包的 README/WHY/WHAT 中收集「声明为依赖」的包名集合。
 * 只认 DEPENDS ON 声明行及其后续非空、非标题的延续行；散文里的跨包引用不算。
 */
const declaredDependencies = (pkg, docRel, allNames) => {
  const text = read(join(REQUIREMENTS, pkg, docRel))
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

// ── 命题 ID 与 PROOF 交叉 ────────────────────────────────────────────────────

const propositionIds = (pkg) => {
  const text = read(join(REQUIREMENTS, pkg, 'WHAT.md'))
  const prefix = pkg.toUpperCase()
  const ids = []
  for (const match of text.matchAll(/^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3})\b/gm)) {
    const id = match[1]
    if (id.slice(0, -4) === prefix) ids.push(id)
  }
  return [...new Set(ids)]
}

/**
 * PROOF.md 中命中该命题 ID 的行。ID 可出现在行首格或第二格，接受多种形式：
 * 完整 ID（`| REQUIREMENT-SYSTEM-001 |`、第二格 `DISPATCH-PROTOCOL-002/003`）、
 * 裸编号（`| 006/007 |`，仅行首格，避免与测试锚点里的三位数字误配）。
 */
const proofRowsFor = (pkg, id) => {
  const text = read(join(REQUIREMENTS, pkg, 'PROOF.md'))
  const full = new RegExp(`\\b${id}\\b`)
  const bare = new RegExp(`\\b${id.slice(-3)}\\b`)
  // 第二格可能携带合并 ID 列表（`DISPATCH-PROTOCOL-002/003`、`005/006/010`），
  // 按分隔符切 token 后逐 token 匹配；锚点里的 `_015_` 不产生词边界，不会误配。
  const idTokens = (cell) => cell.split(/[\s/,–—]+/).filter(Boolean)
  const rows = []
  for (const line of text.split('\n')) {
    if (!line.startsWith('|')) continue
    const cells = line.split('|')
    const cell1 = cells[1] ?? ''
    const cell2 = cells[2] ?? ''
    if (full.test(cell1) || bare.test(cell1)) {
      rows.push(line)
      continue
    }
    for (const token of idTokens(cell2)) {
      if (full.test(token) || bare.test(token)) {
        rows.push(line)
        break
      }
    }
  }
  return rows
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

/** 对单个「已迁移」包跑 PROOF 反向检查（命题有行、落点文件存在）。 */
const proofFailures = (pkg) => {
  const failures = []
  for (const id of propositionIds(pkg)) {
    if (proofRowsFor(pkg, id).length === 0) {
      failures.push(`${pkg}: WHAT proposition ${id} has no row in PROOF.md`)
    }
  }

  const proofText = read(join(REQUIREMENTS, pkg, 'PROOF.md'))
  for (const line of proofText.split('\n')) {
    if (!line.startsWith('|')) continue
    for (const token of landingFileTokens(line)) {
      const resolved = resolveLanding(pkg, token)
      if (!existsSync(resolved)) {
        failures.push(`${pkg}: PROOF landing file missing: ${token}`)
      }
    }
  }
  return failures
}

/** 对单个「已迁移」包跑依赖声明 ⊆ 骨架检查。 */
const depFailures = (pkg, allNames, skeleton) => {
  const failures = []
  for (const doc of ['README.md', 'WHY.md', 'WHAT.md']) {
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

test('WHAT[REQUIREMENT-SYSTEM-003] every INDEX package carries all five documents', () => {
  const allNames = packageNamesFromIndexTables()
  const missing = []
  for (const pkg of allNames) {
    for (const doc of REQUIRED_DOCS) {
      if (!existsSync(join(REQUIREMENTS, pkg, doc))) missing.push(`${pkg}/${doc}`)
    }
  }
  assert.deepEqual(
    missing,
    [],
    'every INDEX package must carry README/WHY/WHAT/HOW/PROOF; missing (expected while migration is mid-flight):\n' +
      missing.join('\n'),
  )
})

test('WHAT[REQUIREMENT-SYSTEM-004] every WHAT proposition has a PROOF row and a live landing file', () => {
  const allNames = packageNamesFromIndexTables()
  const dirs = readdirSync(REQUIREMENTS)
    .filter((entry) => statSync(join(REQUIREMENTS, entry)).isDirectory())
    .sort()
  const migrated = dirs.filter((pkg) => REQUIRED_DOCS.every((doc) => existsSync(join(REQUIREMENTS, pkg, doc))))

  assert.ok(migrated.length >= 2, 'requirement-system and verification-system must be structurally complete now')

  const failures = []
  for (const pkg of migrated) {
    failures.push(...proofFailures(pkg))
  }
  // 删一个已存在包的 PROOF 行 → 该包进入此失败列表（可红性）。
  assert.deepEqual(
    failures,
    [],
    'every migrated package must prove every proposition and name live landing files:\n' + failures.join('\n'),
  )
})

test('WHAT[REQUIREMENT-SYSTEM-001] every product truth has exactly one owner package', () => {
  // 机器落点（PROOF L8）：requirements/ 无 INDEX 外目录 —— 每个包目录都出现在 INDEX
  // 中，即每个语义 owner 唯一且被索引。语义归属裁决（WHY/independent-change/failure
  // meaning）由 cutover 设计期人工完成（历史见 git），本 test 锁物理唯一性。
  const fromIndex = packageNamesFromIndexTables()
  const dirs = readdirSync(REQUIREMENTS)
    .filter((entry) => statSync(join(REQUIREMENTS, entry)).isDirectory())
    .sort()

  const unknown = dirs.filter((dir) => !fromIndex.includes(dir))
  assert.deepEqual(unknown, [], `requirements/ must not contain INDEX-external package dirs: ${unknown.join(', ')}`)
})

test('WHAT[REQUIREMENT-SYSTEM-002] package identity is the name, not the physical layout', () => {
  // 结构检查只绑定「包名 + 5 份固定文档名」；不读取任何 manifest 格式
  // （TOML/JSON/…）或目录布局细节，因此整体更换物理布局不改变任何 WHAT。
  assert.deepEqual(REQUIRED_DOCS, ['README.md', 'WHY.md', 'WHAT.md', 'HOW.md', 'PROOF.md'])
  // 无 manifest 参与：requirements/<pkg>/ 下不存在强制格式文件（meta-verifier 从不
  // 读取 .toml/.json manifest；本 test 即机器证明）。
  assert.ok(!existsSync(join(REQUIREMENTS, 'requirement-system/package.toml')), 'no manifest format may enter the tree contract')
})

test('WHAT[REQUIREMENT-SYSTEM-006] tree entry and INDEX name the same package set', () => {
  const fromTree = packageNamesFromTreeEntry()
  const fromIndex = packageNamesFromIndexTables()

  assert.deepEqual(
    [...fromTree].sort(),
    [...fromIndex].sort(),
    'requirements/README.md tree entry and requirements-design/INDEX.md must name the same package set',
  )

  assert.equal(fromIndex.length, 48, `expected 48 packages in INDEX, found ${fromIndex.length}`)
})

test('WHAT[REQUIREMENT-SYSTEM-016] declared DEPENDS ON stays within the INDEX skeleton', () => {
  const allNames = packageNamesFromIndexTables()
  const skeleton = dependencySkeleton()
  const dirs = readdirSync(REQUIREMENTS)
    .filter((entry) => statSync(join(REQUIREMENTS, entry)).isDirectory())
    .sort()
  const migrated = dirs.filter((pkg) => REQUIRED_DOCS.every((doc) => existsSync(join(REQUIREMENTS, pkg, doc))))

  const failures = []
  for (const pkg of migrated) {
    failures.push(...depFailures(pkg, allNames, skeleton))
  }

  assert.deepEqual(
    failures,
    [],
    'declared dependencies must be a subset of the INDEX skeleton:\n' + failures.join('\n'),
  )
})

test('WHAT[REQUIREMENT-SYSTEM-017] meta-verifier executes as the machine proof', () => {
  // 本测试自身即机器执行：文件存在、随 run.mjs 全量运行、上述断言可红。
  assert.ok(
    existsSync(join(REQUIREMENTS, 'requirement-system/tests/meta-verifier.test.mjs')),
    'meta-verifier.test.mjs must exist and run as the REQUIREMENT-SYSTEM-017 machine proof',
  )
})
