/**
 * M0 ratchet contract (RED): per-file DSL ownership ratchet gate.
 * Pins the CLI contract of scripts/checks/dsl-ownership-ratchet.mjs BEFORE it exists:
 *
 *   node scripts/checks/dsl-ownership-ratchet.mjs --baseline=<json> --root=<dir>
 *
 * Baseline is a JSON object { "<rel-path>": { "<gate>": <count> } }; rel-path is
 * POSIX-normalized and relative to --root. A file/gate whose actual count exceeds its
 * baseline (0 when absent) prints "<file> <gate> <old> -> <new>" and exits non-zero.
 * A drop below baseline exits zero. Synthetic fixtures only — never production trees.
 *
 * M2 exemption: Application/** paths ending in Interpreter.fs raw-task is not counted
 * (side-effect owners); every other gate in those files, and every gate elsewhere,
 * still counts.
 */
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { scanText } from '../../../scripts/checks/dsl-ownership.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname
const RATCHET_SCRIPT = join(ROOT, 'scripts/checks/dsl-ownership-ratchet.mjs')

const norm = (p) => p.replace(/\\/g, '/')

/** Create an isolated tmp fixture root: write(rel, text) + baseline(obj). */
const makeFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'dsl-ratchet-'))
  return {
    dir,
    write: (rel, text) => {
      const file = join(dir, rel)
      mkdirSync(dirname(file), { recursive: true })
      writeFileSync(file, text)
    },
    baseline: (obj) => {
      const file = join(dir, 'baseline.json')
      writeFileSync(file, JSON.stringify(obj))
      return file
    },
    dispose: () => rmSync(dir, { recursive: true, force: true }),
  }
}

/** Spawn the ratchet script; resolve { code, stdout, stderr } on close. */
const runRatchet = (baselinePath, root, cwd) =>
  new Promise((resolve, reject) => {
    const child = spawn(
      process.execPath,
      [RATCHET_SCRIPT, `--baseline=${baselinePath}`, `--root=${root}`],
      { cwd },
    )
    let stdout = ''
    let stderr = ''
    child.stdout.on('data', (chunk) => {
      stdout += chunk
    })
    child.stderr.on('data', (chunk) => {
      stderr += chunk
    })
    child.on('error', reject)
    child.on('close', (code) => resolve({ code, stdout, stderr }))
  })

const output = ({ stdout, stderr }) => norm(stdout + stderr)

test('DSL_OWNERSHIP_RATCHET_above_baseline_exits_nonzero_with_hint', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Agent/Foo.fs'
  const source = ['module Foo', 'let run () = task { return 1 }', 'let other () = task { return 2 }'].join('\n')
  fx.write(file, source)

  // Fixture self-check: exactly 2 raw-task hits, so failure cannot be a miswritten fixture.
  const hits = scanText(source, file).filter((v) => v.gate === 'raw-task')
  assert.equal(hits.length, 2)

  const baseline = fx.baseline({ [file]: { 'raw-task': 1 } })
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  const out = output(result)
  assert.notEqual(result.code, 0, `expected non-zero exit, got ${result.code}: ${out}`)
  assert.ok(out.includes(`${file} raw-task 1 -> 2`), `expected hint in output, got: ${out}`)
})

test('DSL_OWNERSHIP_RATCHET_unlisted_file_with_violation_exits_nonzero', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Application/New.fs'
  const source = ['module New', 'let mutable counter = 0'].join('\n')
  fx.write(file, source)

  // Fixture self-check: exactly 1 mutable hit.
  const hits = scanText(source, file).filter((v) => v.gate === 'mutable')
  assert.equal(hits.length, 1)

  // Baseline does not mention Application/New.fs: any violation there is a new violation.
  const baseline = fx.baseline({})
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  const out = output(result)
  assert.notEqual(result.code, 0, `expected non-zero exit, got ${result.code}: ${out}`)
  assert.ok(out.includes(`${file} mutable`), `expected unlisted file to be reported, got: ${out}`)
})

