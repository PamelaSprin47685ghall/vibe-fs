// JS-018: parallel js-* calls are safe because every transaction
// re-snapshots committed state. The generated description carries the model
// contract; consecutive workflow transactions pin the host invariant.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { run, caseName, failureCode, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-parallel-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const runWorkflow = async (dir, program) => ({
  outcome: await run(dir, 'Coder', 'en', program, 2000, Date.now() + 60_000, 1 << 20, null),
  surface: generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en'),
})

const succeeded = (outcome) => caseName(outcome) === 'Succeeded'
const failureText = (outcome) => String(failureCode(outcome) ?? 'workflow failed')

test('WHAT[REPOSITORY-PROGRAMMING-017] JS018_generated_surface_teaches_parallel_safety_for_edits_and_reads', () => {
  const surface = generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')
  assert.equal(
    surface.description.includes('Parallel js-coder calls are absolutely safe for same-file and cross-file edits'),
    true,
    'edit-parallel contract must be in the description',
  )
  assert.equal(
    surface.description.includes(
      'Parallel reads, parallel edits, same-file\nand cross-file calls are all absolutely safe',
    ),
    true,
    'read/edit parallel contract must be in the description',
  )
  assert.equal(
    surface.description.includes('The Host serializes one assistant'),
    true,
    'the description must state the Host-side deterministic serialization',
  )
})

test('WHAT[REPOSITORY-PROGRAMMING-017] JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'step0', 'utf8')
    const first = await runWorkflow(
      dir,
      `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt');
    this.rewrite('a.txt', 'step1:' + view.text());
    return { after: view.text() };
  }
}`,
    )
    assert.equal(succeeded(first.outcome), true, failureText(first.outcome))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'step1:step0')

    const second = await runWorkflow(
      dir,
      `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt');
    this.rewrite('a.txt', view.text() + ':step2');
    return { before: view.text() };
  }
}`,
    )
    assert.equal(succeeded(second.outcome), true, failureText(second.outcome))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'step1:step0:step2')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-017] JS018_interleaved_reads_are_immutable_snapshots_not_mutation_aliases', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'original', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt');
    const before = view.text();
    this.rewrite('a.txt', 'mutated');
    return { before, after: view.text() };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(succeeded(outcome), true, failureText(outcome))
    const rendered = render(outcome)
    assert.match(rendered, /before = "original"/)
    assert.match(rendered, /after = "original"/)
  } finally {
    cleanup()
  }
})
