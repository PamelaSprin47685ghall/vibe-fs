import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { basename, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { isProposalPath } from '../../../scripts/lib/spec-rules.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const REQUIREMENTS = join(ROOT, 'requirements')
const INDEX_FILE = join(ROOT, 'requirements/INDEX.md')

const read = (path) => readFileSync(path, 'utf8')

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

const liveClauseNumbers = (pkg) => {
  const whatPath = join(REQUIREMENTS, pkg, 'WHAT.md')
  if (!existsSync(whatPath)) return new Set()
  const text = read(whatPath)
  const numbers = new Set()
  for (const line of text.split('\n')) {
    const mBracket = /^##\s+\[(\d{3})\]/.exec(line)
    if (mBracket && !/已删除|已废止/.test(line)) {
      numbers.add(mBracket[1])
    }
  }
  return numbers
}

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

test('WHAT[requirement-system-018] test filename must match clause exactly and tests for same clause live in one file', () => {
  // [018]：每个有效可执行测试用例，文件名必须恰好对应到条款。同一命题可以拥有多个测试，都放在同一文件。
  const packages = packageNamesFromIndexTables()
  const violations = []

  for (const pkg of packages) {
    const testsDir = join(REQUIREMENTS, pkg, 'tests')
    if (!existsSync(testsDir)) continue

    const liveClauses = liveClauseNumbers(pkg)
    const testFiles = findTestFiles(testsDir)
    const seenClausesInPkg = new Map() // clauseNum -> filePath

    for (const filePath of testFiles) {
      const fileName = basename(filePath)
      const m = /^(\d{3})\.test\.mjs$/.exec(fileName)
      if (!m) {
        violations.push(
          `${relative(ROOT, filePath)}: 文件名 "${fileName}" 不符合 NNN.test.mjs 格式，无法恰好对应到条款`,
        )
        continue
      }

      const clauseNum = m[1]
      // 1. 文件名恰对条款：测试编号必须在同包 WHAT.md 的存活条款中
      if (!liveClauses.has(clauseNum)) {
        violations.push(
          `${relative(ROOT, filePath)}: 测试文件名条款编号 [${clauseNum}] 在 ${pkg}/WHAT.md 存活条款中不存在`,
        )
      }

      // 2. 同一命题同文件：同包下该条款只能有这唯一一个测试文件
      if (seenClausesInPkg.has(clauseNum)) {
        const prev = seenClausesInPkg.get(clauseNum)
        violations.push(
          `${relative(ROOT, filePath)}: 条款 [${clauseNum}] 存在重复测试文件（已有 ${relative(ROOT, prev)}），违背「同一命题测试都在同一文件」约束`,
        )
      } else {
        seenClausesInPkg.set(clauseNum, filePath)
      }
    }
  }

  assert.deepEqual(
    violations,
    [],
    'all tests must strictly match clauses and live in single files per clause:\n' + violations.join('\n'),
  )
})
