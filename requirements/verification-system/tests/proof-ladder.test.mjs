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
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

// ── 1. format-build-test 层序（VERIFY-001 五层）─────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-001] format-build-test ladder pins the five layers in order', () => {
  const { scripts, wireit } = JSON.parse(read('package.json'))
  const command = scripts['format-build-test']
  assert.equal(typeof command, 'string', 'package.json scripts.format-build-test must exist')

  const steps = command.split('&&').map((step) => step.trim()).filter(Boolean)
  const normalized = steps.map((step) => step.replace(/\s+/g, ' '))

  assert.deepEqual(normalized, [
    'npm run format:check', // read-only format verification
    'npm run check', // L0 text gates
    'npm run build', // build（dist 生产字节）
    'node requirements/verification-system/tests/run.mjs', // L1 pure laws + L2 temporal + L3 adapter 契约面
    'node requirements/verification-system/tests/integration/run.mjs',
    'node requirements/verification-system/tests/e2e/entry.test.mjs', // L4 唯一 Long Stroke
    'npm pack --dry-run', // L5 release（打包面）
  ])

  assert.deepEqual(
    Object.fromEntries(['format', 'format:check', 'check', 'build'].map((name) => [name, scripts[name]])),
    {
      format: 'wireit',
      'format:check': 'dotnet tool run fantomas --check src/Wanxiangshu',
      check: 'wireit',
      build: 'wireit',
    },
  )
  assert.deepEqual(
    Object.fromEntries(['format', 'check', 'build'].map((name) => [name, wireit[name]?.command])),
    {
      format: 'dotnet tool run fantomas src/Wanxiangshu',
      check: 'node scripts/check.mjs',
      build: 'node scripts/build.mjs',
    },
  )
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

