// requirements/verification-system/tests/no-line-count-check.test.mjs
//
// VERIFICATION-SYSTEM-012（行数不是门禁，也不做任何机械行数检查）的机器载体。
// 结构性 absence 证明：本包 tests 与 gate 机制（scripts/checks，本包 MECHANISM）
// 内不得出现行数检查代码的指纹字样。任何人重新引入行数检查（门禁或 advisory），
// 本测试立即红。
//
// 指纹 = 已删除的 kolmogorov-size advisory 自留签名。故意不扫泛词：`lineCount`
// 是 diagnostics 的合法字段、覆盖门禁（VERIFY-011）按行统计合法、
// `kolmogorov-principles` 是产品 tool 参数——都不属于行数检查。
//
// 扫描排除自身：本文件必须携带这些指纹才能执行检查。

import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const SELF = basename(fileURLToPath(import.meta.url))

const FINGERPRINT = /SOFT_LIMIT|exceeds advisory|size-advisory|行数/

const codeFiles = (dir) => {
  const out = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) {
      if (entry.name === 'node_modules') continue
      out.push(...codeFiles(full))
    } else if (/\.(mjs|js)$/.test(entry.name) && entry.name !== SELF) {
      out.push(full)
    }
  }
  return out
}

test('VERIFY_012_no_line_count_check_wording_in_package_or_gates', () => {
  const scopes = [
    join(ROOT, 'scripts/checks'),
    join(ROOT, 'requirements/verification-system/tests'),
  ]
  const offenders = []
  for (const scope of scopes) {
    for (const file of codeFiles(scope)) {
      const lines = readFileSync(file, 'utf8').split('\n')
      lines.forEach((line, i) => {
        if (FINGERPRINT.test(line)) offenders.push(`${file.replace(ROOT + '/', '')}:${i + 1}`)
      })
    }
  }
  assert.deepEqual(offenders, [], `line-count check fingerprint found:\n${offenders.join('\n')}`)
})
