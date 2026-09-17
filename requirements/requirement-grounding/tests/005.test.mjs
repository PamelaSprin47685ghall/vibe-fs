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
