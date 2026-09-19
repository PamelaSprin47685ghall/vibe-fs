import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { basename, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const REQUIREMENTS = join(ROOT, 'requirements')

const INDEX_FILE = join(ROOT, 'requirements/INDEX.md')

const TREE_ENTRY = join(REQUIREMENTS, 'README.md')

const read = (path) => readFileSync(path, 'utf8')

const REQUIRED_DOCS = ['WHY.md', 'WHAT.md']

const packageNamesFromTreeEntry = () => {
  const text = read(TREE_ENTRY)
  const names = [...text.matchAll(/\]\(([a-z][a-z0-9-]*)(?:\/(?:WHAT|WHY|README)\.md|\/)?\)/g)].map((m) => m[1])
  return [...new Set(names)]
}

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

const DEPENDS_ON_TRIGGER = /^\s*(?:#+\s*|\*\*\s*)?DEPENDS\s+ON\b/i

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

const landingFileTokens = (row) => {
  const cells = row.split('|').map((cell) => cell.trim())
  const landing = cells[2] ?? ''
  return [...landing.matchAll(/(?:requirements\/|tests\/|scripts\/)[\w./-]+\.(?:test\.mjs|mjs)/g)].map(
    (m) => m[0],
  )
}

const resolveLanding = (pkg, token) => {
  const repo = join(ROOT, token)
  if (existsSync(repo)) return repo
  if (token.startsWith('tests/')) {
    const local = join(REQUIREMENTS, pkg, token)
    if (existsSync(local)) return local
  }
  return repo
}

const docFailures = (pkg) => {
  const failures = []
  for (const doc of REQUIRED_DOCS) {
    if (!existsSync(join(REQUIREMENTS, pkg, doc))) failures.push(`${pkg}: missing ${doc}`)
  }
  const testsDir = join(REQUIREMENTS, pkg, 'tests')
  if (!existsSync(testsDir) || !statSync(testsDir).isDirectory()) {
    failures.push(`${pkg}: missing tests/ directory`)
  }
  return failures
}

const liveClauseNumbers = (pkg) => {
  const whatPath = join(REQUIREMENTS, pkg, 'WHAT.md')
  if (!existsSync(whatPath)) return new Set()
  const text = read(whatPath)
  const numbers = new Set()
  for (const line of text.split('\n')) {
    const mBracket = /^##\s+\[(\d{3})\]/.exec(line)
    if (mBracket) {
      if (!/已删除|已废止/.test(line)) numbers.add(mBracket[1])
    }
  }
  return numbers
}

const danglingTestFailures = (pkg) => {
  const failures = []
  const testsDir = join(REQUIREMENTS, pkg, 'tests')
  if (!existsSync(testsDir) || !statSync(testsDir).isDirectory()) return failures
  const live = liveClauseNumbers(pkg)
  const files = readdirSync(testsDir).filter((name) => name.endsWith('.test.mjs'))
  for (const file of files) {
    const match = /^(\d{3})\.test\.mjs$/.exec(file)
    if (match) {
      const num = match[1]
      if (!live.has(num)) {
        failures.push(
          `${pkg}/tests/${file}: 测试编号 [${num}] 在同包 WHAT.md 存活条款中不存在。如果某条款已删除，对应的测试需要提示用户进行相应处理。`,
        )
      }
    }
  }
  return failures
}

const proofFailures = (pkg) => {
  const failures = []
  const howPath = join(REQUIREMENTS, pkg, 'HOW.md')
  if (!existsSync(howPath)) return failures
  const howText = read(howPath)
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

const depFailures = (pkg, allNames, skeleton) => {
  const failures = []
  for (const doc of ['WHY.md', 'WHAT.md']) {
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

test('WHAT[requirement-system-017] meta-verifier executes as the machine proof', () => {
  const fromIndex = packageNamesFromIndexTables()
  const skeleton = dependencySkeleton()

  // 1. 包目录封闭性：requirements/ 规范树仅包含 INDEX.md 所列目录
  const dirs = readdirSync(REQUIREMENTS)
    .filter((entry) => statSync(join(REQUIREMENTS, entry)).isDirectory())
    .sort()
  const unknownDirs = dirs.filter((dir) => !fromIndex.includes(dir))
  assert.deepEqual(unknownDirs, [], `requirements/ must not contain INDEX-external package dirs: ${unknownDirs.join(', ')}`)

  // 2. 文档齐备性：所有包完整包含 WHY/WHAT 与 tests/ 目录（对齐 [006]）
  const docErrors = []
  for (const pkg of fromIndex) {
    docErrors.push(...docFailures(pkg))
  }
  assert.deepEqual(docErrors, [], 'all packages must carry complete documentation and tests/ directory:\n' + docErrors.join('\n'))

  // 3. 依赖声明合法性：DEPENDS ON 集合必须是 INDEX 依赖骨架的子集
  const depErrors = []
  for (const pkg of fromIndex) {
    depErrors.push(...depFailures(pkg, fromIndex, skeleton))
  }
  assert.deepEqual(depErrors, [], 'declared dependencies must be a subset of the INDEX skeleton:\n' + depErrors.join('\n'))

  // 4. 已声明证明落点引用完整性与测试文件物理存在性
  const proofErrors = []
  for (const pkg of fromIndex) {
    proofErrors.push(...proofFailures(pkg))
  }
  assert.deepEqual(proofErrors, [], 'declared proof landing files must physically exist:\n' + proofErrors.join('\n'))

  // 5. 存活条款与测试对应性：每个 tests/ 下 NNN.test.mjs 的编号必须在同包 WHAT.md 存活条款中存在
  const danglingErrors = []
  for (const pkg of fromIndex) {
    danglingErrors.push(...danglingTestFailures(pkg))
  }
  assert.deepEqual(
    danglingErrors,
    [],
    'all tests must correspond to live clauses in WHAT.md:\n' + danglingErrors.join('\n'),
  )

  // 6. 测试文件名规范性：requirements/**/tests/ 下所有 .test.mjs 文件名必须匹配 NNN.test.mjs（任意深度）
  const findTestFiles = (dir) => {
    const results = []
    if (!existsSync(dir)) return results
    const entries = readdirSync(dir, { withFileTypes: true })
    for (const entry of entries) {
      const full = join(dir, entry.name)
      if (entry.isDirectory()) {
        results.push(...findTestFiles(full))
      } else if (entry.name.endsWith('.test.mjs')) {
        results.push(full)
      }
    }
    return results
  }

  const testNamingErrors = []
  for (const pkg of fromIndex) {
    const testsDir = join(REQUIREMENTS, pkg, 'tests')
    for (const file of findTestFiles(testsDir)) {
      const base = basename(file)
      if (!/^\d{3}\.test\.mjs$/.test(base)) {
        testNamingErrors.push(
          `${relative(ROOT, file)}: 测试文件名 "${base}" 不符合 NNN.test.mjs 命名规范`,
        )
      }
    }
  }
  assert.deepEqual(
    testNamingErrors,
    [],
    'all .test.mjs files under requirements/**/tests/ must match NNN.test.mjs:\n' + testNamingErrors.join('\n'),
  )

  // 7. 测试用例标题锚点规范性：每个 NNN.test.mjs 内出现的测试用例标题锚点 WHAT[前缀-编号] 必须与 所在包名-文件编号 一致
  const extractTestAnchors = (content) => {
    const anchors = []
    const lines = content.split('\n')
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]
      const m = /^\s*(?:test|it)\s*\(\s*(['"`])([\s\S]*?)\1/.exec(line)
      if (m) {
        const title = m[2]
        const mAnchor = /WHAT\[([^\]]+)\]/.exec(title)
        if (mAnchor) {
          anchors.push({ line: i + 1, raw: mAnchor[0], content: mAnchor[1], title })
        }
      } else {
        const mMulti = /^\s*(?:test|it)\s*\(\s*`([\s\S]*?)`/.exec(line)
        if (mMulti) {
          const title = mMulti[1]
          const mAnchor = /WHAT\[([^\]]+)\]/.exec(title)
          if (mAnchor) {
            anchors.push({ line: i + 1, raw: mAnchor[0], content: mAnchor[1], title })
          }
        }
      }
    }
    return anchors
  }

  const testAnchorErrors = []
  for (const pkg of fromIndex) {
    const testsDir = join(REQUIREMENTS, pkg, 'tests')
    for (const file of findTestFiles(testsDir)) {
      const base = basename(file)
      const mFile = /^(\d{3})\.test\.mjs$/.exec(base)
      if (!mFile) continue

      const expectedNum = mFile[1]
      const expectedAnchor = `WHAT[${pkg}-${expectedNum}]`
      const content = read(file)
      const anchors = extractTestAnchors(content)

      for (const a of anchors) {
        if (a.raw !== expectedAnchor) {
          testAnchorErrors.push(
            `${relative(ROOT, file)}:${a.line}: 测试用例标题锚点 "${a.raw}" 与所在包名-文件编号 "${pkg}-${expectedNum}" 不一致 (用例标题: "${a.title.trim()}")`,
          )
        }
      }
    }
  }
  assert.deepEqual(
    testAnchorErrors,
    [],
    'all test case title anchors in NNN.test.mjs must match package-fileNum:\n' + testAnchorErrors.join('\n'),
  )
})
