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

test('WHAT[requirement-grounding-012] freezes result-only terminal bytes across restart replay while changed digests append without rewriting the provider prefix', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const source = join(dir, 'src', 'main.fs')
    let opened = await host.createJournal(dir)
    await host.requestPaths(opened.journal, dir, 's-restart', [source])
    const first = await host.projectWithJournal(opened.journal, 's-restart', terminalRead(source))
    assert.equal(first.ok, true)
    // Universal cursor mode produces no synthetic read pairs; grounding rides the terminal tool result.
    assert.equal(first.value.some((m) => m.info?.source === host.source), false)
    const frozen = first.value.at(-1).parts[0].state.output
    assert.ok(frozen.includes('requirement_source_path = "requirements/alpha/WHAT.md"'))
    assert.ok(frozen.includes('what-v1'))
    host.disposeJournal(opened.journal)

    // Restart must replay the durable occurrence bytes without rereading the files:
    // WHAT.md changed on disk, but a bare re-projection keeps the frozen bytes.
    writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'what-v2\n', 'utf8')
    opened = await host.createJournal(dir)
    const replay = await host.projectWithJournal(opened.journal, 's-restart', terminalRead(source))
    assert.equal(replay.ok, true)
    assert.equal(replay.value.at(-1).parts[0].state.output, frozen)
    assert.equal(replay.value.at(-1).parts[0].state.output.includes('what-v2'), false)

    // A fresh request grounds the changed digest by appending after the frozen prefix.
    const changed = await host.requestPaths(opened.journal, dir, 's-restart', [source])
    assert.equal(changed.needsGrounding, true)
    assert.equal(changed.requested, 1)
    const appended = await host.projectWithJournal(opened.journal, 's-restart', terminalRead(source))
    assert.equal(appended.ok, true)
    const grown = appended.value.at(-1).parts[0].state.output
    assert.ok(grown.startsWith(frozen))
    assert.ok(grown.includes('what-v2'))
    host.disposeJournal(opened.journal)
  } finally { cleanup() }
})
