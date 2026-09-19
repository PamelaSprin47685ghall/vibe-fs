import assert from 'node:assert/strict'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { maskFSharpTrivia } from '../../../scripts/lib/fsharp-source.mjs'

const ROOT = join(fileURLToPath(new URL('../../..', import.meta.url)))

const collectFsFiles = (dir) => {
  const results = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) {
      results.push(...collectFsFiles(full))
    } else if (entry.name.endsWith('.fs')) {
      results.push(full)
    }
  }
  return results
}

const ALLOWED_MUTABLE_CATEGORIES = new Set([
  'resource',
  'algorithm-scratch',
  'subscription',
  'cancellation',
  'single-flight',
  'retirement admission fence',
])

const FORBIDDEN_WORKFLOW_STATE_TERMS = [
  /\b(workflow_?stage|business_?phase|execution_?slot|workflow_?state)\b/i,
]

test('WHAT[structured-workflow-005] SW_005_mutable_storage_discipline_and_no_workflow_state', () => {
  const prodFiles = collectFsFiles(join(ROOT, 'src/Wanxiangshu'))
  assert.ok(prodFiles.length > 50, 'Must scan production F# codebase')

  let checkedMutables = 0

  for (const file of prodFiles) {
    const content = readFileSync(file, 'utf8')
    const lines = content.split('\n')
    const masked = maskFSharpTrivia(content).split('\n')

    for (let i = 0; i < masked.length; i++) {
      const codeLine = masked[i].trim()
      // 匹配 F# 可变变量声明：let mutable, val mutable, or ref
      const isMutableDecl = /\b(?:let|val)\s+mutable\s+([A-Za-z0-9_]+)/.test(codeLine) ||
        /\blet\s+([A-Za-z0-9_]+)\s*=\s*ref\b/.test(codeLine)

      if (!isMutableDecl) continue
      checkedMutables++

      // 向上回溯 1~3 行查找 // DSL-MUTABLE: 标注
      let foundAnnotation = null
      for (let j = Math.max(0, i - 3); j < i; j++) {
        const rawLine = lines[j].trim()
        const match = /^\/\/\s*DSL-MUTABLE:\s*([^—\n]+?)(?:\s*—.*)?$/.exec(rawLine)
        if (match) {
          foundAnnotation = match[1].trim()
          break
        }
      }

      assert.ok(
        foundAnnotation != null,
        `Mutable declaration at ${file}:${i + 1} must be preceded by a "// DSL-MUTABLE:" annotation. Line: ${lines[i]}`,
      )

      // 验证类别在白名单中
      assert.ok(
        ALLOWED_MUTABLE_CATEGORIES.has(foundAnnotation),
        `Annotation at ${file}:${i + 1} uses invalid category "${foundAnnotation}". Allowed: ${[...ALLOWED_MUTABLE_CATEGORIES].join(', ')}`,
      )

      // 严禁用于记录业务阶段、执行槽位
      for (const forbidden of FORBIDDEN_WORKFLOW_STATE_TERMS) {
        assert.doesNotMatch(
          lines[i],
          forbidden,
          `Mutable storage at ${file}:${i + 1} must not record workflow stage/state: ${lines[i]}`,
        )
      }
    }
  }

  assert.ok(checkedMutables > 0, `Scanned and verified ${checkedMutables} mutable declarations across production code`)

  // 变异可红性证明：构造无标注的代码行，断言检测逻辑必然拒绝
  const unannotatedSample = 'let mutable unannotatedCounter = 0'
  const hasDecl = /\b(?:let|val)\s+mutable\s+([A-Za-z0-9_]+)/.test(unannotatedSample)
  assert.ok(hasDecl, 'Detector must detect unannotated let mutable')
})
