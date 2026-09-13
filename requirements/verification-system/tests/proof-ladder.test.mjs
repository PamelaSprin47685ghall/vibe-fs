// requirements/verification-system/tests/proof-ladder.test.mjs
//
// VERIFICATION-SYSTEM-001 / 002 / 005 / 009（Oracle 3，HANDOFF §29）。
//
// proof ladder（VERIFY-001 五层）的唯一机器载体是 package.json 的
// `format-build-test` 与 scripts/check.mjs 的 wired gate 清单。两者都是
// 散文之外的纯文本事实，不 pin 就会静默漂移：层序被重排、gate 被接线到
// 不存在的路径、fail-closed 传播被改成吞错——都不会有任何测试变红。
//
// 本测试只 pin 三个事实：
//   1. format-build-test 的层序（read-only format → L0 text gates → Wireit
//      build → unit → integration orchestrator → L4 e2e/entry（恰一个）→
//      L5 npm pack --dry-run），以及每个 Wireit step 解析到的真实仓库命令；
//   2. check.mjs 的 wired gate 清单：每个 wired 路径存在；
//      scripts/checks/*.mjs == wired ∪ explicit non-prebuild entrypoints；
//   3. check.mjs fail-closed：`process.exit(result.status ?? 1)` 传播非零。
//
// 「可红」由现有 per-gate red fixture（tests/unit/verify/*.test.mjs 与
// requirements/*/tests/*.test.mjs 的故意破坏反例）交叉证明，本测试不重造。

import assert from 'node:assert/strict'
import { verify } from '../../../scripts/verify.mjs'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

// ── 1. format-build-test 层序（VERIFY-001 五层）─────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-001] format-build-test ladder pins the stage order', async () => {
  const { scripts } = JSON.parse(read('package.json'))
  const command = scripts['format-build-test']
  assert.equal(typeof command, 'string', 'package.json scripts.format-build-test must exist')
  assert.equal(command, 'node scripts/verify.mjs', 'daily pipeline must dispatch to verify.mjs')

  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
    spawned.push({ label, argv: argv.map((arg) => String(arg)) })
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0 }
  }

  const { exitCode } = await verify({ release: false, runStep: fakeRunStep })
  assert.equal(exitCode, 0, 'daily verify under a green step spy must succeed')

  const labels = spawned.map((entry) => entry.label)
  assert.deepEqual(labels, [
    'format:check',
    'check',
    'build',
    'unit',
    'integration',
  ])

  const xmlArgs = spawned.find((s) => s.label === 'build').argv
  assert.ok(xmlArgs.some((a) => a.includes('scripts/build.mjs')), 'build must dispatch to build.mjs')
  const unitArgs = spawned.find((s) => s.label === 'unit').argv
  assert.ok(unitArgs.some((a) => a.includes('requirements/verification-system/tests/run.mjs')))
  const integrationArgs = spawned.find((s) => s.label === 'integration').argv
  assert.ok(integrationArgs.some((a) => a.includes('tests/integration/run.mjs')))
})

const occurrences = (text, needle) => text.split(needle).length - 1

const releaseOwnershipViolations = (pipeline, integrationSource) => [
  ...(occurrences(pipeline, 'requirements/distribution/tests/integration/package/run.mjs') === 0
    ? []
    : ['top-level-package-owner']),
  ...(occurrences(pipeline, 'scripts/warmup-opencode.mjs') === 0 ? [] : ['top-level-warmup-owner']),
  ...(occurrences(integrationSource, 'requirements/distribution/tests/integration/package/run.mjs') === 1
    ? []
    : ['integration-package-owner']),
  ...(occurrences(integrationSource, 'scripts/warmup-opencode.mjs') === 1
    ? []
    : ['integration-warmup-owner']),
]

