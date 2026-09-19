import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname

const readIfExists = (rel) => {
  const full = join(ROOT, rel)
  if (!existsSync(full)) return null
  return readFileSync(full, 'utf8')
}

test('WHAT[provider-language-012] bilingual prompts maintain semantic parity across core roles and forbid invalid fission claims', async () => {
  // ── 1. Engineer: 明确负责本地事实调查与源码工作，无真实执行权，完成即返回；明确是唯一允许使用 Fission 的角色 ──
  const engZh = readIfExists('resources/provider/role/engineer/zh-CN.md')
  const engEn = readIfExists('resources/provider/role/engineer/en.md')
  assert.ok(engZh, 'engineer zh-CN.md must exist')
  assert.ok(engEn, 'engineer en.md must exist')

  assert.match(engZh, /本地事实调查与源码工作/, 'Engineer zh prompt must assert local investigation and source work')
  assert.match(engZh, /没有 bash[\s\S]*?不能执行命令|不执行真实命令/, 'Engineer zh prompt must assert no command execution')
  assert.match(engZh, /唯一允许使用 Fission 的角色/, 'Engineer zh prompt must assert fission exclusivity')
  assert.match(engZh, /本次工作完成[\s\S]*?立即返回 Manager/, 'Engineer zh prompt must assert return to manager upon completion')

  assert.match(engEn, /local facts investigation and changing the written world/i, 'Engineer en prompt must assert local investigation and source work')
  assert.match(engEn, /has no bash access and cannot execute commands|do not execute commands|may not execute real commands/i, 'Engineer en prompt must assert no command execution')
  assert.match(engEn, /only role permitted to use Fission/i, 'Engineer en prompt must assert fission exclusivity')
  assert.match(engEn, /Finish this assignment and return to the\s+Manager/i, 'Engineer en prompt must assert return to manager upon completion')

  // ── 2. DevOps: 明确具备完整本地工程与真实执行能力，拥有非架构级直接修复授权；明确不能 Fission，不差遣其他工程代理 ──
  const devZh = readIfExists('resources/provider/role/devops/zh-CN.md')
  const devEn = readIfExists('resources/provider/role/devops/en.md')
  assert.ok(devZh, 'devops zh-CN.md must exist')
  assert.ok(devEn, 'devops en.md must exist')

  assert.match(devZh, /完整的本地工程能力与真实执行能力/, 'DevOps zh prompt must assert full engineering and execution capacity')
  assert.match(devZh, /直接修复源码|自主局部修复/, 'DevOps zh prompt must assert direct repair authorization')
  assert.match(devZh, /不能 Fission[，,]\s*不创建或差遣其他工程代理/, 'DevOps zh prompt must forbid fission and dispatching other agents')

  assert.match(devEn, /full local engineering (?:and real command execution )?capabilit(?:y|ies)/i, 'DevOps en prompt must assert full engineering and execution capacity')
  assert.match(devEn, /repair the source directly|Direct engineering and autonomous local repair/i, 'DevOps en prompt must assert direct repair authorization')
  assert.match(devEn, /cannot Fission, do not create or dispatch other engineering agents/i, 'DevOps en prompt must forbid fission and dispatching other agents')

  // ── 3. Manager: 明确管理任意数量 Engineer 与唯一固定 DevOps；明确自身不能 Fission，不创建管理分身 ──
  const mgrZh = readIfExists('resources/provider/role/manager/zh-CN.md')
  const mgrEn = readIfExists('resources/provider/role/manager/en.md')
  assert.ok(mgrZh, 'manager zh-CN.md must exist')
  assert.ok(mgrEn, 'manager en.md must exist')

  assert.match(mgrZh, /Engineer 分别承担有边界的工作[，,]运行时绑定一名固定 DevOps/, 'Manager zh prompt must assert managing Engineers and fixed DevOps')
  assert.match(mgrZh, /不能使用 Fission。需要并行时[，,]分派独立 Engineer[，,]不创建自己的副本/, 'Manager zh prompt must forbid fission and clones')

  assert.match(mgrEn, /Engineers assigned to bounded work, and one fixed\s+DevOps bound by the runtime/i, 'Manager en prompt must assert managing Engineers and fixed DevOps')
  assert.match(mgrEn, /cannot use Fission\. Delegate independent work to Engineers; do not create copies of yourself/i, 'Manager en prompt must forbid fission and clones')

  // ── 4. Sphinx 内部 Engineer: 明确本次调用仅调研现有本地事实，无修改、无执行、无 Fission 权 ──
  assert.match(
    engZh,
    /Sphinx 内部调用另受只读任务约束：不修改、不执行、不 Fission、不递归启动探究/,
    'Sphinx internal engineer zh prompt must assert read-only, no mutation, no execution, no fission',
  )
  assert.match(
    engEn,
    /For a Sphinx invocation, obey its narrower read-only charge[\s\S]*?Do not mutate,\s*execute,\s*use Fission,\s*or start another\s+investigation workflow/i,
    'Sphinx internal engineer en prompt must assert read-only, no mutation, no execution, no fission',
  )

  // ── 5. 全仓删除违规示例：严禁在任何双语提示词中保留「Manager 可分身」「DevOps 可分身」等肯定句式 ──
  const { scanForbiddenPromptPhrases } = await import('../../../scripts/checks/language-parity-gate.mjs')
  const violations = scanForbiddenPromptPhrases(join(ROOT, 'resources/provider'))
  assert.equal(violations.length, 0, 'Zero affirmative invalid fission claims must exist across provider resources')

  // ── 6. Language Parity Gate 门禁整体通过与可红性验证 ──
  const { check } = await import('../../../scripts/checks/language-parity-gate.mjs')
  const result = check({ root: ROOT })
  assert.equal(result.ok, true, 'language-parity-gate must pass cleanly on current repository')
  assert.equal(result.issues.length, 0, 'language-parity-gate must have 0 issues')

  // 可红性验证：证明门禁遇到违规分身短语时必定判红
  const { FORBIDDEN_PROMPT_PATTERNS } = await import('../../../scripts/checks/language-parity-gate.mjs')
  const dummyBadLine = '在这个流程中，Manager 可分身并发执行多个任务。'
  const matched = FORBIDDEN_PROMPT_PATTERNS.some((p) => p.pattern.test(dummyBadLine))
  assert.equal(matched, true, 'forbidden prompt patterns must reliably catch affirmative manager fission claims')
})
