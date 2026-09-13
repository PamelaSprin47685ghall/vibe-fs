// requirements/verification-system/tests/proof-ladder.test.mjs
//
// VERIFICATION-SYSTEM-001 / 002 / 005 / 009（Oracle 3，HANDOFF §29）。
//
// proof ladder（VERIFY-001 五层）的唯一机器载体是 package.json 的
 // `format-build-test` 与 scripts/check.mjs 的 wired gate 清单。
 // 本测试验证 verify 调度阶梯与 check 门禁。
//
// 本测试只 pin 三个事实：
//   1. format-build-test 的层序（format:check → check → build → unit → integration，release 追加 e2e 与 package），
//      以及 step 失败中断与传播行为；
//   2. check.mjs 的 wired gate 清单：每个 wired 路径存在且 scripts/checks 目录内 gate 对齐；
//   3. check.mjs fail-closed 传播非零。
//
// 「可红」由现有 per-gate red fixture（tests/unit/verify/*.test.mjs 与
// requirements/*/tests/*.test.mjs 的故意破坏反例）交叉证明，本测试不重造。

import assert from 'node:assert/strict'
import { verify } from '../../../scripts/verify.mjs'
import { checks } from '../../../scripts/check.mjs'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, readlinkSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

function createMemorySink() {
  let buf = ''
  return {
    write(chunk) {
      buf += chunk
    },
    get output() {
      return buf
    },
  }
  }

// ── 1. format-build-test 层序（VERIFY-001 五层）─────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-001] format-build-test ladder pins the stage order', async () => {
  const { scripts } = JSON.parse(read('package.json'))
  const command = scripts['format-build-test']
  assert.equal(typeof command, 'string', 'package.json scripts.format-build-test must exist')
  assert.equal(command, 'node scripts/verify.mjs', 'daily pipeline must dispatch to verify.mjs')

  const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-logs-'))
  const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
    spawned.push({ label, argv: argv.map((arg) => String(arg)) })
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0 }
  }

  try {
    const defaultLatest = join(ROOT, '.fable-build/verify-logs/latest')
    const beforeLink = existsSync(defaultLatest) ? readlinkSync(defaultLatest) : null

    const result = await verify({
      release: false,
      runStep: fakeRunStep,
      output: sink,
      logDirectory: tmpLogDir,
})
    assert.equal(result.exitCode, 0, 'daily verify under a green step spy must succeed')
    assert.equal(result.outcome, 'pass')
    assert.ok(result.steps.every((s) => s.status === 'ok'))

  const labels = spawned.map((entry) => entry.label)
  assert.deepEqual(labels, [
    'format:check',
    'check',
    'build',
    'unit',
    'integration',
  ])

    const buildArgs = spawned.find((s) => s.label === 'build').argv
    assert.ok(buildArgs.some((a) => a.includes('scripts/build.mjs')), 'build must dispatch to build.mjs')
    assert.ok(existsSync(buildArgs[0]), 'build target must exist as real file')
  const unitArgs = spawned.find((s) => s.label === 'unit').argv
  assert.ok(unitArgs.some((a) => a.includes('requirements/verification-system/tests/run.mjs')))
    assert.ok(existsSync(unitArgs[0]), 'unit target must exist as real file')
  const integrationArgs = spawned.find((s) => s.label === 'integration').argv
  assert.ok(integrationArgs.some((a) => a.includes('tests/integration/run.mjs')))
    assert.ok(existsSync(integrationArgs[0]), 'integration target must exist as real file')

    assert.match(sink.output, /PASS  verify daily/)
    const afterLink = existsSync(defaultLatest) ? readlinkSync(defaultLatest) : null
    assert.equal(afterLink, beforeLink, 'default verify-logs/latest must not be modified')
  } finally {
    rmSync(tmpLogDir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-002] release ladder includes clean build, exactly one e2e and one package step', async () => {
  const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-release-'))
  const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
    spawned.push({ label, argv: argv.map((arg) => String(arg)) })
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0 }
  }

  try {
    const { exitCode, outcome, steps } = await verify({
      release: true,
      runStep: fakeRunStep,
      output: sink,
      logDirectory: tmpLogDir,
})
  assert.equal(exitCode, 0)
    assert.equal(outcome, 'pass')
    assert.ok(steps.every((s) => s.status === 'ok'))

  const releaseLabels = spawned.map((entry) => entry.label)
    assert.deepEqual(releaseLabels, [
    'format:check',
    'check',
    'build',
    'unit',
    'integration',
      'e2e',
      'package',
  ])

    const buildCall = spawned.find((s) => s.label === 'build')
    assert.ok(buildCall.argv.includes('--clean'), 'release build argv must contain --clean')

    const e2eCalls = spawned.filter((s) => s.label === 'e2e')
    assert.equal(e2eCalls.length, 1, 'release must have exactly one e2e step')
    assert.ok(existsSync(e2eCalls[0].argv[0]), 'e2e target must exist as real file')

    const packageCalls = spawned.filter((s) => s.label === 'package')
    assert.equal(packageCalls.length, 1, 'release must have exactly one package step')
    assert.ok(existsSync(packageCalls[0].argv[0]), 'package target must exist as real file')

    assert.match(sink.output, /PASS  verify release/)
  } finally {
    rmSync(tmpLogDir, { recursive: true, force: true })
  }
})

