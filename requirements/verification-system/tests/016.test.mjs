import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { collectVerificationInputs, diffVerificationInputs } from '../../../scripts/lib/build-state.mjs'
import { verify, verificationSteps } from '../../../scripts/verify.mjs'

function setupFixtureRepo() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'verify-inputs-fixture-'))

  fs.mkdirSync(path.join(dir, 'src'), { recursive: true })
  fs.mkdirSync(path.join(dir, 'scripts'), { recursive: true })
  fs.mkdirSync(path.join(dir, 'requirements/p/tests'), { recursive: true })
  fs.mkdirSync(path.join(dir, 'resources'), { recursive: true })
  fs.mkdirSync(path.join(dir, '.github/workflows'), { recursive: true })

  fs.writeFileSync(path.join(dir, 'src/Foo.fs'), 'module Foo\nlet x = 1\n')
  fs.writeFileSync(path.join(dir, 'scripts/x.mjs'), 'console.log("x")\n')
  fs.writeFileSync(path.join(dir, 'requirements/p/tests/p.test.mjs'), '// test\n')
  fs.writeFileSync(path.join(dir, 'requirements/p/WHAT.md'), '# WHAT\n')
  fs.writeFileSync(path.join(dir, 'resources/r.txt'), 'resource\n')
  fs.writeFileSync(path.join(dir, '.github/workflows/ci.yml'), 'name: CI\n')
  fs.writeFileSync(path.join(dir, 'package.json'), '{"name":"fixture"}\n')
  fs.writeFileSync(path.join(dir, 'package-lock.json'), '{"lockfileVersion":3}\n')

  return dir
}

