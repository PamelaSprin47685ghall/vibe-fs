import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as grounding from '../../../dist/OpenCode/Host/RequirementGroundingSurface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-opencode-'))
  mkdirSync(join(dir, 'requirements', 'alpha'), { recursive: true })
  mkdirSync(join(dir, 'src'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'ground truth\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n', 'utf8')
  writeFileSync(join(dir, 'src', 'main.fs'), 'before\n', 'utf8')
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const toolBatch = (providerID, path) => [
  { info: { id: 'c1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'pending', input: { filePath: path }, time: { start: 0 } } }] },
  { info: { id: 'r1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'completed', input: { filePath: path }, output: 'before\n', time: { start: 0, end: 0 } } }] },
]

test('WHAT[requirement-grounding-007] actual pair and grounding transforms append result-only bytes for each tested provider without synthetic calls', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    assert.equal(grounding.cursorSeparator, '\0\uFEFF')
    for (const provider of ['anthropic', 'cursor', 'openai']) {
      const opened = await grounding.createJournal(dir)
      try {
        await grounding.requestPaths(opened.journal, dir, provider, [sourcePath])
        const input = toolBatch(provider, sourcePath)
        const paired = await pair.tryInject(provider, pair.text, input)
        assert.equal(paired.ok, true)
        const result = await grounding.projectWithJournal(opened.journal, provider, paired.value)
        assert.equal(result.ok, true)
        assert.equal(result.value.length, input.length)
        assert.deepEqual(result.value.map((message) => message.info), input.map((message) => message.info))
        assert.deepEqual(result.value[0], input[0], 'pending tool call remains untouched')
        const terminal = result.value.at(-1).parts[0].state.output
        assert.ok(terminal.startsWith('before\n'))
        assert.ok(terminal.includes('\0\uFEFF# ground truth'))
        assert.ok(terminal.includes('requirement_source_path = "requirements/alpha/WHAT.md"'))
        assert.equal(result.value.some((message) => message.info?.source === grounding.source), false)
        const pairAt = terminal.indexOf(pair.text.trim())
        if (paired.value.at(-1).parts[0].state.output.includes(pair.text.trim())) {
          assert.ok(pairAt >= 0)
          assert.ok(terminal.indexOf('requirement_source_path') > pairAt)
        }
        assert.deepEqual(input, toolBatch(provider, sourcePath), 'input is not mutated')
      } finally { grounding.disposeJournal(opened.journal) }
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-007] candidate discovery tools do not ground, while an explicit read does', async () => {
  const { dir, cleanup } = sandbox()
  const opened = await grounding.createJournal(dir)
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    for (const tool of ['grep', 'glob', 'list']) {
      const result = await grounding.observationDecision(opened.journal, dir, 'candidate', tool, { filePath: sourcePath, path: join(dir, 'src') }, `${sourcePath}:1:before\n`)
      assert.equal(result.ok, true)
      assert.equal(result.needsGrounding, false)
      assert.equal(result.requested, 0)
      assert.deepEqual(result.packages, [])
    }
    const read = await grounding.observationDecision(opened.journal, dir, 'candidate', 'read', { filePath: sourcePath }, 'before\n')
    assert.equal(read.ok, true)
    assert.equal(read.needsGrounding, true)
    assert.equal(read.requested, 1)
    assert.deepEqual(read.packages, ['alpha'])
  } finally {
    grounding.disposeJournal(opened.journal)
    cleanup()
  }
})

test('WHAT[requirement-grounding-007] original Markdown bytes survive the actual result suffix unchanged', { todo: 'GAP-086: current LlmFacing instruction rendering adds comments and normalizes newlines; resolve the representation contract' }, async () => {
  const { dir, cleanup } = sandbox()
  const opened = await grounding.createJournal(dir)
  try {
    const content = 'first\r\n\r\nsecond  \r\n'
    writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), content)
    const path = join(dir, 'src', 'main.fs')
    await grounding.requestPaths(opened.journal, dir, 'raw-bytes', [path])
    const result = await grounding.projectWithJournal(opened.journal, 'raw-bytes', toolBatch('anthropic', path))
    assert.equal(result.ok, true)
    assert.ok(result.value.at(-1).parts[0].state.output.includes(content))
  } finally {
    grounding.disposeJournal(opened.journal)
    cleanup()
  }
})

const assertRegisteredComposition = (messages, original, resultId) => {
  assert.equal(messages.length, original.length, 'registered composition preserves message count')
  assert.deepEqual(messages.map(message => message.info), original.map(message => message.info))
  assert.deepEqual(messages[0], original[0], 'pending tool call and arguments are untouched')
  const output = messages.find(message => message.info.id === resultId).parts[0].state.output
  const originalOutput = original.find(message => message.info.id === resultId).parts[0].state.output
  assert.ok(output.startsWith(originalOutput), 'existing result and suffix bytes remain intact')
  const guidance = 'Pair Programming: Language Anchor'
  const requirementSource = 'requirement_source_path = "requirements/alpha/WHAT.md"'
  assert.equal(output.split(guidance).length - 1, 1, 'registered guidance is present exactly once')
  assert.equal(output.split(requirementSource).length - 1, 1, 'registered grounding is present exactly once')
  assert.ok(output.indexOf(guidance) < output.indexOf(requirementSource), 'registered guidance precedes grounding')
  assert.equal(messages.some(message => message.info.source === pair.source || message.info.source === grounding.source), false)
}

const exerciseRegisteredComposition = async (providerID) => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    mkdirSync(join(directory, 'requirements', 'alpha'), { recursive: true })
    mkdirSync(join(directory, 'src'), { recursive: true })
    writeFileSync(join(directory, 'requirements', 'alpha', 'WHAT.md'), 'GROUNDING-ONLY-MATERIAL\n')
    writeFileSync(join(directory, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n')
    const sourcePath = join(directory, 'src', 'main.fs')
    writeFileSync(sourcePath, 'before\n')
    for (const tool of ['read', 'write', 'edit', 'patch', 'apply_patch', 'mv', 'rm', 'js-manager', 'js-engineer']) {
      const sessionID = `composition-${providerID}-${tool}`
      if (tool === 'js-manager') await acceptAuthorityRoot(runtime, sessionID, 'manager')
      if (tool === 'js-engineer') await acceptAuthorityRoot(runtime, sessionID, 'engineer')
      await openIncumbency(runtime, sessionID)
      const callID = `call-${sessionID}`
      const args = tool === 'js-manager'
        ? { program: "class Js extends JsProgram { async run() { const file = await this.file('src/main.fs'); return file.text(); } }" }
        : tool === 'js-engineer'
          ? { program: "class Js extends JsProgram { async run() { await this.write('src/generated.fs', 'PROGRAM-MUTATION'); return 'PROGRAM-WRITTEN'; } }" }
          : tool === 'mv'
            ? { source: sourcePath, destination: join(directory, 'src', 'next.fs') }
            : { filePath: sourcePath, content: 'changed\n' }
      const input = { tool, sessionID, callID }
      await hooks['tool.execute.before'](input, { args })
      const actualOutput = tool.startsWith('js-')
        ? await hooks.tool[tool].execute(args, { sessionID, agent: tool.slice(3) })
        : 'NATIVE-RESULT\r\n'
      if (tool === 'js-manager') assert.match(actualOutput, /before/, 'registered program actually reads covered source')
      if (tool === 'js-engineer') assert.match(actualOutput, /PROGRAM-WRITTEN/, 'registered program actually mutates covered source')
      if (tool === 'js-engineer') assert.equal(readFileSync(join(directory, 'src', 'generated.fs'), 'utf8'), 'PROGRAM-MUTATION')
      const rawOutput = `${actualOutput}\0\uFEFFEXISTING-SUFFIX\n`
      await hooks['tool.execute.after']({ ...input, args }, { title: tool, output: rawOutput, metadata: {} })
      const original = [{
        info: { id: `pending-${sessionID}`, role: 'assistant', sessionID, providerID },
        parts: [{ id: `pending-part-${sessionID}`, type: 'tool', tool, callID,
          state: { status: 'pending', input: args, time: { start: 0 } } }],
      }, {
        info: { id: `result-${sessionID}`, role: 'assistant', sessionID, providerID },
        parts: [{ id: `part-${sessionID}`, type: 'tool', tool, callID,
          state: { status: 'completed', input: args, output: rawOutput, time: { start: 0, end: 1 } } }],
      }]
      const transformed = { messages: structuredClone(original) }
      await hooks['experimental.chat.messages.transform']({}, transformed)
      assertRegisteredComposition(transformed.messages, original, original.at(-1).info.id)
      assert.deepEqual(original.at(-1).parts[0].state.output, rawOutput)
      const canonical = await trace.currentProjection(runtime.journal, sessionID)
      assert.deepEqual(canonical.messages.flatMap(message => message.parts).filter(part => part.kind === 'tool-result'),
        [{ kind: 'tool-result', result: rawOutput }], 'canonical capture retains Host bytes but excludes both presentation additions')

      const replay = { messages: structuredClone(original) }
      await hooks['experimental.chat.messages.transform']({}, replay)
      assert.deepEqual(replay.messages, transformed.messages, 'raw Host replay reproduces the same anchored suffix once')
      const canonicalReplay = await trace.currentProjection(runtime.journal, sessionID)
      assert.deepEqual(canonicalReplay, canonical, 'presentation replay cannot append or contaminate canonical X')
    }
  })
}

