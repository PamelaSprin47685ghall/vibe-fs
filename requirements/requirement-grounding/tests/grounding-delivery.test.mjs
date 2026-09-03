import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as grounding from '../../../dist/Requirement/Grounding/Surface.js'
import * as host from '../../../dist/OpenCode/Host/RequirementGroundingSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-delivery-'))
  mkdirSync(join(dir, 'requirements', 'alpha', 'tests'), { recursive: true })
  mkdirSync(join(dir, 'src'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHY.md'), 'why\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'what-v1\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'HOW.md'), 'how\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'tests', 'z.test.mjs'), 'z\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'tests', 'a.test.mjs'), 'a\n', 'utf8')
  writeFileSync(join(dir, 'src', 'main.fs'), 'source\n', 'utf8')
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const terminalRead = (path) => [{
  info: { id: 'r1', role: 'assistant', providerID: 'anthropic' },
  parts: [{ type: 'tool', tool: 'read', callID: 'source-read', state: { status: 'completed', input: { filePath: path }, output: 'source\n', time: { start: 0, end: 0 } } }],
}]

test('WHAT[REQUIREMENT-GROUNDING-005] APPLIES-TO external grounding injects only direct Markdown and excludes tests plus the manifest', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'requirements', 'alpha', 'PROOF.md'), 'proof\n', 'utf8')
    writeFileSync(join(dir, 'requirements', 'alpha', 'notes.txt'), 'not guidance\n', 'utf8')

    const opened = await host.createJournal(dir)
    assert.equal(opened.ok, true)
    const source = join(dir, 'src', 'main.fs')
    const requested = await host.requestPaths(opened.journal, dir, 's-material', [source])
    assert.equal(requested.needsGrounding, true)
    const projected = await host.projectWithJournal(opened.journal, 's-material', terminalRead(source))
    assert.equal(projected.ok, true)

    const terminalOutput = projected.value.at(-1).parts[0].state.output
    for (const file of ['HOW.md', 'PROOF.md', 'WHAT.md', 'WHY.md']) {
      assert.ok(terminalOutput.includes(`requirement_source_path = "requirements/alpha/${file}"`), `must contain ${file}`)
    }
    assert.equal(terminalOutput.includes('notes.txt'), false)
    assert.equal(terminalOutput.includes('/tests/'), false)
    assert.equal(terminalOutput.includes('/APPLIES-TO'), false)
    assert.equal(projected.value.some((m) => m.info?.source === host.source), false)
    host.disposeJournal(opened.journal)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-006] direct Markdown read counts as visible grounding material and only unread siblings are injected', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const snapshot = grounding.materializePackage(dir, 'alpha')
    assert.deepEqual(snapshot.materials.map((material) => material.path), [
      'requirements/alpha/HOW.md',
      'requirements/alpha/WHAT.md',
      'requirements/alpha/WHY.md',
    ])
    assert.equal(snapshot.materials.some((m) => m.path.includes('/tests/')), false)
    assert.equal(snapshot.materials.some((m) => m.path.endsWith('/APPLIES-TO')), false)

    // Reading WHAT.md is itself grounding. Auto-grounding must not echo WHAT.md
    // back at the model; only the unread sibling materials remain.
    const opened = await host.createJournal(dir)
    const selfPath = join(dir, 'requirements', 'alpha', 'WHAT.md')
    const requested = await host.observationDecision(
      opened.journal,
      dir,
      's-self-material',
      'read',
      { filePath: selfPath },
      'what-v1\n',
    )
    assert.equal(requested.needsGrounding, true)
    const projected = await host.projectWithJournal(opened.journal, 's-self-material', terminalRead(selfPath))
    assert.equal(projected.ok, true)

    const terminalOutput = projected.value.at(-1).parts[0].state.output
    assert.ok(terminalOutput.includes('requirement_source_path = "requirements/alpha/HOW.md"'))
    assert.ok(terminalOutput.includes('requirement_source_path = "requirements/alpha/WHY.md"'))
    assert.equal(terminalOutput.includes('requirement_source_path = "requirements/alpha/WHAT.md"'), false)
    assert.equal(terminalOutput.includes('/tests/'), false)
    assert.equal(terminalOutput.includes('/APPLIES-TO'), false)

    const later = await host.requestPaths(opened.journal, dir, 's-self-material', [join(dir, 'src', 'main.fs')])
    assert.equal(later.needsGrounding, false, 'manual + automatic reads share one horizon dedupe fact')
    host.disposeJournal(opened.journal)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-006] deduplicates material content versions and re-grounds only the changed Markdown sibling', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const opened = await host.createJournal(dir)
    assert.equal(opened.ok, true)
    const journal = opened.journal
    const source = join(dir, 'src', 'main.fs')
    const first = await host.requestPaths(journal, dir, 's-dedupe', [source])
    assert.equal(first.needsGrounding, true)
    assert.equal(first.requested, 1)
    await host.projectWithJournal(journal, 's-dedupe', terminalRead(source))
    const same = await host.requestPaths(journal, dir, 's-dedupe', [source])
    assert.equal(same.needsGrounding, false)
    writeFileSync(join(dir, 'requirements', 'alpha', 'tests', 'a.test.mjs'), 'a-v2\n', 'utf8')
    const testOnlyChange = await host.requestPaths(journal, dir, 's-dedupe', [source])
    assert.equal(testOnlyChange.needsGrounding, false, 'package identity excludes tests/**')
    writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'what-v2\n', 'utf8')
    const changed = await host.requestPaths(journal, dir, 's-dedupe', [source])
    assert.equal(changed.needsGrounding, true)
    assert.equal(changed.requested, 1)
    const changedProjection = await host.projectWithJournal(journal, 's-dedupe', terminalRead(source))
    const changedOutput = changedProjection.value.at(-1).parts[0].state.output
    assert.ok(changedOutput.includes('requirement_source_path = "requirements/alpha/WHAT.md"'))
    host.disposeJournal(journal)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-006] reanchor_resets_horizon_coverage_so_the_same_digest_must_ground_again', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const opened = await host.createJournal(dir)
    const source = join(dir, 'src', 'main.fs')
    const session = 's-reanchor-dedupe'
    await host.requestPaths(opened.journal, dir, session, [source])
    await host.projectWithJournal(opened.journal, session, terminalRead(source))
    assert.equal((await host.requestPaths(opened.journal, dir, session, [source])).needsGrounding, false)

    const reanchored = await host.appendContextReanchored(opened.journal, session, 0n, 1n, 'compaction-1')
    assert.equal(reanchored.ok, true, reanchored.error)
    const after = await host.requestPaths(opened.journal, dir, session, [source])
    assert.equal(after.needsGrounding, true)
    assert.equal(after.requested, 1)
    host.disposeJournal(opened.journal)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-011] ordinary read observations add knowledge without creating authority or expanding capability', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const opened = await host.createJournal(dir)
    const source = join(dir, 'src', 'main.fs')
    await host.requestPaths(opened.journal, dir, 's-authority', [source])
    const projected = await host.projectWithJournal(opened.journal, 's-authority', terminalRead(source))
    assert.equal(projected.ok, true)
    const terminalOutput = projected.value.at(-1).parts[0].state.output
    assert.ok(terminalOutput.includes('requirement_source_path = "requirements/alpha/WHAT.md"'))
    assert.equal(projected.value.some((m) => m.info?.source === host.source), false)
    host.disposeJournal(opened.journal)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-012] freezes ordinary read-pair bytes and Cursor path-attributed result bytes for restart replay while changed digests append without rewriting the provider prefix', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const source = join(dir, 'src', 'main.fs')
    let opened = await host.createJournal(dir)
    await host.requestPaths(opened.journal, dir, 's-restart', [source])
    const first = await host.projectWithJournal(opened.journal, 's-restart', terminalRead(source))
    assert.equal(first.ok, true)
    const frozen = first.value.filter((m) => m.info?.source === host.source)
    host.disposeJournal(opened.journal)

    opened = await host.createJournal(dir)
    const replay = await host.projectWithJournal(opened.journal, 's-restart', terminalRead(source))
    assert.deepEqual(replay.value.filter((m) => m.info?.source === host.source), frozen)
    host.disposeJournal(opened.journal)
  } finally { cleanup() }
})

