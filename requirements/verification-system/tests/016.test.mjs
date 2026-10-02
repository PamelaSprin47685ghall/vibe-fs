// Verification inputs collection and mid-run perturbation detection tests.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'

test.todo('WHAT[verification-system-016] the actual verification run binds its evidence to the same immutable candidate snapshot')
import { collectGeneratedInputs, collectVerificationInputs, computeDigest, diffVerificationInputs } from '../../../scripts/lib/build-state.mjs'
import { verify } from '../../../scripts/verify.mjs'

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
  execFileSync('git', ['init', '--quiet', dir])

  return dir
}

test('WHAT[verification-system-016] tracked corpus proposals contribute their actual content to verification inputs', () => {
  const fixture = setupFixtureRepo()
  try {
    fs.mkdirSync(path.join(fixture, 'proposals'))
    const proposal = path.join(fixture, 'proposals', 'decision.md')
    fs.writeFileSync(proposal, '# Before\n')
    execFileSync('git', ['-C', fixture, 'add', 'proposals/decision.md'])

    const before = collectVerificationInputs(fixture)
    assert.ok(before.some(entry => entry.path === 'proposals/decision.md'))
    assert.ok(collectGeneratedInputs(fixture).some(entry => entry.path === 'proposals/decision.md'))
    fs.writeFileSync(proposal, '# Changed\n')
    const after = collectVerificationInputs(fixture)

    assert.deepEqual(diffVerificationInputs(before, after), {
      equal: false,
      reason: 'content-changed:proposals/decision.md',
    })
    assert.notEqual(computeDigest(before), computeDigest(after))
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
  }
})

for (const initiallyTracked of [false, true]) {
  test(`WHAT[verification-system-016] corpus tracking ${initiallyTracked ? 'removal' : 'addition'} changes verification identity without changing file bytes`, () => {
    const fixture = setupFixtureRepo()
    try {
      if (initiallyTracked) execFileSync('git', ['-C', fixture, 'add', 'src/Foo.fs'])
      const original = fs.readFileSync(path.join(fixture, 'src/Foo.fs'))
      const before = collectVerificationInputs(fixture)
      const generatedBefore = collectGeneratedInputs(fixture)

      execFileSync('git', ['-C', fixture, ...(initiallyTracked ? ['rm', '--cached', 'src/Foo.fs'] : ['add', 'src/Foo.fs'])])
      const after = collectVerificationInputs(fixture)
      const generatedAfter = collectGeneratedInputs(fixture)

      assert.deepEqual(fs.readFileSync(path.join(fixture, 'src/Foo.fs')), original)
      assert.deepEqual(before.map(entry => entry.path), after.map(entry => entry.path))
      assert.notDeepEqual(generatedBefore.map(entry => entry.path), generatedAfter.map(entry => entry.path))
      assert.equal(diffVerificationInputs(before, after).equal, false)
      assert.notEqual(computeDigest(before), computeDigest(after))
    } finally {
      fs.rmSync(fixture, { recursive: true, force: true })
    }
  })
}

for (const collect of [collectGeneratedInputs, collectVerificationInputs]) {
  test(`WHAT[verification-system-016] ${collect.name} rejects failed Git corpus inventory`, () => {
    const fixture = setupFixtureRepo()
    try {
      fs.writeFileSync(path.join(fixture, '.git', 'index'), 'invalid index')
      assert.throws(() => collect(fixture), error => {
        assert.equal(error.status, 128)
        assert.match(String(error.stderr), /index/)
        return true
      })
    } finally {
      fs.rmSync(fixture, { recursive: true, force: true })
    }
  })
}

