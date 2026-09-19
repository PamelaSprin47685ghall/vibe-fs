import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { checks } from '../../../scripts/check.mjs'

const ROOT = join(fileURLToPath(new URL('../../..', import.meta.url)))

test('WHAT[verification-system-012] VS_012_no_mechanical_line_count_gate_or_advisory', () => {
  // 1. 扫描 scripts/check.mjs 注册的全部门禁脚本
  assert.ok(checks.length >= 10, 'Gate list in check.mjs must contain active gates')

  for (const checkScript of checks) {
    const content = readFileSync(checkScript, 'utf8')

    // 严禁设立机械的文件行数硬门禁：不能以 lines.length 或行数上限作为报错依据
    assert.doesNotMatch(
      content,
      /\b(?:maxLines|maxFileLines|max_lines|lines\.length\s*>\s*\d{3,})\b.*?(?:push\(|error|fail)/i,
      `Gate script ${checkScript} must not enforce mechanical line count limits`,
    )

    // 严禁设立针对行数的 advisory 警告扫描器
    assert.doesNotMatch(
      content,
      /\b(?:line-count-advisory|file-too-long|advisory-line-limit)\b/i,
      `Gate script ${checkScript} must not emit mechanical line count advisories`,
    )
  }

  // 2. 检查 scripts/checks 目录下的全部脚本
  const checksDir = join(ROOT, 'scripts/checks')
  const allGateFiles = readdirSync(checksDir).filter((f) => f.endsWith('.mjs'))

  for (const gateFile of allGateFiles) {
    const content = readFileSync(join(checksDir, gateFile), 'utf8')
    assert.doesNotMatch(
      content,
      /issue.*?(?:file too long|line count exceeds|exceeded max lines)/i,
      `Gate ${gateFile} must not fail or issue warnings based on file line count`,
    )
  }

  // 3. 检查 package.json scripts，不设立机械行数门禁命令
  const pkgJson = JSON.parse(readFileSync(join(ROOT, 'package.json'), 'utf8'))
  for (const [scriptName, scriptCmd] of Object.entries(pkgJson.scripts ?? {})) {
    assert.doesNotMatch(
      scriptCmd,
      /\b(?:cloc|wc -l|line-count-check)\b/i,
      `npm script "${scriptName}" must not contain mechanical line count gates`,
    )
  }

  // 4. 变异可红性证明：若存在伪造的机械行数门禁，检测模式必须能触发
  const simulatedBadGate = 'if (lines.length > 500) issues.push({ message: "file too long" })'
  assert.match(
    simulatedBadGate,
    /lines\.length\s*>\s*\d{3,}.*?(?:push\(|error|fail)/i,
    'Detector must catch simulated line count hard gate',
  )
})