test('WHAT[VERIFICATION-SYSTEM-016] collectVerificationInputs collects expected relative paths and diff detects mutations', () => {
  const fixture = setupFixtureRepo()
  try {
    const initial = collectVerificationInputs(fixture)
    const paths = initial.map((e) => e.path).sort()
    const expected = [
      '.github/workflows/ci.yml',
      'package-lock.json',
      'package.json',
      'requirements/p/WHAT.md',
      'requirements/p/tests/p.test.mjs',
      'resources/r.txt',
      'scripts/x.mjs',
      'src/Foo.fs',
    ].sort()

    assert.deepEqual(paths, expected, 'collected input paths must match fixture exactly')

    const target = path.join(fixture, 'src/Foo.fs')
    const originalContent = fs.readFileSync(target, 'utf8')
    const statBefore = fs.statSync(target)
    const modifiedContent = originalContent.replace('1', '2')
    assert.equal(modifiedContent.length, originalContent.length)
    fs.writeFileSync(target, modifiedContent)
    fs.utimesSync(target, statBefore.atime, statBefore.mtime)

    const afterContentChange = collectVerificationInputs(fixture)
    const contentDiff = diffVerificationInputs(initial, afterContentChange)
    assert.equal(contentDiff.equal, false)
    assert.equal(contentDiff.reason, 'content-changed:src/Foo.fs')

    fs.writeFileSync(target, originalContent)

    const newFile = path.join(fixture, 'requirements/p/tests/new.test.mjs')
    fs.writeFileSync(newFile, '// new test\n')
    const afterAdd = collectVerificationInputs(fixture)
    const addDiff = diffVerificationInputs(initial, afterAdd)
    assert.equal(addDiff.equal, false)
    assert.equal(addDiff.reason, 'file-set-changed')
    fs.unlinkSync(newFile)

    const deletedFile = path.join(fixture, 'scripts/x.mjs')
    const deletedContent = fs.readFileSync(deletedFile, 'utf8')
    fs.unlinkSync(deletedFile)
    const afterDelete = collectVerificationInputs(fixture)
    const deleteDiff = diffVerificationInputs(initial, afterDelete)
    assert.equal(deleteDiff.equal, false)
    assert.equal(deleteDiff.reason, 'file-set-changed')
    fs.writeFileSync(deletedFile, deletedContent)

    const buildLog = path.join(fixture, '.fable-build/run.log')
    fs.mkdirSync(path.dirname(buildLog), { recursive: true })
    fs.writeFileSync(buildLog, 'log entry\n')
    const afterBuildLog = collectVerificationInputs(fixture)
    const ignoreDiff = diffVerificationInputs(initial, afterBuildLog)
    assert.equal(ignoreDiff.equal, true, '.fable-build writes must not affect input diff')

    const emptyFixture = fs.mkdtempSync(path.join(os.tmpdir(), 'verify-empty-'))
    try {
      assert.throws(
        () => collectVerificationInputs(emptyFixture),
        (err) => err.code === 'verification-inputs-root-missing',
      )
    } finally {
      fs.rmSync(emptyFixture, { recursive: true, force: true })
    }
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-016] verify detects mid-flight inputs change via runStep perturbation and halts with fail', async () => {
  const fixture = setupFixtureRepo()
  const tmpLogs = fs.mkdtempSync(path.join(os.tmpdir(), 'verify-step-logs-'))
  let buf = ''
  const sink = { write(chunk) { buf += chunk } }

  try {
    const mutatingRunStep = async ({ label }) => {
      if (label === 'unit') {
        fs.appendFileSync(path.join(fixture, 'src/Foo.fs'), '// mid-run mutation\n')
      }
      return { label, ok: true, exitCode: 0, signal: null, durationMs: 5 }
    }

    const result = await verify({
      root: fixture,
      release: false,
      runStep: mutatingRunStep,
      output: sink,
      logDirectory: tmpLogs,
    })

    assert.equal(result.exitCode, 1, 'verify must return exitCode 1 when inputs change')
    assert.equal(result.outcome, 'fail', 'outcome must be fail')
    assert.ok(result.inputChanges, 'inputChanges must be present')
    assert.equal(result.inputChanges.equal, false)
    assert.ok(result.inputChanges.reason.includes('src/Foo.fs'))

    const cleanLogs = fs.mkdtempSync(path.join(os.tmpdir(), 'verify-clean-logs-'))
    let cleanBuf = ''
    const cleanSink = { write(chunk) { cleanBuf += chunk } }
    try {
      fs.writeFileSync(path.join(fixture, 'src/Foo.fs'), 'module Foo\nlet x = 1\n')
      const cleanStep = async ({ label }) => ({ label, ok: true, exitCode: 0, signal: null, durationMs: 5 })
      const cleanResult = await verify({
        root: fixture,
        release: false,
        runStep: cleanStep,
        output: cleanSink,
        logDirectory: cleanLogs,
      })
      assert.equal(cleanResult.exitCode, 0, 'clean verify must succeed with 0')
      assert.equal(cleanResult.outcome, 'pass')
    } finally {
      rmSync(cleanLogs, { recursive: true, force: true })
    }
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
    fs.rmSync(tmpLogs, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-016] verify with isolated logDirectory creates run dir and latest link without touching repo root', async () => {
  const fixture = setupFixtureRepo()
  const isolatedLogs = fs.mkdtempSync(path.join(os.tmpdir(), 'verify-isolated-logs-'))
  let buf = ''
  const sink = { write(chunk) { buf += chunk } }

  try {
    const fakeStep = async ({ label }) => ({ label, ok: true, exitCode: 0, signal: null, durationMs: 1 })
    const result = await verify({
      root: fixture,
      release: false,
      runStep: fakeStep,
      output: sink,
      logDirectory: isolatedLogs,
    })

    assert.equal(result.exitCode, 0)
    const runDirs = fs.readdirSync(isolatedLogs).filter((name) => name !== 'latest')
    assert.ok(runDirs.length >= 1, 'must have at least one run directory in isolated logDirectory')
    const latestLink = path.join(isolatedLogs, 'latest')
    assert.ok(fs.existsSync(latestLink), 'latest link must exist in isolated logDirectory')
    assert.equal(fs.readlinkSync(latestLink), runDirs[0])

    assert.equal(fs.existsSync(path.join(fixture, '.fable-build')), false, 'fixture root must have no .fable-build')
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
    fs.rmSync(isolatedLogs, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-016] verificationSteps excludes TESTS_MJS_FILES from unit and integration step environments', () => {
  const hostEnvWithOverride = {
    PATH: process.env.PATH || '',
    TESTS_MJS_FILES: 'some-test-override.test.mjs',
    HOME: process.env.HOME || '',
  }
  const steps = verificationSteps({ root: '.', release: false, verbose: true, env: hostEnvWithOverride })
  const unitStep = steps.find((s) => s.label === 'unit')
  const integrationStep = steps.find((s) => s.label === 'integration')
  assert.ok(unitStep, 'unit step should be defined')
  assert.ok(integrationStep, 'integration step should be defined')
  assert.equal('TESTS_MJS_FILES' in unitStep.env, false, 'unit step env must not contain TESTS_MJS_FILES')
  assert.equal('TESTS_MJS_FILES' in integrationStep.env, false, 'integration step env must not contain TESTS_MJS_FILES')
  assert.equal(unitStep.env.NODE_TEST_VERBOSE, '1')
  assert.equal(unitStep.env.WXS_E2E_QUIET, '1')
})