for (const failingLabel of ['format:check', 'check', 'build']) {
  test(`WHAT[VERIFICATION-SYSTEM-001] verify halts and marks subsequent steps not-run when ${failingLabel} fails`, async () => {
    const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-fail-'))
    const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
      spawned.push(label)
      if (label === failingLabel) {
        return { label, ok: false, exitCode: 1, signal: null, durationMs: 5 }
  }
      return { label, ok: true, exitCode: 0, signal: null, durationMs: 5 }
    }

    try {
      const result = await verify({
        release: false,
        runStep: fakeRunStep,
        output: sink,
        logDirectory: tmpLogDir,
})
      assert.equal(result.exitCode, 1)
      assert.equal(result.outcome, 'fail')
      assert.equal(spawned.at(-1), failingLabel, `execution must stop after ${failingLabel}`)

      const failedIdx = result.steps.findIndex((s) => s.label === failingLabel)
      assert.ok(failedIdx >= 0)
      assert.equal(result.steps[failedIdx].status, 'failed')
      for (let i = failedIdx + 1; i < result.steps.length; i++) {
        assert.equal(result.steps[i].status, 'not-run', `step ${result.steps[i].label} must be marked not-run`)
    }
      assert.match(sink.output, /FAIL  verify daily/)
    } finally {
      rmSync(tmpLogDir, { recursive: true, force: true })
    }
})
  }

test('WHAT[VERIFICATION-SYSTEM-009] every ladder step target exists as a real file', () => {
  // 层序里的每个入口都必须是真实文件：指向不存在文件的命令恒为「没跑到」，
  // 层序 pin 就退化成文字装饰（VERIFY-004 静态门禁必须命中真实路径）。
  const required = [
    'scripts/check.mjs',
    'scripts/build.mjs',
    'requirements/verification-system/tests/run.mjs',
    'requirements/verification-system/tests/integration/run.mjs',
    'requirements/distribution/tests/integration/package/run.mjs',
    'scripts/warmup-opencode.mjs',
    'requirements/verification-system/tests/e2e/entry.test.mjs',
  ]
  for (const rel of required) {
    assert.ok(existsSync(join(ROOT, rel)), `ladder step target missing: ${rel}`)
  }
})

// ── 2. check.mjs wired gate 清单 ─────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-009] every wired gate path exists and checks set matches scripts/checks directory', () => {
  for (const gatePath of checks) {
    assert.ok(
      existsSync(gatePath),
      `check.mjs wires a gate that does not exist: ${gatePath}`,
    )
  }

  const dirEntries = readdirSync(join(ROOT, 'scripts/checks'))
  const fsxFiles = dirEntries.filter((f) => f.endsWith('.fsx'))
  assert.deepEqual(fsxFiles, [], 'scripts/checks must contain no .fsx custom compiler executables')

  // Data files that are not gate scripts
  const nonGateFiles = new Set([
    'owner-impact-corpus.json',
    'subsystems.json',
    'release-closure-nodes.json',
    // Helper / non-gate modules:
    'fsharp-control-pyramid-guide.mjs',
    'js-module-linkage.mjs',
    'js-surface-gate.mjs',
    'js-surface-manifest.mjs',
  ])

  const wiredBasenames = new Set(checks.map((p) => basename(p)))
  const gateScriptsInDir = dirEntries.filter((f) => f.endsWith('.mjs') && !nonGateFiles.has(f))

  assert.deepEqual(
    [...wiredBasenames].sort(),
    gateScriptsInDir.sort(),
    'checks registered in check.mjs must equal gate scripts in scripts/checks',
  )
})