for (const providerID of ['anthropic', 'cursor', 'openai']) {
  test(`WHAT[requirement-grounding-007] registered composition preserves result suffixes, orders guidance before grounding and keeps canonical X clean (${providerID})`, () =>
    exerciseRegisteredComposition(providerID))
}

for (const mutation of ['missing-guidance', 'missing-grounding', 'reversed']) {
  test(`WHAT[requirement-grounding-007] registered composition proof rejects ${mutation} production wiring`, () => {
    const env = { ...process.env, WANXIANGSHU_GROUNDING_COMPOSITION_MUTATION: mutation }
    delete env.NODE_TEST_CONTEXT
    const result = spawnSync(process.execPath, [
      '--loader', new URL('./support/007-composition-loader.mjs', import.meta.url).href,
      '--test', '--test-name-pattern=registered composition preserves result suffixes.*anthropic',
      new URL(import.meta.url).pathname,
    ], {
      encoding: 'utf8', timeout: 30000,
      env,
    })
    assert.equal(result.error, undefined, `${mutation}: mutation child completed`)
    assert.equal(result.signal, null, `${mutation}: mutation child was not terminated`)
    assert.equal(result.status, 1, `${mutation}: the same registered composition assertion must fail`)
    assert.match(result.stdout, /registered (guidance is present exactly once|grounding is present exactly once|guidance precedes grounding)/)
  })
}
