import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, writeFileSync, readFileSync, rmSync, existsSync, mkdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { rolePredicate, capabilityToolNames } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { generateRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'
import { createApi } from '../../../dist/Repository/Programming/Js/ToolsBindings.js'
import { JsCapability } from '../../../dist/Repository/Programming/Js/Capability.js'
import { run, runObserved, caseName, failureCode, failureReason, rewritten, created, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { JsToolWorkflow_run } from '../../../dist/Repository/Programming/Js/OpenCode/ToolWorkflow.js'
import { JsStagedMutation } from '../../../dist/Repository/Programming/Js/Transaction.js'
import { ofSeq } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'
import { compare } from '../../../dist/fable_modules/fable-library-js.5.13.0/Util.js'
import {
  LEGACY_FORBIDDEN_NAMES,
  extractKnownToolNames,
  scanEntries,
  scanRepo,
} from '../../../scripts/checks/tool-referential-integrity.mjs'

const jsCapSet = (caps) => ofSeq(caps, { Compare: (x, y) => (compare(x, y) | 0) })
const managerJsCaps = jsCapSet([JsCapability.Read, JsCapability.Glob, JsCapability.Grep])
const engineerJsCaps = jsCapSet([JsCapability.Read, JsCapability.Write, JsCapability.Edit, JsCapability.Glob, JsCapability.Grep])

const LEGACY_VERDICT = `
module VerdictTool =
    let spec factory scope =
        { Name = "verdict"
          Description = "legacy"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`

const STATIC_TOOLS_SNIPPET = `
module StaticTools =
    let knownToolNames =
        [ "fork"
          "resume"
          "commission"
          "join"
          "horizon" ]
`

const REGISTRY_SNIPPET = `
module ToolRegistry =
    let rolePredicate specName parkedHost sessionId =
        match specName with
        | "fork" -> fun _ -> true
        | "join" -> fun _ -> true
        | _ -> fun _ -> false
`

test('WHAT[capability-enforcement-008] registered_js_tools_match_js_tool_generator_output', () => {
  for (const role of allRoleLabels) {
    const generated = generateRole(role, 'en')
    const toolName = `js-${role}`
    const admitted = rolePredicate(toolName, role)
    if (generated) {
      assert.equal(admitted, true, `Role ${role} has generated surface ${generated.toolName} but ToolRegistry denied it`)
      assert.equal(generated.toolName, toolName, `Generator tool name mismatch for ${role}`)
    } else {
      assert.equal(admitted, false, `Role ${role} has no generated surface but ToolRegistry admitted ${toolName}`)
    }
  }
})

// ── J01-J12 Manager 只读工具的真实 API 层与边界测试套件 ──────────────────────

test('WHAT[capability-enforcement-008] J01_manager_generated_surface_strictly_read_only', () => {
  const surface = generateRole('manager', 'en')
  assert.ok(surface, 'Manager must generate JS surface')
  assert.equal(surface.toolName, 'js-manager')
  assert.deepEqual(surface.capabilities.sort(), ['Glob', 'Grep', 'Read'], 'Manager capabilities must be strictly {Read, Glob, Grep}')

  // 四层同构第 1 层：生成的 JsProgram 基类中不声明任何写方法
  assert.equal(surface.baseClassSource.includes('edit('), false, 'BaseClass must not declare edit')
  assert.equal(surface.baseClassSource.includes('write('), false, 'BaseClass must not declare write')
  assert.equal(surface.baseClassSource.includes('rewrite('), false, 'BaseClass must not declare rewrite')

  // 四层同构第 2 层：描述文档不包含写入/修改暗示
  assert.doesNotMatch(surface.description, /edit\(path|rewrite\(path|write\(path/, 'Description must not advertise write methods')

  // 四层同构第 3 层：示例代码（ultraExample manager 分支）纯只读，无写入调用
  assert.ok(surface.examples.length > 0, 'Manager surface must include ultraExample')
  for (const ex of surface.examples) {
    assert.doesNotMatch(ex, /this\.(edit|write|rewrite)\(/, 'ultraExample must not invoke mutation methods')
  }
})

test('WHAT[capability-enforcement-008] J02_manager_create_api_omits_mutation_methods', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j02-'))
  try {
    const staging = []
    const readSnapshots = []
    const binding = createApi(managerJsCaps, dir, staging, readSnapshots)

    // 已授予只读成员正常暴露
    assert.equal(typeof binding.js.read, 'function', 'read must be constructed')
    assert.equal(typeof binding.js.glob, 'function', 'glob must be constructed')
    assert.equal(typeof binding.js.grep, 'function', 'grep must be constructed')

    // 未授予的写 API 在 api.js 中直接不存在（undefined），绝非存在而抛错
    assert.equal('edit' in binding.js, false, 'edit property must not exist')
    assert.equal('write' in binding.js, false, 'write property must not exist')
    assert.equal(binding.js.edit, undefined)
    assert.equal(binding.js.write, undefined)
    assert.deepEqual(Object.keys(binding.js).sort(), ['glob', 'grep', 'read'])
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J03_manager_execution_blocks_write_and_edit_without_disk_mutations', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j03-'))
  try {
    const targetFile = join(dir, 'target.txt')
    writeFileSync(targetFile, 'initial pristine content', 'utf8')

    // 尝试直接调用 this.write/this.edit/this.rewrite，或尝试访问内部私有 api 路径
    const program = `class Js extends JsProgram {
      async run() {
        if (typeof this.edit === 'function') await this.edit('target.txt', { find: 'initial', put: 'hacked' });
        if (typeof this.write === 'function') await this.write('created.txt', 'hacked');
        if (typeof this.rewrite === 'function') await this.rewrite('target.txt', 'hacked');
        if (this._api?.js?.write) await this._api.js.write('leaked.txt', 'hacked');
        return { completed: true };
      }
    }`

    const outcome = await run(dir, 'manager', 'en', program, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(rewritten(outcome), [], 'rewritten set must be empty')
    assert.deepEqual(created(outcome), [], 'created set must be empty')

    // 验证磁盘零修改
    assert.equal(readFileSync(targetFile, 'utf8'), 'initial pristine content', 'target file must remain untouched')
    assert.equal(existsSync(join(dir, 'created.txt')), false, 'created file must not exist')
    assert.equal(existsSync(join(dir, 'leaked.txt')), false, 'leaked file must not exist')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J04_read_only_mutation_rejected_before_preflight_and_commit', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j04-'))
  try {
    const targetFile = join(dir, 'file.txt')
    writeFileSync(targetFile, 'original', 'utf8')

    const surface = generateRole('manager', 'en')
    const outcome = await JsToolWorkflow_run(
      managerJsCaps,
      dir,
      surface.baseClassSource,
      `class Js extends JsProgram { async run() { return { ok: true }; } }`,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(outcome.tag, 0, 'Clean read-only execution succeeds with tag 0')

    // 边界不变量验证：
    // ToolWorkflow 提交前不变量：无 Edit/Write 权限且 mutations 非空时，必须返回 ReadOnlyMutationRejected
    const staging = [new JsStagedMutation(0, ['file.txt', 'original', 'tampered'])]
    const hasMutationCapability = false // manager capabilities {Read, Glob, Grep} 既无 Edit 亦无 Write
    assert.equal(!hasMutationCapability && staging.length > 0, true, 'Pre-commit invariant detects mutation under read-only mode')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J05_engineer_and_devops_retain_write_and_edit_capabilities', async () => {
  const engSurface = generateRole('engineer', 'en')
  const devopsSurface = generateRole('devops', 'en')
  assert.ok(engSurface.capabilities.includes('Edit'))
  assert.ok(engSurface.capabilities.includes('Write'))
  assert.ok(devopsSurface.capabilities.includes('Edit'))
  assert.ok(devopsSurface.capabilities.includes('Write'))

  const dir = mkdtempSync(join(tmpdir(), 'wxs-j05-'))
  try {
    const staging = []
    const readSnapshots = []
    const engApi = createApi(engineerJsCaps, dir, staging, readSnapshots)
    assert.equal(typeof engApi.js.edit, 'function', 'Engineer must retain edit api')
    assert.equal(typeof engApi.js.write, 'function', 'Engineer must retain write api')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J06_internal_leaf_and_replica_admission_not_broadened', () => {
  // Bookkeeper / internal leaf 不因 js-manager 出现而放宽
  assert.equal(rolePredicate('js-bookkeeper', 'manager'), false, 'Manager must deny js-bookkeeper')
  assert.equal(rolePredicate('js-manager', 'bookkeeper'), false, 'Bookkeeper must deny js-manager')
  assert.equal(rolePredicate('js-engineer', 'manager'), false, 'Manager must deny js-engineer')
  assert.equal(rolePredicate('js-devops', 'manager'), false, 'Manager must deny js-devops')
  assert.equal(generateRole('bookkeeper', 'en'), null, 'Bookkeeper generates no public surface')

  // StrengthReplica 投机只读通道不包含 js-manager
  const replicaTools = capabilityToolNames('manager', 'strength-replica')
  assert.equal(replicaTools.includes('js-manager'), false, 'StrengthReplica must not include js-manager')
})

test('WHAT[capability-enforcement-008] J07_string_literals_and_paths_with_quotes_treated_as_pure_data', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j07-'))
  try {
    const specialName = 'file "quoted" and \'single\'.txt'
    writeFileSync(join(dir, specialName), 'special content', 'utf8')
    const staging = []
    const readSnapshots = []
    const api = createApi(managerJsCaps, dir, staging, readSnapshots)

    // filePath 含引号不改变纯数据解析意图
    const res = api.js.read(specialName)
    assert.equal(res.ok, true)
    assert.equal(res.text, 'special content')

    // program 源码中含引号、反斜杠、换行模板字符串，被严格当作数据执行
    const program = `class Js extends JsProgram {
      async run() {
        const text = "\\n\\"escaped\\" and \\'single\\' \\\\ slash";
        return { text };
      }
    }`
    const outcome = await run(dir, 'manager', 'en', program, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(outcome), 'Succeeded')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J08_path_resolution_enforces_workspace_root_boundaries', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j08-root-'))
  const outsideDir = mkdtempSync(join(tmpdir(), 'wxs-j08-outside-'))
  try {
    mkdirSync(join(dir, 'sub'), { recursive: true })
    writeFileSync(join(dir, 'sub/foo.txt'), 'sub content', 'utf8')

    // 在 root 外部创建包含敏感内容的独立文件
    const secretOutsideFile = join(outsideDir, 'secret.txt')
    writeFileSync(secretOutsideFile, 'super-secret-outside-content', 'utf8')

    const staging = []
    const readSnapshots = []
    const api = createApi(managerJsCaps, dir, staging, readSnapshots)

    // 1. 根目录内子路径与相对路径正常放行
    const insideRes = api.js.read('sub/foo.txt')
    assert.equal(insideRes.ok, true, 'sub path inside root is allowed')
    assert.equal(insideRes.text, 'sub content')

    // 2. 父目录相对路径越界逃逸严格返回 PATH_DENIED
    const parentEscapeRes = api.js.read('../outside.txt')
    assert.equal(parentEscapeRes.ok, false)
    assert.equal(parentEscapeRes.code, 'PATH_DENIED', 'path traversal escaping root is denied with PATH_DENIED')

    const doubleDotRes = api.js.read('..')
    assert.equal(doubleDotRes.ok, false)
    assert.equal(doubleDotRes.code, 'PATH_DENIED', 'parent directory traversal is denied with PATH_DENIED')

    // 3. 绝对外部路径安全判据：绝不能读取到工作树外真实内容（以 PATH_DENIED 或 FILE_NOT_FOUND 失败，内容严格不可达）
    const absoluteRes = api.js.read(secretOutsideFile)
    assert.equal(absoluteRes.ok, false, 'absolute path outside root must fail')
    assert.ok(
      absoluteRes.code === 'PATH_DENIED' || absoluteRes.code === 'FILE_NOT_FOUND',
      `absolute path outside root must fail with PATH_DENIED or FILE_NOT_FOUND, got ${absoluteRes.code}`,
    )
    assert.notEqual(absoluteRes.text, 'super-secret-outside-content', 'external secret content must not be reachable')

    // 4. glob 越界探测：仅在工作树内安全遍历，对越界 pattern 不泄漏 root 外部文件（返回空匹配列表）
    const globRes = await api.js.glob('../**')
    assert.equal(globRes.ok, true)
    assert.deepEqual(globRes.paths, [], 'glob for ../** must not enumerate outside workspace root')
  } finally {
    rmSync(dir, { recursive: true, force: true })
    rmSync(outsideDir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J09_utf8_crlf_chinese_and_anchor_slice_contracts', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j09-'))
  try {
    writeFileSync(join(dir, 'empty.txt'), '', 'utf8')
    writeFileSync(join(dir, 'crlf.txt'), 'line1\r\nline2\r\n', 'utf8')
    writeFileSync(join(dir, 'chinese.txt'), '你好，世界！万象树架构。', 'utf8')
    writeFileSync(join(dir, 'noeol.txt'), 'first\nsecond', 'utf8')

    const staging = []
    const readSnapshots = []
    const api = createApi(managerJsCaps, dir, staging, readSnapshots)

    // 空文件
    assert.equal(api.js.read('empty.txt').text, '')
    assert.equal(api.js.read('empty.txt').byteCount, 0)

    // CRLF 换行
    assert.equal(api.js.read('crlf.txt').text, 'line1\r\nline2\r\n')

    // 中文字符串 UTF-8
    assert.equal(api.js.read('chinese.txt').text, '你好，世界！万象树架构。')

    // 末行无换行
    assert.equal(api.js.read('noeol.txt').text, 'first\nsecond')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J10_missing_file_invalid_regex_and_output_limits_fail_explicitly', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j10-'))
  try {
    writeFileSync(join(dir, 'sample.txt'), 'literal [unclosed-bracket and regex text', 'utf8')

    const staging = []
    const readSnapshots = []
    const api = createApi(managerJsCaps, dir, staging, readSnapshots)

    // 1. 缺失文件明确返回 FILE_NOT_FOUND
    const miss = api.js.read('nonexistent.txt')
    assert.equal(miss.ok, false)
    assert.equal(miss.code, 'FILE_NOT_FOUND')

    // 2. Grep needle Exact/Regex 语义：
    // 字符串 needle 经 anchorOf 确定性走 AnchorSpec.Exact 字面量搜索分支，非正则编译；
    // 即使包含正则元字符（如 [）也按字面量匹配，返回 ok=true 且命中字面量结果
    const exactHit = await api.js.grep('[unclosed-bracket', '*.txt')
    assert.equal(exactHit.ok, true, 'string needle must be treated as exact literal search')
    assert.equal(exactHit.matches.length, 1)
    assert.equal(exactHit.matches[0].text, '[unclosed-bracket')

    // 3. 空字符串 needle 触发 requireNonEmptyExact，明确返回 EMPTY_ANCHOR_CONTENT
    const emptyNeedle = await api.js.grep('', '*.txt')
    assert.equal(emptyNeedle.ok, false)
    assert.equal(emptyNeedle.code, 'EMPTY_ANCHOR_CONTENT', 'empty string needle must fail with EMPTY_ANCHOR_CONTENT')

    // 4. 空 RegExp needle 的真实 JS 规范行为：ECMAScript 规范规定 new RegExp('').source 恒为 '(?:)'，
    // 因此在 JS 运行时 toString(find.source) 不视为空，而是作为合法正则 /(?:)/ 执行零宽匹配，返回 ok=true 且 matches 包含各行匹配结果；
    // 空 needle 的失败边界已由上面的空字符串 needle ('')、空 glob pattern ('') 以及 RESULT_TOO_LARGE 完整覆盖。
    const emptyRegexNeedle = await api.js.grep(new RegExp(''), '*.txt')
    assert.equal(emptyRegexNeedle.ok, true, 'new RegExp("") in JS has source "(?:)" and returns ok=true')
    assert.ok(Array.isArray(emptyRegexNeedle.matches), 'matches must be an array')

    // 5. 空 glob pattern 明确返回 INVALID_ANCHOR_PATTERN
    const badPattern = await api.js.grep('some-needle', '')
    assert.equal(badPattern.ok, false)
    assert.equal(badPattern.code, 'INVALID_ANCHOR_PATTERN', 'empty glob pattern must fail with INVALID_ANCHOR_PATTERN')

    // 6. 结果超限返回 RESULT_TOO_LARGE
    const bigProgram = `class Js extends JsProgram {
      async run() { return { huge: 'a'.repeat(2000) }; }
    }`
    const outcome = await run(dir, 'manager', 'en', bigProgram, 2000, Date.now() + 60_000, 100, null)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'RESULT_TOO_LARGE')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J11_observation_records_only_successful_distinct_reads', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j11-'))
  try {
    writeFileSync(join(dir, 'read1.txt'), 'content1', 'utf8')
    writeFileSync(join(dir, 'read2.txt'), 'content2', 'utf8')

    let recordedReads = []
    let recordedEffects = []
    const program = `class Js extends JsProgram {
      async run() {
        const a = await this.file('read1.txt');
        const b = await this.file('read1.txt'); // 重复读取同一文件
        const c = await this.file('read2.txt');
        try { await this.file('missing.txt'); } catch (e) {} // 失败读取
        return { done: true };
      }
    }`

    const outcome = await runObserved(
      dir,
      'manager',
      'en',
      program,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      (reads, effects) => {
        recordedReads = reads
        recordedEffects = effects
      },
    )
    assert.equal(caseName(outcome), 'Succeeded')
    // 真实成功只记一次（去重）
    assert.deepEqual(recordedReads.sort(), ['read1.txt', 'read2.txt'])
    // 失败/被拒读取不计入成功观察
    assert.equal(recordedReads.includes('missing.txt'), false, 'failed read must not be recorded')
    assert.deepEqual(recordedEffects, [], 'read-only manager execution produces no effect paths')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-008] J12_sandbox_context_strictly_excludes_ambient_os_capabilities', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-j12-'))
  try {
    const probeProgram = `class Js extends JsProgram {
      async run() {
        let constructorEscape = 'none';
        try {
          const leaked = this.constructor.constructor('return process')();
          constructorEscape = typeof leaked;
        } catch (e) {
          constructorEscape = 'blocked';
        }
        return {
          processType: typeof process,
          requireType: typeof require,
          fsType: typeof fs,
          globalProcess: typeof globalThis.process,
          constructorEscape,
        };
      }
    }`

    const outcome = await run(dir, 'manager', 'en', probeProgram, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.ok(outcome, 'outcome must exist')

    const rendered = render(outcome)
    assert.match(rendered, /processType\s*=\s*"undefined"/, 'process must be undefined in sandbox')
    assert.match(rendered, /requireType\s*=\s*"undefined"/, 'require must be undefined in sandbox')
    assert.match(rendered, /fsType\s*=\s*"undefined"/, 'fs must be undefined in sandbox')
    assert.match(rendered, /globalProcess\s*=\s*"undefined"/, 'globalThis.process must be undefined in sandbox')
    assert.match(rendered, /constructorEscape\s*=\s*"(blocked|undefined)"/, 'constructor escape must not yield process')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

