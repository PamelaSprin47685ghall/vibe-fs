import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const resources = join(root, 'resources')
const provider = join(resources, 'provider')
const read = (path, locale) => readFileSync(join(provider, path, `${locale}.md`), 'utf8')

function resourceFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)
    return entry.isDirectory() ? resourceFiles(path) : /\.(?:md|json|mjs)$/.test(entry.name) ? [path] : []
  })
}

test('WHAT[OFF-005] distributed resources do not teach retired roles or delegation tools', () => {
  const retired = /\b(?:Coder|Inspector|Distiller)\b|role\/(?:coder|inspector|browser|inquiry|distiller)\b|js-(?:coder|inspector|browser|inquiry|distiller)\b|establish-behavior|repair-behavior|query-shell|stealth-browser-mcp|\b(?:CODER|INSPECTOR|BROWSER|INQUIRY|DISTILLER)_[A-Z]+\b/
  const stale = resourceFiles(resources).flatMap((path) => {
    const matches = readFileSync(path, 'utf8').split('\n').flatMap((line, index) =>
      retired.test(line) ? [`${relative(root, path)}:${index + 1}: ${line}`] : [],
    )
    return matches
  })
  assert.deepEqual(stale, [])
  for (const path of [
    'role/coder', 'role/inspector', 'role/browser', 'role/inquiry', 'role/distiller',
    'tool/inspect', 'tool/establish-behavior', 'tool/repair-behavior', 'tool/query-shell', 'tool/distill',
  ]) assert.equal(existsSync(join(provider, path)), false, `${path} must not ship`)
})

for (const locale of ['en', 'zh-CN']) {
  const english = locale === 'en'
  test(`WHAT[OFF-016] ${locale} Engineer owns source work, not commands or validation dispatch`, () => {
    const law = read('role/engineer', locale)
    assert.match(law, english ? /read-only (?:task|assignment)/i : /只读(?:任务|调研)/)
    assert.match(law, english ? /even (?:a )?read-only command/i : /即使(?:是)?只读命令/)
    assert.match(law, english ? /return[\s\S]{0,120}Manager/i : /(?:交回|返回)[\s\S]{0,100}Manager/)
    assert.doesNotMatch(law, /`git (?:log|show|blame|stat)`/)
    assert.match(read('tool/bash-honeypot/denial', locale), /Engineer/)
  })

  test(`WHAT[OFF-017] ${locale} DevOps repairs directly without a unique-mechanical-fix gate`, () => {
    const law = read('role/devops', locale)
    assert.match(law, english ? /ordinary engineering judgment/i : /普通工程判断/)
    assert.match(law, english ? /(?:does not|without|no)[\s\S]{0,70}(?:separate|per-task|case-by-case)[\s\S]{0,40}(?:approval|authorization)/i : /(?:不需要|无需)[\s\S]{0,40}逐次(?:授权|批准)/)
    assert.match(law, english ? /(?:repair|change)[\s\S]{0,100}(?:re-verify|rerun|re-run)/i : /(?:修复|改动)[\s\S]{0,100}(?:重新验证|重跑)/)
    assert.doesNotMatch(law, /Are several materially different correct worlds still possible|若干种实质不同却都可能正确的/)
    assert.match(law, english ? /cannot (?:use )?Fission/i : /不能(?:使用)?\s*Fission/)
  })

  test(`WHAT[OFF-005] ${locale} dispatch and assessment explain the new division of work`, () => {
    const manager = read('role/manager', locale)
    assert.match(manager, english ? /read-only Engineer/i : /只读[\s\S]{0,20}Engineer/)
    assert.match(manager, /resume[\s\S]{0,60}DevOps/)
    assert.match(read('tool/resume/description', locale), english ? /(?:fixed|bound) DevOps/i : /(?:固定|绑定的)\s*DevOps/)
    assert.match(read('tool/resume/description', locale), english ? /existing Engineer/i : /已有\s*Engineer/)
    assert.match(read('tool/fission/description', locale), /Engineer/)
    assert.match(read('runtime/manager-assess', locale), /Engineer/)
    assert.match(read('world/common-law', locale), /Engineer[\s\S]*DevOps/)
  })
}