test('DSL_OWNERSHIP_RATCHET_drop_below_baseline_exits_zero', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Domain/Bar.fs'
  const source = ['module Bar', 'type Flags = { Dirty: bool }'].join('\n')
  fx.write(file, source)

  // Fixture self-check: exactly 1 program-counter hit (Dirty fires only that gate).
  const hits = scanText(source, file).filter((v) => v.gate === 'program-counter')
  assert.equal(hits.length, 1)

  // Actual 1 <= baseline 3: regression is allowed, gate stays green.
  const baseline = fx.baseline({ [file]: { 'program-counter': 3 } })
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  assert.equal(result.code, 0, `expected exit 0, got ${result.code}: ${output(result)}`)
})

test('DSL_OWNERSHIP_RATCHET_ignores_non_program_dirs', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Infrastructure/Foo.fs'
  const source = ['module Foo', 'let run () = task { return 1 }'].join('\n')
  fx.write(file, source)

  // Fixture self-check: the violation exists; only the scan scope excludes it.
  const hits = scanText(source, file).filter((v) => v.gate === 'raw-task')
  assert.equal(hits.length, 1)

  // Infrastructure/ is outside PROGRAM_DIRS: never scanned, never reported.
  const baseline = fx.baseline({})
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  const out = output(result)
  assert.equal(result.code, 0, `expected exit 0, got ${result.code}: ${out}`)
  assert.ok(!out.includes('Infrastructure'), `expected no Infrastructure output, got: ${out}`)
})

test('DSL_OWNERSHIP_RATCHET_application_interpreter_raw_task_is_exempt', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Application/DummyInterpreter.fs'
  const source = ['module DummyInterpreter', 'let run () = task { return 1 }'].join('\n')
  fx.write(file, source)

  // Fixture self-check: the raw-task violation exists in the text; only the
  // Application/**/*Interpreter.fs exemption may drop it from the count.
  const hits = scanText(source, file).filter((v) => v.gate === 'raw-task')
  assert.equal(hits.length, 1)

  // Interpreter files own their side effects: raw-task is exempt, exit 0.
  const baseline = fx.baseline({})
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  const out = output(result)
  assert.equal(result.code, 0, `expected exit 0, got ${result.code}: ${out}`)
  assert.ok(!out.includes('DummyInterpreter'), `expected no DummyInterpreter output, got: ${out}`)
})

test('DSL_OWNERSHIP_RATCHET_application_interpreter_other_gates_still_count', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Application/DummyInterpreter.fs'
  const source = ['module DummyInterpreter', 'let mutable x = 1'].join('\n')
  fx.write(file, source)

  // Fixture self-check: exactly 1 mutable hit.
  const hits = scanText(source, file).filter((v) => v.gate === 'mutable')
  assert.equal(hits.length, 1)

  // The exemption covers raw-task only: mutable still counts and must fail.
  const baseline = fx.baseline({})
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  const out = output(result)
  assert.notEqual(result.code, 0, `expected non-zero exit, got ${result.code}: ${out}`)
  assert.ok(
    out.includes('DummyInterpreter') && out.includes('mutable'),
    `expected DummyInterpreter mutable hint, got: ${out}`,
  )
})

test('DSL_OWNERSHIP_RATCHET_non_application_interpreter_raw_task_regression', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Domain/NotAnInterpreter.fs'
  const source = ['module NotAnInterpreter', 'let run () = task { return 1 }'].join('\n')
  fx.write(file, source)

  // Fixture self-check: exactly 1 raw-task hit.
  const hits = scanText(source, file).filter((v) => v.gate === 'raw-task')
  assert.equal(hits.length, 1)

  // Exemption is Application/**/*Interpreter.fs only: Domain files still fail.
  const baseline = fx.baseline({})
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  const out = output(result)
  assert.notEqual(result.code, 0, `expected non-zero exit, got ${result.code}: ${out}`)
})

test('DSL_OWNERSHIP_RATCHET_application_non_interpreter_raw_task_regression', async (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)

  const file = 'Application/Helpers.fs'
  const source = ['module Helpers', 'let run () = task { return 1 }'].join('\n')
  fx.write(file, source)

  // Fixture self-check: exactly 1 raw-task hit.
  const hits = scanText(source, file).filter((v) => v.gate === 'raw-task')
  assert.equal(hits.length, 1)

  // File must end with Interpreter.fs to be exempt: Helpers.fs still fails.
  const baseline = fx.baseline({})
  const result = await runRatchet(baseline, fx.dir, fx.dir)
  const out = output(result)
  assert.notEqual(result.code, 0, `expected non-zero exit, got ${result.code}: ${out}`)
})
