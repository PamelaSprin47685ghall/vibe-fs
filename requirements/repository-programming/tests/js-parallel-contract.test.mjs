// requirements/repository-programming/tests/js-parallel-contract.test.mjs
//
// JS-018: parallel js-* calls are safe because every transaction re-snapshots
// the committed state. This test pins the two unit-testable halves of that
// contract:
//   1. the generated surface teaches the model-side parallel-safety rule
//      (contract-parallel-edits / contract-parallel-reads resources), and
//   2. two consecutive transactions on the same path stack deterministically:
//      the second observes the first's committed state — the re-snapshot
//      property that makes Host-side serialization free of lost updates.
// The Host's own deterministic per-message serial execution is proven at the
// plugin boundary (tests/integration/plugin/), not here.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { JsToolWorkflow_run as workflowRun, JsToolsResult_render as render } from '../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js'
import { JsToolGenerator_generate as generate } from '../../../dist/Domain/JsSurface.js'
import { JsDescriptionAssets_load as loadJsProse } from '../../../dist/Infrastructure/OpenCode/Tools/JsToolHost.js'
import { ProviderLanguage } from '../../../dist/Domain/ProviderLanguage.js'
import { ToolPermission } from '../../../dist/Kernel/Roles.js'
import { FsSet, caseOf, listItems, payloadOf } from '../../../tests/unit/support/domain.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-parallel-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const permissionComparer = { Compare: (a, b) => a.CompareTo(b) }
const coderCaps = FsSet.ofArray(
  [ToolPermission.Read, ToolPermission.Write, ToolPermission.Edit, ToolPermission.Glob, ToolPermission.Grep],
  permissionComparer,
)

const runWorkflow = async (dir, program) => {
  const surface = generate('Coder', coderCaps, loadJsProse(ProviderLanguage.English))
  const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
  return { outcome, surface }
}

// JsToolOutcome is a union: Succeeded(value, rewritten, created) | Failed failure.
const succeeded = (outcome) => caseOf(outcome) === 'Succeeded'
const failureText = (outcome) => String(payloadOf(outcome)[0])

test('JS018_generated_surface_teaches_parallel_safety_for_edits_and_reads', () => {
  const surface = generate('Coder', coderCaps, loadJsProse(ProviderLanguage.English))
  assert.equal(
    surface.Description.includes('Parallel js-coder calls are absolutely safe for same-file and cross-file edits'),
    true,
    'edit-parallel contract must be in the description',
  )
  assert.equal(
    surface.Description.includes(
      'Parallel reads, parallel edits, same-file\nand cross-file calls are all absolutely safe',
    ),
    true,
    'read/edit parallel contract must be in the description',
  )
  assert.equal(
    surface.Description.includes('The Host serializes one assistant'),
    true,
    'the description must state the Host-side deterministic serialization',
  )
})

test('JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'step0', 'utf8')

    // Transaction 1: prepend.
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

    // Transaction 2 (a "parallel" sibling): appends onto the state the first
    // transaction committed. A stale read would clobber step1 — the contract
    // says the second transaction re-snapshots and stacks.
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

test('JS018_interleaved_reads_are_immutable_snapshots_not_mutation_aliases', async () => {
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
    // The view captured before the mutation stays the original text even
    // though the same program later rewrites the file (snapshot isolation).
    assert.match(rendered, /before = "original"/)
    assert.match(rendered, /after = "original"/)
  } finally {
    cleanup()
  }
})
