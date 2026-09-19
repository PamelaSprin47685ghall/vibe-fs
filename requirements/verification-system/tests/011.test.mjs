import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync, writeFileSync, readFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { COVERAGE_EXCLUDE_GLOBS, selectProductionModules, verifyCoverageDenominator } = await import("./support/coverage-policy.mjs");


test('WHAT[verification-system-011] selectProductionModules excludes fable_modules so untested modules count at 0%', () => {
  const files = [
    'dist/foo.js',
    'dist/fable_modules/bar.js',
    'dist/sub/fable_modules/baz.js',
    'dist/qux.js',
  ]
  assert.deepEqual(selectProductionModules(files), ['dist/foo.js', 'dist/qux.js'])
})
test('WHAT[verification-system-011] verifyCoverageDenominator catches missing and extra files', () => {
  const expected = ['dist/a.js', 'dist/b.js']
  const reported1 = ['dist/a.js', 'dist/b.js']
  assert.equal(verifyCoverageDenominator(reported1, expected).ok, true)

  const reported2 = ['dist/a.js']
  const check2 = verifyCoverageDenominator(reported2, expected)
  assert.equal(check2.ok, false)
  assert.deepEqual(check2.missing, ['dist/b.js'])

  const reported3 = ['dist/a.js', 'dist/b.js', 'dist/extra.js']
  const check3 = verifyCoverageDenominator(reported3, expected)
  assert.equal(check3.ok, false)
  assert.deepEqual(check3.extra, ['dist/extra.js'])
})
test('WHAT[verification-system-011] coverage exclude globs are fixed: node_modules, fable_modules, tests, scripts', () => {
  assert.deepEqual(COVERAGE_EXCLUDE_GLOBS, [
    '**/node_modules/**',
    '**/fable_modules/**',
    '**/tests/**',
    '**/scripts/**',
  ])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { spawnSync, fork } = await import("node:child_process");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync, statSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { default: path, join } = await import("node:path");
const { default: crypto } = await import("node:crypto");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");
const { walk } = await import("../../../scripts/lib/walk.mjs");
const { runCoverage } = await import("../../../scripts/coverage.mjs");
const { selectProductionModules, verifyCoverageDenominator } = await import("./support/coverage-policy.mjs");

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const C8_BIN = path.join(REPO_ROOT, 'node_modules/c8/bin/c8.js')
function createMiniFixture(dir, options = {}) {
  const distDir = path.join(dir, 'dist')
  mkdirSync(distDir, { recursive: true })

  // dist/a.js
  writeFileSync(
    path.join(distDir, 'a.js'),
    `export function foo(x) {
  if (x > 0) {
    return 'positive'
  }
  return 'negative'
}
`,
  )

  // dist/b.js (unimported)
  if (options.bThrows) {
    writeFileSync(
      path.join(distDir, 'b.js'),
      `throw new Error('top-level explosion in b.js')
export function bar() { return 42 }
`,
    )
  } else {
    writeFileSync(
      path.join(distDir, 'b.js'),
      `export function bar() {
  return 42
}
`,
    )
  }

  // sub/a.js if duplicate names in different directories requested
  if (options.duplicateName) {
    const subDir = path.join(distDir, 'sub')
    mkdirSync(subDir, { recursive: true })
    writeFileSync(
      path.join(subDir, 'a.js'),
      `export function subFoo() {
  return 'sub'
}
`,
    )
  }

  // mini package.json
  writeFileSync(
    path.join(dir, 'package.json'),
    JSON.stringify({ name: 'mini-fixture', type: 'module' }),
  )

  // fake build receipt in .fable-build/build-manifest.json
  const fableBuildDir = path.join(dir, '.fable-build')
  mkdirSync(fableBuildDir, { recursive: true })

  function hashFile(file) {
    const buf = readFileSync(file)
    const st = statSync(file)
    return [
      crypto.createHash('sha256').update(buf).digest('hex'),
      st.size,
      st.mtimeMs,
    ]
  }

  const outputs = {}
  outputs['a.js'] = hashFile(path.join(distDir, 'a.js'))
  outputs['b.js'] = hashFile(path.join(distDir, 'b.js'))
  if (options.duplicateName) {
    outputs['sub/a.js'] = hashFile(path.join(distDir, 'sub/a.js'))
  }

  const manifest = {
    schema: 'build-manifest-v1',
    generation: 1,
    outputDir: 'dist',
    outputs,
    compiler: { inputDigest: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855' },
    generated: { inputDigest: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855' },
    artifacts: { inputDigest: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855' },
  }
  writeFileSync(path.join(fableBuildDir, 'build-manifest.json'), JSON.stringify(manifest))

  // test script
  const testScript = path.join(dir, 'test.js')
  if (options.testScriptContent) {
    writeFileSync(testScript, options.testScriptContent)
  } else {
    writeFileSync(
      testScript,
      `import assert from 'node:assert/strict'
import { foo } from './dist/a.js'
assert.equal(foo(1), 'positive')
`,
    )
  }

  const assertBuildFreshFn = ({ root }) => {
    const mPath = path.join(root, '.fable-build/build-manifest.json')
    if (!existsSync(mPath)) throw new Error('Manifest missing')
    const m = JSON.parse(readFileSync(mPath, 'utf8'))
    // check outputs
    for (const [k, [expectedSha]] of Object.entries(m.outputs)) {
      const p = path.join(root, m.outputDir, k)
      if (!existsSync(p)) throw new Error(`Missing output: ${k}`)
      const actualSha = crypto.createHash('sha256').update(readFileSync(p)).digest('hex')
      if (actualSha !== expectedSha) throw new Error(`Hash mismatch: ${k}`)
    }
    return { generation: m.generation }
  }

  const collectInputsFn = () => {
    // Hash all files in dist for the fixture
    const distFiles = existsSync(distDir) ? walk(distDir, ['.js']) : []
    let hash = crypto.createHash('sha256')
    for (const f of distFiles.sort()) {
      hash.update(f).update(readFileSync(f))
    }
    const digest = hash.digest('hex')
    return { compiler: digest, generated: digest, artifact: digest }
  }

  return { distDir, testScript, assertBuildFreshFn, collectInputsFn }
}

test('WHAT[verification-system-011] 1. 一份被测文件、一份未导入文件: 两者都在分母，未导入文件为零', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-1-'))
  try {
    const { testScript, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir)
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, true, `expected coverage ok, got ${result.code}`)
    const reportJson = JSON.parse(
      readFileSync(path.join(result.reportDir, 'coverage-final.json'), 'utf8'),
    )
    const files = Object.keys(reportJson)
    const aFile = files.find((f) => f.endsWith('a.js'))
    const bFile = files.find((f) => f.endsWith('b.js'))

    assert.ok(aFile, 'a.js must be reported')
    assert.ok(bFile, 'b.js must be reported')

    // b.js was not imported: statements must all be 0
    const bCov = reportJson[bFile]
    const bExecutedStatements = Object.values(bCov.s).filter((c) => c > 0)
    assert.equal(bExecutedStatements.length, 0, 'b.js statements must all be 0')

    // a.js was imported and run
    const aCov = reportJson[aFile]
    const aExecutedStatements = Object.values(aCov.s).filter((c) => c > 0)
    assert.ok(aExecutedStatements.length > 0, 'a.js must have executed statements')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 2. 未导入文件顶层抛错: 不执行该顶层代码，仍作为未覆盖文件出现', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-2-'))
  try {
    const { testScript, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir, { bThrows: true })
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, true, `coverage must succeed without pre-import crashing: ${result.code}`)
    const reportJson = JSON.parse(
      readFileSync(path.join(result.reportDir, 'coverage-final.json'), 'utf8'),
    )
    const files = Object.keys(reportJson)
    const bFile = files.find((f) => f.endsWith('b.js'))
    assert.ok(bFile, 'b.js must appear in coverage report even if its top-level throws')

    const bCov = reportJson[bFile]
    const bExecutedStatements = Object.values(bCov.s).filter((c) => c > 0)
    assert.equal(bExecutedStatements.length, 0, 'b.js must have 0 executed statements')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 3. 同名文件分处两个目录: 报表两条独立记录', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-3-'))
  try {
    const { testScript, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir, { duplicateName: true })
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, true, `expected coverage ok, got ${result.code}`)
    const reportJson = JSON.parse(
      readFileSync(path.join(result.reportDir, 'coverage-final.json'), 'utf8'),
    )
    const files = Object.keys(reportJson)
    const rootA = files.find((f) => f.endsWith('/dist/a.js') || f.endsWith('\\dist\\a.js'))
    const subA = files.find((f) => f.endsWith('/sub/a.js') || f.endsWith('\\sub\\a.js'))

    assert.ok(rootA, 'dist/a.js must be in report')
    assert.ok(subA, 'dist/sub/a.js must be in report')
    assert.notEqual(rootA, subA, 'they must be two distinct entries')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 4. 主测试与一个正常完成的子进程覆盖不同分支: 合并后的同一文件包含两条实际路径', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-4-'))
  try {
    // Parent exercises foo(1) ('positive'), child exercises foo(-1) ('negative')
    const childScript = path.join(dir, 'child.js')
    writeFileSync(
      childScript,
      `import assert from 'node:assert/strict'
import { foo } from './dist/a.js'
assert.equal(foo(-1), 'negative')
`,
    )

    const parentScript = `import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { foo } from './dist/a.js'

assert.equal(foo(1), 'positive')

// spawn child inheriting env (including NODE_V8_COVERAGE)
const child = spawnSync(process.execPath, [path.resolve('child.js')], {
  stdio: 'inherit',
  env: process.env,
})
assert.equal(child.status, 0)
`
    const { testScript, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir, { testScriptContent: parentScript })
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, true, `expected coverage ok: ${result.code}`)
    const reportJson = JSON.parse(
      readFileSync(path.join(result.reportDir, 'coverage-final.json'), 'utf8'),
    )
    const aFile = Object.keys(reportJson).find((f) => f.endsWith('a.js'))
    assert.ok(aFile)

    const aCov = reportJson[aFile]
    // Both return 'positive' and return 'negative' branches were executed across parent + child!
    for (const [branchId, branchCounts] of Object.entries(aCov.b)) {
      for (const count of branchCounts) {
        assert.ok(count > 0, `branch ${branchId} was executed across merged parent + child`)
      }
    }
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 5. 测试断言失败: 命令失败，即使已写出报告', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-5-'))
  try {
    const failingScript = `import assert from 'node:assert/strict'
import { foo } from './dist/a.js'
assert.equal(foo(1), 'wrong-expectation')
`
    const { testScript, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir, { testScriptContent: failingScript })
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, false, 'coverage must report failure when tests fail')
    assert.equal(result.code, 'TESTS_FAILED')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 6. 内层 runner 在测试启动前崩溃: 明确 infrastructure error，不接受全零报告当成功', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-6-'))
  try {
    const nonExistentScript = path.join(dir, 'does-not-exist.js')
    const { assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir)
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: nonExistentScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, false, 'coverage must report failure on runner error')
    assert.ok(
      result.code === 'RAW_COVERAGE_MISSING' || result.code === 'COVERAGE_RUNNER_ERROR' || result.code === 'TESTS_FAILED',
      `code should be infrastructure error, got ${result.code}`,
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 7. raw 数据为空、JSON 损坏或错误 run-id: 命令失败，不读取历史数据兜底', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-7-'))
  try {
    // Runner that exits immediately without touching V8 coverage
    const noopScript = `process.exit(0)`
    const { testScript, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir, { testScriptContent: noopScript })

    // Create an artificial corrupt run directory
    const coverageDir = path.join(dir, '.fable-build/coverage')
    const runId = 'corrupt-run-id'
    const rawDir = path.join(coverageDir, runId, 'raw')
    mkdirSync(rawDir, { recursive: true })
    writeFileSync(path.join(rawDir, 'coverage-corrupt.json'), 'CORRUPTED JSON NOT PARSEABLE')

    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      coverageDir,
      runId,
      silent: true,
    })

    assert.equal(result.ok, false, 'corrupt raw coverage data must fail the command')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 8. 分母与 build receipt 对等 (source map / dist mismatch)', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-8-'))
  try {
    const { testScript, distDir, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir)
    // Introduce extra production file in dist after initial receipt
    writeFileSync(path.join(distDir, 'extra.js'), 'export const extra = 1\n')

    // With denominator check, c8 reports extra.js but manifest didn't expect it or vice-versa
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, false, 'manifest mismatch / denominator drift must fail closed')
    assert.ok(
      result.code === 'BUILD_NOT_FRESH' || result.code === 'DENOMINATOR_MISMATCH' || result.code === 'RECEIPT_DENOMINATOR_MISMATCH',
      `expected freshness/denominator failure, got ${result.code}`,
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[verification-system-011] 9. 测试运行期间生产文件被改写: INPUT_CHANGED，不授予本次报告当前性', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cov-test-9-'))
  try {
    // Script modifies a production file during its test execution
    const modifyingScript = `import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { foo } from './dist/a.js'

assert.equal(foo(1), 'positive')

// Maliciously / concurrently modify production file during run
fs.appendFileSync(path.resolve('dist/a.js'), '// concurrently modified\\n')
`
    const { testScript, assertBuildFreshFn, collectInputsFn } = createMiniFixture(dir, { testScriptContent: modifyingScript })
    const result = await runCoverage({
      root: dir,
      c8Bin: C8_BIN,
      unitRunnerScript: testScript,
      assertBuildFreshFn,
      collectInputsFn,
      silent: true,
    })

    assert.equal(result.ok, false, 'mutation of production files during run must fail with INPUT_CHANGED')
    assert.equal(result.code, 'INPUT_CHANGED')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
}