test('WHAT[verification-system-016] failed Git corpus inventory stops verification before any stage starts', async () => {
  const fixture = setupFixtureRepo()
  const stages = []
  try {
    fs.writeFileSync(path.join(fixture, '.git', 'index'), 'invalid index')
    const result = await verify({
      root: fixture,
      output: { write() {} },
      runStep: async ({ label }) => {
        stages.push(label)
        return { label, ok: true, exitCode: 0 }
      },
    })
    assert.equal(result.exitCode, 1)
    assert.equal(result.outcome, 'fail')
    assert.match(result.failureReason, /^input-collection-failed:.*git/s)
    assert.deepEqual(stages, [])
    assert.ok(result.steps.every(step => step.status === 'not-run'))
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-016] collectVerificationInputs collects expected relative paths and diff detects mutations', () => {
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

    // Same size + mtime content change
    const target = path.join(fixture, 'src/Foo.fs')
    const originalContent = fs.readFileSync(target, 'utf8')
    const statBefore = fs.statSync(target)
    // Replace "1" with "2" keeping length same
    const modifiedContent = originalContent.replace('1', '2')
    assert.equal(modifiedContent.length, originalContent.length)
    fs.writeFileSync(target, modifiedContent)
    fs.utimesSync(target, statBefore.atime, statBefore.mtime)

    const afterContentChange = collectVerificationInputs(fixture)
    const contentDiff = diffVerificationInputs(initial, afterContentChange)
    assert.equal(contentDiff.equal, false)
    assert.equal(contentDiff.reason, 'content-changed:src/Foo.fs')

    // Revert target content
    fs.writeFileSync(target, originalContent)

    // Add new file
    const newFile = path.join(fixture, 'requirements/p/tests/new.test.mjs')
    fs.writeFileSync(newFile, '// new test\n')
    const afterAdd = collectVerificationInputs(fixture)
    const addDiff = diffVerificationInputs(initial, afterAdd)
    assert.equal(addDiff.equal, false)
    assert.equal(addDiff.reason, 'file-set-changed')
    fs.unlinkSync(newFile)

    // Delete existing file
    const deletedFile = path.join(fixture, 'scripts/x.mjs')
    const deletedContent = fs.readFileSync(deletedFile, 'utf8')
    fs.unlinkSync(deletedFile)
    const afterDelete = collectVerificationInputs(fixture)
    const deleteDiff = diffVerificationInputs(initial, afterDelete)
    assert.equal(deleteDiff.equal, false)
    assert.equal(deleteDiff.reason, 'file-set-changed')
    fs.writeFileSync(deletedFile, deletedContent)

    // Writing to .fable-build directory is ignored
    const buildLog = path.join(fixture, '.fable-build/run.log')
    fs.mkdirSync(path.dirname(buildLog), { recursive: true })
    fs.writeFileSync(buildLog, 'log entry\n')
    const afterBuildLog = collectVerificationInputs(fixture)
    const ignoreDiff = diffVerificationInputs(initial, afterBuildLog)
    assert.equal(ignoreDiff.equal, true, '.fable-build writes must not affect input diff')

    // Missing required root directory throws verification-inputs-root-missing
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

test('WHAT[verification-system-016] verify detects mid-flight inputs change via runStep perturbation and halts with fail', async () => {
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
    const integrationStep = result.steps.find((s) => s.label === 'integration')
    assert.equal(integrationStep?.status, 'not-run', 'subsequent integration step must not run after unit mutation')

    // Control group: clean run without mutations
    const cleanLogs = fs.mkdtempSync(path.join(os.tmpdir(), 'verify-clean-logs-'))
    let cleanBuf = ''
    const cleanSink = { write(chunk) { cleanBuf += chunk } }
    try {
      // Restore Foo.fs
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
      fs.rmSync(cleanLogs, { recursive: true, force: true })
    }
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
    fs.rmSync(tmpLogs, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-016] verify with isolated logDirectory creates run dir and latest link without touching repo root', async () => {
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

    // Verify fixture root has no .fable-build directory created
    assert.equal(fs.existsSync(path.join(fixture, '.fable-build')), false, 'fixture root must have no .fable-build')
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
    fs.rmSync(isolatedLogs, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-016] a detected step-boundary mutation prevents later steps', async () => {
  const fixture = setupFixtureRepo()
  const tmpLogs = fs.mkdtempSync(path.join(os.tmpdir(), 'verify-step-interrupt-logs-'))
  let buf = ''
  const sink = { write(chunk) { buf += chunk } }
  const executedSteps = []

  try {
    const mutatingRunStep = async ({ label }) => {
      executedSteps.push(label)
      if (label === 'check') {
        fs.writeFileSync(path.join(fixture, 'src/Foo.fs'), 'module Foo\nlet x = 999\n')
      }
      return { label, ok: true, exitCode: 0, signal: null, durationMs: 2 }
    }

    const result = await verify({
      root: fixture,
      release: false,
      runStep: mutatingRunStep,
      output: sink,
      logDirectory: tmpLogs,
    })

    assert.equal(result.exitCode, 1, 'must exit with non-zero status')
    assert.equal(result.outcome, 'fail', 'outcome must be fail')
    assert.ok(result.inputChanges, 'inputChanges must be recorded')
    assert.equal(result.inputChanges.equal, false)

    // Planned steps for daily: format:check, check, build, unit, integration
    // When check mutates input, subsequent steps build, unit, integration must NOT be executed!
    assert.deepEqual(executedSteps, ['format:check', 'check'], 'steps after check must not be executed')

    const stepStatuses = Object.fromEntries(result.steps.map((s) => [s.label, s.status]))
    assert.equal(stepStatuses['format:check'], 'ok')
    assert.equal(stepStatuses['check'], 'ok')
    assert.equal(stepStatuses['build'], 'not-run', 'subsequent step build must be not-run')
    assert.equal(stepStatuses['unit'], 'not-run', 'subsequent step unit must be not-run')
    assert.equal(stepStatuses['integration'], 'not-run', 'subsequent step integration must be not-run')
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
    fs.rmSync(tmpLogs, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-016] a mutation restored within one step still invalidates the run', {
  todo: 'D2: fixed isolated inputs selected; current runner still uses mutable inputs and step-boundary hashes miss this counterexample',
}, async () => {
  const fixture = setupFixtureRepo()
  try {
    const target = path.join(fixture, 'src/Foo.fs')
    const original = fs.readFileSync(target)
    const result = await verify({
      root: fixture,
      output: { write() {} },
      runStep: async ({ label }) => {
        if (label === 'check') {
          fs.writeFileSync(target, 'module Foo\nlet x = 999\n')
          fs.writeFileSync(target, original)
        }
        return { label, ok: true, exitCode: 0, signal: null, durationMs: 1 }
      },
    })
    assert.equal(result.exitCode, 1, 'changing and restoring an input is not a stable verification run')
    assert.equal(result.outcome, 'fail')
  } finally {
    fs.rmSync(fixture, { recursive: true, force: true })
  }
})
