// tests/unit/js-tools/js-tool-host.test.mjs — G5 Phase C: builtin coexistence
// hook (JS-003) + generated js-* tool spec (JS-073/074).
//
// The hook only rewrites descriptions (never schema/executor), is idempotent,
// and must not recommend a tool the provider cannot see. The spec executes a
// model program through the workflow and renders a stable TOML result.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  BuiltinToolDescriptionHook_annotate as annotate,
  BuiltinToolDescriptionHook_validateRecommendation as validateRecommendation,
  BuiltinToolDescriptionHook_BuiltinFilesystemTools as builtinTools,
} from '../../../dist/Infrastructure/JsToolHost.js'
import { JsToolGenerator_generate as generate } from '../../../dist/Domain/JsTools.js'
import { ToolPermission } from '../../../dist/Kernel/Roles.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'
import { resultOf, stringSet } from '../support/domain.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-host-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const permissionComparer = { Compare: (a, b) => a.CompareTo(b) }
const coderCaps = ofArray(
  [ToolPermission.Read, ToolPermission.Write, ToolPermission.Edit, ToolPermission.Glob, ToolPermission.Grep],
  permissionComparer,
)

test('JS003_hook_annotates_builtin_descriptions_idempotently', () => {
  assert.deepEqual([...builtinTools].sort(), ['edit', 'glob', 'grep', 'patch', 'read', 'write'])
  const annotated = annotate('read', 'Read a file', 'js-coder')
  assert.equal(annotated.includes('DEPRECATED'), true)
  assert.equal(annotated.includes('js-coder'), true)
  assert.equal(annotated.includes('parallel calls are safe'), true)
  // idempotent: already-deprecated descriptions are not re-annotated
  const again = annotate('read', annotated, 'js-coder')
  assert.equal(again, annotated)
  // non-builtin tools are untouched
  assert.equal(annotate('join', 'Join a session', 'js-coder'), 'Join a session')
})

test('JS003_hook_must_not_recommend_invisible_tools', () => {
  assert.equal(resultOf(validateRecommendation('js-coder', stringSet(['js-coder', 'read']))).ok, true)
  const denied = resultOf(validateRecommendation('js-coder', stringSet(['read'])))
  assert.equal(denied.ok, false)
  assert.equal(denied.error.includes('not provider-visible'), true)
})

test('JS073_spec_executes_program_and_renders_result', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const surface = generate('Coder', coderCaps)
    // Build the spec with the real Host tool factory, like ToolRegistry does.
    const codec = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
    const toolModule = { tool: { schema: { string: () => ({ type: 'string' }) } } }
    const factory = codec.ToolHostCodec_factory(toolModule)
    const { JsToolSpec_create: create } = await import('../../../dist/Infrastructure/JsToolHost.js')
    const spec = create(factory, surface, dir, undefined)

    assert.equal(spec.Name, 'js-coder')
    assert.equal(spec.Description.includes('file'), true)

    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt');
    await this.rewrite('a.txt', { find: 'hello', replace: 'goodbye' });
    return { before: view.text };
  }
}`
    const result = await spec.Execute({ program }, { sessionID: 'ses-test', agent: 'fast-coder' })
    assert.equal(result.includes('status = "ok"'), true)
    assert.equal(result.includes('before'), true)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed via workflow')
    // missing program argument → stable error
    const missing = await spec.Execute({}, { sessionID: 'ses-test', agent: 'fast-coder' })
    assert.equal(missing.includes('error'), true)
  } finally {
    cleanup()
  }
})
