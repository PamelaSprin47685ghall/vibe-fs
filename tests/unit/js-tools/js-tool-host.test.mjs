// tests/unit/js-tools/js-tool-host.test.mjs — builtin coexistence + generated
// js-* tool spec. Primitive filesystem tools remain normal fallbacks: no
// DEPRECATED annotation. Intent-level preference lives in the js-* description.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  BuiltinToolDescriptionHook_annotate as annotate,
  BuiltinToolDescriptionHook_validateRecommendation as validateRecommendation,
  BuiltinToolDescriptionHook_BuiltinFilesystemTools as builtinTools,
  JsDescriptionAssets_load as loadJsProse,
} from '../../../dist/Infrastructure/OpenCode/Tools/JsToolHost.js'
import { JsToolGenerator_generate as generate } from '../../../dist/Domain/JsSurface.js'
import { ProviderLanguage } from '../../../dist/Domain/ProviderLanguage.js'
import { ToolPermission } from '../../../dist/Kernel/Roles.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'
import { resultOf, stringSet } from '../support/domain.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-host-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const permissionComparer = { Compare: (a, b) => a.CompareTo(b) }
const jsProse = () => loadJsProse(ProviderLanguage.English)
const coderCaps = ofArray(
  [ToolPermission.Read, ToolPermission.Write, ToolPermission.Edit, ToolPermission.Glob, ToolPermission.Grep],
  permissionComparer,
)

test('JS003_builtin_fallback_descriptions_are_left_untouched', () => {
  assert.deepEqual([...builtinTools].sort(), ['edit', 'glob', 'grep', 'patch', 'read', 'write'])
  for (const name of builtinTools) {
    const original = `${name} primitive fallback`
    const visible = annotate(name, original, 'js-coder')
    assert.equal(visible, original)
    assert.doesNotMatch(visible, /DEPRECATED|js-coder/i)
  }
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
    const surface = generate('Coder', coderCaps, jsProse())
    // Build the spec with the real Host tool factory, like ToolRegistry does.
    const codec = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
    const tool = (definition) => definition
    tool.schema = {
      string: () => ({
        type: 'string',
        describe: (description) => ({ type: 'string', description }),
      }),
    }
    const factory = codec.ToolHostCodec_factory({ tool })
    const { JsToolSpec_create: create } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolHost.js')
    const spec = create(factory, surface, dir, undefined)
    const registered = codec.ToolHostCodec_register(factory, spec)

    assert.equal(spec.Name, 'js-coder')
    assert.equal(spec.Description.includes('class JsProgram'), true)
    assert.equal(spec.Description.includes('HOST_READ_IMMUTABLE_UTF8_SNAPSHOT'), true)
    assert.equal(spec.Description.includes('_api'), false)

    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', view.text('^', 'begin') + 'goodbye' + view.text('end', '$'));
    return { before: view.text() };
  }
}`
    const result = await registered.execute({ program }, { sessionID: 'ses-test', agent: 'fast-coder' })
    assert.equal(result.startsWith('# ok\n'), true)
    assert.equal(result.includes('[data]'), true)
    assert.equal(result.includes('before'), true)
    assert.equal(result.includes('[fs]'), true)
    assert.equal(result.includes('status ='), false)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed via workflow')
    const missing = await registered.execute({}, { sessionID: 'ses-test', agent: 'fast-coder' })
    assert.equal(missing.includes('error'), true)
    assert.equal(missing.includes("missing 'program' argument"), true)
  } finally {
    cleanup()
  }
})