test('WHAT[VERIFICATION-SYSTEM-002] l4 has exactly one e2e entry in the ladder', () => {
  const { scripts } = JSON.parse(read('package.json'))
  const command = scripts['format-build-test']
  const e2eMentions = command.match(/tests\/e2e\//g) ?? []
  assert.equal(e2eMentions.length, 1, 'format-build-test must reference tests/e2e/ exactly once (One World)')
  assert.ok(command.includes('tests/e2e/entry.test.mjs'), 'the sole e2e mention must be entry.test.mjs')
})

test('WHAT[VERIFICATION-SYSTEM-008] Wireit build fingerprint covers the repository-derived envelope', () => {
  const { wireit } = JSON.parse(read('package.json'))
  assert.deepEqual(
    wireit.build?.files,
    ['**/*', '!dist/**', '!.fable-build/**'],
    'build must fingerprint the whole workspace because LoopDetectorEnvelope derives from every tracked source document',
  )
  assert.deepEqual(wireit.build?.output, ['dist/**'])
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

const WIRED_ALLOWLIST = new Set([
  'spec-rules.mjs', // lib：被 spec.mjs import，不直接 spawn
  'semantic-anchors.mjs', // catalog：被各 gate import 的 anchor 清单，不直接 spawn
  'fsharp-control-pyramid-guide.mjs', // guide lib：被 fsharp-control-pyramid.mjs import
  'js-surface-manifest.mjs', // post-build gate：由 build.mjs 在 fable precompile 后调用（依赖 dist 产物，不能 pre-build）
  'js-module-linkage.mjs', // post-build linkage gate: invoked by build.mjs after Fable emit, cannot run pre-build
  'legacy-horizon-census.mjs', // census tool：由 OBL-007 历史 detector 退出验证调用
  'locality-dependencies.mjs', // report-only integration analyzer：M6.4 原子切换前不得成为 pre-build release gate
])

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

test('WHAT[VERIFICATION-SYSTEM-005] owner-contracts producer precedes every semantic consumer gate', () => {
  const checkSource = read('scripts/check.mjs')
  const wired = wiredGates(checkSource)
  const semanticOwners = wired.indexOf('semantic-owners.mjs')
  const ownerContracts = wired.indexOf('owner-contracts.mjs')
  assert.ok(semanticOwners >= 0 && ownerContracts === semanticOwners + 1)
  for (const consumer of [
    'dsl-ownership.mjs',
    'authority-boundary.mjs',
    'semantic-decorator-invariant.mjs',
  ]) assert.ok(ownerContracts < wired.indexOf(consumer), `${consumer} must run after the contract registry gate`)
})

test('WHAT[VERIFICATION-SYSTEM-010] wired gate count has a non-shrinking floor', () => {
  const checkSource = read('scripts/check.mjs')
  const wired = wiredGates(checkSource)
  // 2026-08-15：kolmogorov-size 与 enforcer-cross-family-collision 两门按用户要求
  // 删除，18 = 当前 wired 数。2026-08-19：cross-callback-pc 门新增，22 = 当前 wired 数。
  // 再删任何 gate 必须显式下调本下限（ratchet 语义，验收判据只收紧不放宽）。
  assert.ok(wired.length >= 22, `expected a substantial wired gate list, found ${wired.length}`)
})

test('WHAT[VERIFICATION-SYSTEM-004] checks directory is wired plus allowlist only', () => {
  const checkSource = read('scripts/check.mjs')
  const wired = wiredGates(checkSource)
  const actual = readdirSync(join(ROOT, 'scripts/checks'))
    .filter((name) => name.endsWith('.mjs'))
    .sort()

  assert.deepEqual(
    actual,
    [...new Set([...wired, ...WIRED_ALLOWLIST])].sort(),
    'scripts/checks/*.mjs must equal wired gates ∪ explicit non-prebuild entrypoints',
  )
})

// ── 3. check.mjs fail-closed 传播 ────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-005] check.mjs propagates nonzero fail-closed', () => {
  const checkSource = read('scripts/check.mjs')
  assert.match(
    checkSource,
    /process\.exit\(result\.status \?\? 1\)/,
    'check.mjs must propagate result.status ?? 1 (a gate that cannot report a status must still fail closed)',
  )
})

test('WHAT[VERIFICATION-SYSTEM-005] fail-closed propagates a failing gate exit code', () => {
  // 行为面：把 check.mjs 的 checks 数组替换为单个必败 gate，spawn 后必须
  // 以该 gate 的退出码退出——不是吞错、不是转绿。
  const dir = mkdtempSync(join(tmpdir(), 'proof-ladder-fail-'))
  try {
    const checksDir = join(dir, 'checks')
    mkdirSync(checksDir)
    writeFileSync(join(checksDir, 'failing.mjs'), 'process.exit(7)\n')
    const variant = read('scripts/check.mjs').replace(
      /const checks = \[[\s\S]*?\]/,
      "const checks = [join(root, 'checks/failing.mjs')]",
    )
    writeFileSync(join(dir, 'check.mjs'), variant)
    const result = spawnSync(process.execPath, [join(dir, 'check.mjs')], { encoding: 'utf8' })
    assert.equal(result.status, 7, `failing gate must propagate its exit code; stderr: ${result.stderr}`)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-005] fail-closed treats an unspawnable gate as failure', () => {
  // result.status 为 null（spawn 失败，例如脚本不存在）时 ?? 1 必须判失败，
  // 不能把「根本没跑起来」当成通过。
  const dir = mkdtempSync(join(tmpdir(), 'proof-ladder-missing-'))
  try {
    mkdirSync(join(dir, 'checks'))
    const variant = read('scripts/check.mjs').replace(
      /const checks = \[[\s\S]*?\]/,
      "const checks = [join(root, 'checks/does-not-exist.mjs')]",
    )
    writeFileSync(join(dir, 'check.mjs'), variant)
    const result = spawnSync(process.execPath, [join(dir, 'check.mjs')], { encoding: 'utf8' })
    assert.equal(result.status, 1, `unspawnable gate must exit 1 (status ?? 1); stderr: ${result.stderr}`)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