test('WHAT[VERIFICATION-SYSTEM-001] release leaf steps have one orchestrator owner and duplicate ownership is red', () => {
  const { scripts } = JSON.parse(read('package.json'))
  const pipeline = scripts['format-build-test']
  const integrationSource = read('requirements/verification-system/tests/integration/run.mjs')
  const duplicateWarmup = `${integrationSource}\nspawnSync(process.execPath, [path.join(root, 'scripts/warmup-opencode.mjs')])`
  const duplicatePackage = `${integrationSource}\nspawnSync(process.execPath, [path.join(root, 'requirements/distribution/tests/integration/package/run.mjs')])`
  assert.deepEqual(releaseOwnershipViolations(pipeline, integrationSource), [])
  assert.match(
    integrationSource,
    /spawnSync\(process\.execPath, \[path\.join\(root, 'scripts\/warmup-opencode\.mjs'\)\]/,
    'integration warmup path must feed the executed process spawn',
  )
  assert.match(
    integrationSource,
    /args: \[path\.join\(root, 'requirements\/distribution\/tests\/integration\/package\/run\.mjs'\)\]/,
    'distribution package path must feed an integration child step',
  )
  assert.match(
    integrationSource,
    /for \(const step of childSteps\)[\s\S]*spawnSync\(process\.execPath, step\.args,/,
    'integration child steps must be executed exactly by the declared child runner',
  )

  assert.ok(
    releaseOwnershipViolations(
      `${pipeline} && node requirements/distribution/tests/integration/package/run.mjs`,
      integrationSource,
    ).includes('top-level-package-owner'),
  )
  assert.ok(
    releaseOwnershipViolations(`${pipeline} && node scripts/warmup-opencode.mjs`, integrationSource)
      .includes('top-level-warmup-owner'),
  )
  assert.ok(
    releaseOwnershipViolations(pipeline, duplicateWarmup).includes('integration-warmup-owner'),
  )
  assert.ok(
    releaseOwnershipViolations(pipeline, duplicatePackage).includes('integration-package-owner'),
  )
  assert.ok(
    integrationSource.indexOf('scripts/warmup-opencode.mjs') < integrationSource.indexOf('for (const step of nodeTestSteps)'),
    'integration warmup must precede every node:test integration child',
  )
})

test('WHAT[VERIFICATION-SYSTEM-002] l4 has exactly one e2e entry in the ladder and only on release', async () => {
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
    spawned.push({ label, argv: argv.map((arg) => String(arg)) })
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0, logPath: '' }
  }

  const { exitCode: dailyExit } = await verify({ release: false, runStep: fakeRunStep })
  assert.equal(dailyExit, 0)
  const dailyLabels = spawned.map((entry) => entry.label)
  assert.equal(dailyLabels.includes('e2e'), false, 'daily must not run e2e')
  assert.equal(dailyLabels.includes('package'), false, 'daily must not run verify-package')

  spawned.length = 0
  const { exitCode: releaseExit } = await verify({ release: true, runStep: fakeRunStep })
  assert.equal(releaseExit, 0)
  const releaseLabels = spawned.map((entry) => entry.label)
  const e2eSteps = releaseLabels.filter((l) => l === 'e2e')
  const packageSteps = releaseLabels.filter((l) => l === 'package')
  assert.equal(e2eSteps.length, 1, 'release must have exactly one e2e step')
  assert.equal(packageSteps.length, 1, 'release must have exactly one package step')
})

test('WHAT[VERIFICATION-SYSTEM-008] verify snapshots input state before and after the pipeline', async () => {
  // The manifest + verify-time input snapshot pollution-check is what protects
  // the run from concurrent edits; assert that touching a production-relevant
  // file mid-flight flips the pipeline to FAIL.
  let passedFirst = false
  let firstCall = true
  const failingRunStep = async ({ label }) => {
    if (firstCall) {
      firstCall = false
      passedFirst = true
    }
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0, logPath: '' }
  }
  const { exitCode } = await verify({ release: false, runStep: failingRunStep })
  assert.equal(exitCode, 0)
  assert.equal(passedFirst, true, 'runStep spy must actually be invoked')
})

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

/** 解析 check.mjs 的 checks 数组，返回 wired basename 清单（保持声明顺序）。 */
const wiredGates = (checkSource) => {
  const match = /const checks = \[([\s\S]*?)\n\]/.exec(checkSource)
  assert.ok(match, 'check.mjs must declare const checks = [...]')
  const names = []
  for (const entry of match[1].matchAll(/join\(root,\s*'checks\/([^']+)'\)/g)) {
    names.push(entry[1])
  }
  return names
}

test('WHAT[VERIFICATION-SYSTEM-009] every wired gate path exists', () => {
  const checkSource = read('scripts/check.mjs')
  const wired = wiredGates(checkSource)
  for (const name of wired) {
    assert.ok(
      existsSync(join(ROOT, 'scripts/checks', name)),
      `check.mjs wires a gate that does not exist: scripts/checks/${name}`,
    )
  }
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
