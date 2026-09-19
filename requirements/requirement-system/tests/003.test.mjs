import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

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

test('WHAT[requirement-system-003] all packages are axioms and dependencies do not imply override authority', () => {
  // 新 [003]：所有包的规范均视为公理。包之间的依赖关系表示语义递进，不表示执行优先级或规范覆盖。
  // 被依赖的包不获得对下游包命题的裁决权威，反之亦然。
  // 机械断言：全树规范中不存在破坏公理地位的声明（如包声称 OVERRIDE 或 SUPERSEDE 其他包的条款）
  const allNames = packageNamesFromIndexTables()
  const violations = []

  for (const pkg of allNames) {
    const whatFile = join(REQUIREMENTS, pkg, 'WHAT.md')
    if (!existsSync(whatFile)) continue
    const content = read(whatFile)
    const lines = content.split('\n')
    lines.forEach((line, index) => {
      // 排除否定句（如“不表示规范覆盖”、“不获得裁决权威”等公理声明）
      if (/(?:不表示|不得|严禁|不因|不获得|未获得).{0,30}(?:规范覆盖|裁决权威)/.test(line)) {
        return
      }
      if (/(?:覆盖|override|supersede|overrules?)\s+(?:[a-z0-9-]+\s+的?\s*(?:规范|条款|命题|权威)|(?:其他|下游|上游|各)?(?:包)?(?:的)?(?:规范|条款|命题|裁决权威))|(?:获得对|拥有对).{0,20}(?:其他包|下游包|上游包).{0,20}裁决权威/i.test(line)) {
        violations.push(`${pkg}/WHAT.md:${index + 1}: 包含覆盖或优先裁决声明 "${line.trim()}"`)
      }
    })
  }

  assert.deepEqual(
    violations,
    [],
    'package specifications must be axioms without override authority:\n' + violations.join('\n'),
  )
})
