import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import {
  applyView, captureView, clearAll, takeView,
} from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const session = 'carry-transfer'
const visible = () => [
  { id: '1', text: 'one' },
  { id: '2', text: 'two\nsecond line' },
  { id: '3', text: 'three' },
]
const childContext = (prompt) => {
  if (!prompt) return null
  const wire = (prompt.body?.parts ?? []).map((part) => part.text).join('\n')
  const document = parse(wire)
  return { text: (document.context ?? []).map((row) => row.text), wire }
}
const withForkMember = async (body) => {
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    await acceptAuthorityRoot(runtime, session, 'manager', 'physical-carry-transfer')
    const call = async (carrySource, callId) => {
      const before = created.length
      const promptsBefore = runtime.prompts.length
      const argument = carrySource === undefined ? '' : ', ' + carrySource
      const reply = await hooks.tool['todowrite-for-manager'].execute(
        {
          name: 'carry-transfer-test',
          program: 'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada' + callId + '", charge: "DO THE WORK CURRENT INTENT" }' + argument + '); } }',
          prefixUntil: 0,
          carry: [],
        },
        { sessionID: session, agent: 'manager', callID: callId, messageID: 'run-' + callId },
      )
      const child = created.length > before ? created[created.length - 1] : null
      const prompt = runtime.prompts.slice(promptsBefore).find((candidate) => candidate.path?.id === child)
        ?? runtime.prompts[promptsBefore]
      return { reply: String(reply), child, context: childContext(prompt) }
    }
    await body(call)
  })
}
test.beforeEach(() => clearAll())

test('WHAT[context-compression-032] actual fork receives carry in order as native TOML context tables', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())
    const result = await call('[3, "literal\\nreport", 2, 3, "", "  "]', 'ordered')
    assert.match(result.reply, /ok = true/)
    assert.ok(result.child)
    assert.deepEqual(result.context.text, ['three', 'literal\nreport', 'two\nsecond line', 'three', '', '  '])
    assert.match(result.context.wire, /\[\[context\]\]/)
    assert.match(result.context.wire, /text = """\nliteral\nreport/)
    assert.doesNotMatch(result.context.wire, /commissioner_record|literal\\nreport/)
    assert.equal(result.context.text.some((text) => text.includes('CURRENT INTENT')), false)
  })
})

test('WHAT[context-compression-032] repeated transfers resolve the same history without consuming it', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())
    const first = await call('[2, "literal"]', 'repeat1')
    const second = await call('[2, "literal"]', 'repeat2')
    assert.match(first.reply, /ok = true/)
    assert.match(second.reply, /ok = true/)
    assert.deepEqual(first.context.text, second.context.text)
  })
})

test('WHAT[context-compression-032] invalid carry and unknown anchors place no child', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())
    const invalid = ['{}', 'null', '[null]', '[true]', '[-1]', '[1.5]', '[999]', '[[]]', '[{}]', '[NaN]', '[{toJSON(){return "fake"}}]']
    for (const [index, source] of invalid.entries()) {
      const result = await call(source, 'bad' + index)
      assert.match(result.reply, /ok = false/, source)
      assert.match(result.reply, /code = "VIEW_DECLARATION_INVALID"/, source)
      assert.equal(result.child, null)
      assert.equal(result.context, null)
    }
  })
})

test('WHAT[context-compression-032] empty carry sends only the task; omitted carry is refused', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())
    const omitted = await call(undefined, 'omitted')
    assert.match(omitted.reply, /ok = false/)
    assert.match(omitted.reply, /code = "VIEW_DECLARATION_INVALID"/)
    assert.equal(omitted.child, null)
    const empty = await call('[]', 'empty')
    assert.match(empty.reply, /ok = true/)
    assert.ok(empty.child)
    assert.deepEqual(empty.context.text, [])
    assert.doesNotMatch(empty.context.wire, /second line/)
  })
})

test('WHAT[context-compression-032] transfer reads the snapshot without consuming it while the tool commits its own declaration', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())
    assert.equal(captureView(session, { prefixUntil: 2, carry: ['later summary'] }).ok, true)
    const result = await call('[2, "extra"]', 'readonly')
    assert.match(result.reply, /ok = true/)
    assert.deepEqual(result.context.text, ['two\nsecond line', 'extra'])
    // Successful tool calls always commit their required declaration.
    assert.deepEqual(takeView(session), { prefixUntil: 0, carry: [] })
    assert.equal(takeView(session), null)
    const after = applyView(session, visible())
    assert.deepEqual(after.entries.map((entry) => entry.kind === 'kept' ? entry.message.text : entry.text), [])
  })
})