test('WHAT[VERIFICATION-SYSTEM-009] no custom FCS executable remains after the full-repo ban', () => {
  // GAP-031 全仓自定义 FCS 禁令：扫描链是被删除，不是被禁用。任何自定义 FCS
  // 可执行物（驱动 FSharp.Compiler.Service 的 .fsx，或 shell 到 `dotnet fsi` /
  // 加载 Fable+FCS 程序集 / import 已删除扫描入口的 JS gate）都必须变红。
  // 正常 Fable 编译边界（`dotnet tool run fable`、build.mjs、compile-impact CLI）
  // 不在判据内，本测试不碰它们。
  for (const rel of [
    'scripts/checks/locality-symbol-uses.fsx',
    'scripts/checks/locality-dependencies.mjs',
    'scripts/checks/locality-slice-report.mjs',
    'scripts/lib/locality-dependencies.mjs',
  ]) {
    assert.ok(!existsSync(join(ROOT, rel)), `custom FCS executable must stay deleted: ${rel}`)
  }
  assert.deepEqual(
    readdirSync(join(ROOT, 'scripts/checks')).filter((name) => name.endsWith('.fsx')),
    [],
    'scripts/checks must contain no .fsx custom compiler executables',
  )
  const banned = [
    'FSharp.Compiler.Service',
    'Fable.Compiler.dll',
    'Fable.AST.dll',
    'locality-symbol-uses',
    'scanCompilerObservationsV1',
    'scanDslCompilerEvidence',
    'runLocalityDependencyScan',
    'scanProductionLocalitySliceReportV1',
  ]
  const hits = []
  const walkScripts = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name)
      if (entry.isDirectory()) walkScripts(full)
      else if (entry.name.endsWith('.mjs')) {
        const text = readFileSync(full, 'utf8')
        for (const token of banned) {
          if (text.includes(token)) hits.push(`${full}: ${token}`)
        }
      }
    }
  }
  walkScripts(join(ROOT, 'scripts'))
  assert.deepEqual(hits, [], 'scripts must not reference deleted custom FCS executables or assemblies')
})

// ── 3. check.mjs fail-closed 传播 ────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-005] check.mjs propagates nonzero fail-closed', () => {
  const checkSource = read('scripts/check.mjs')
  assert.match(
    checkSource,
    /process\.exit\(code\)/,
    'check.mjs must propagate nonzero exit code (a gate that fails must exit closed)',
  )
})

test('WHAT[VERIFICATION-SYSTEM-010] acceptance criteria only tighten — a failing gate propagates failure', async () => {
  // 行为面：导入 check.mjs 的 main/runChecks，以失败 gate 测试，必须返回非零退出码。
  const dir = mkdtempSync(join(tmpdir(), 'proof-ladder-fail-'))
  try {
    const checksDir = join(dir, 'checks')
    mkdirSync(checksDir)
    writeFileSync(join(checksDir, 'failing.mjs'), 'export function check() { return { issues: [{ code: "fail", message: "boom" }] } }\n')
    const { main } = await import('../../../scripts/check.mjs')
    const exitCode = await main([], { checkList: [join(checksDir, 'failing.mjs')] })
    assert.equal(exitCode, 1, 'failing gate must return nonzero exit code')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-010] acceptance criteria only tighten — an unreadable gate is failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'proof-ladder-missing-'))
  try {
    const { main } = await import('../../../scripts/check.mjs')
    const exitCode = await main([], { checkList: [join(dir, 'checks/does-not-exist.mjs')] })
    assert.equal(exitCode, 1, 'unreadable gate must return 1')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
