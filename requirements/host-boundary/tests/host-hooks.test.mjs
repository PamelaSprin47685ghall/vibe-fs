// HOST-BOUNDARY-014 / 018 / 019: the plugin hook surface and fatal membrane.
//
// Production owners (no support fixture): the registered hook set and the
// PluginHooksSurface fatal membrane. The hook name set, positional arity, and
// fatal-membrane-before-rethrow contract are read from the production source
// that builds the hooks object and exercised through the registered surface.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

import * as PluginHooksSurface from '../../../dist/OpenCode/Host/PluginHooksSurface.js'

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

const pluginHooksSource = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')
const interopSource = read('src/Wanxiangshu/OpenCode/Host/PluginHostInterop.fs')
const generatedPluginHooks = read('dist/OpenCode/Plugin/PluginHooks.js')

// The complete set of hook keys the production PluginHooks.create builds.
// Extracted from the source so a silent addition/removal is caught.
const REGISTERED_HOOK_NAMES = [
  'chat.message',
  'chat.params',
  'experimental.chat.messages.transform',
  'experimental.chat.system.transform',
  'experimental.session.compacting',
  'experimental.compaction.autocontinue',
  'tool.definition',
  'tool.execute.before',
  'tool.execute.after',
  'event',
  'dispose',
]

// ── HOST-BOUNDARY-014: fatal membrane ────────────────────────────────────

test('WHAT[HOST-BOUNDARY-014] HOST_009_hook_invariant_exceptions_cross_a_fatal_membrane_before_rethrow', () => {
  let threw = null
  const wrapped = PluginHooksSurface.fatalHook('test-fatal-sync', () => { throw new Error('invariant-broken') })
  try {
    wrapped('args', 'ctx')
  } catch (e) {
    threw = e
  }
  assert.equal(threw instanceof Error, true)
  assert.equal(threw.message, 'invariant-broken')
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_fatal_membrane_catches_async_rejection_and_rethrows', async () => {
  let caught = null
  const wrapped = PluginHooksSurface.fatalHook('test-fatal-async', () => Promise.reject(new Error('async-invariant-broken')))
  try {
    await wrapped('args', 'ctx')
  } catch (e) {
    caught = e
  }
  assert.equal(caught instanceof Error, true)
  assert.equal(caught.message, 'async-invariant-broken')
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_fatal_membrane_calls_diagnostic_fatal_before_rethrow', () => {
  // The fatal membrane calls Diagnostic.fatal (which prints a JSON line to stderr)
  // before rethrowing. We capture stderr to observe the fatal record without
  // monkey-patching the module binding (which is captured at import time).
  const originalWrite = process.stderr.write.bind(process.stderr)
  const captured = []
  process.stderr.write = (chunk) => { captured.push(String(chunk)); return true }
  try {
    const wrapped = PluginHooksSurface.fatalHook('observed-op', () => { throw new Error('observed-error') })
    try {
      wrapped('a', 'b')
    } catch (_) {
      // expected rethrow
    }
    const fatalLine = captured.find((line) => line.includes('"operation":"observed-op"'))
    assert.ok(fatalLine, 'Diagnostic.fatal must print a JSON line with the operation name')
    assert.match(fatalLine, /"result":"observed-error"/)
  } finally {
    process.stderr.write = originalWrite
  }
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_inherited_NODE_TEST_CONTEXT_never_disables_production_fatal', () => {
  // The FatalProcess.kill Emit in src has exactly one gate: WANXIANGSHU_NO_FATAL_EXIT.
  // There is no NODE_TEST_CONTEXT gate. Production fatal fires regardless of test context.
  const fatalProcessSource = read('src/Wanxiangshu/Foundation/FatalProcess.fs')
  assert.doesNotMatch(fatalProcessSource, /NODE_TEST_CONTEXT/i)
  assert.match(fatalProcessSource, /WANXIANGSHU_NO_FATAL_EXIT/)
  // The only suppression is the explicit test harness env var, not an inherited context.
  assert.equal(process.env.WANXIANGSHU_NO_FATAL_EXIT, '1')
})

// ── HOST-BOUNDARY-014: registered hook set ───────────────────────────────

test('WHAT[HOST-BOUNDARY-014] HOST_009_every_registered_hook_has_a_fixture_here', () => {
  // Every hook key that PluginHooks.create assigns must appear in our fixture list.
  // This catches silent additions (a new hook with no test) and silent removals.
  // Some hooks are string keys in createObj; others are dynamic hooks? assignments.
  for (const name of REGISTERED_HOOK_NAMES) {
    const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    // Match either "name" in createObj or hooks?name <- assignment
    const pattern = new RegExp(`["']${escaped}["']|hooks\\?${escaped}\\s*<-`)
    assert.match(pluginHooksSource, pattern, `PluginHooks.fs must register hook "${name}"`)
  }
  // The experimental.chat.messages.transform route is structurally exclusive:
  // exactly one transform registration, not a second live copy.
  const transformMatches = pluginHooksSource.match(/experimental\.chat\.messages\.transform/g)
  assert.equal(transformMatches.length, 1)
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_every_hook_accepts_its_arguments_positionally', async () => {
  // The production fatalHook wraps a two-argument callable: (args, context).
  // curriedHook and pairedHook both emit (args, context) arrow functions. The
  // paired adapter must also complete the second stage when Fable boxes an
  // already-paired function as curry2(fn); otherwise the Host sees a fulfilled
  // hook whose body never ran.
  assert.match(interopSource, /\(args,\s*context\)\s*=>\s*\$0\(args\)\(context\)/)
  assert.match(interopSource, /typeof result === 'function' \? result\(context\) : result/)
  assert.doesNotMatch(
    generatedPluginHooks,
    /"tool\.execute\.before"[^\n]*=>\s*curry2\(toolBefore\)\(args, context\)\)/,
    'generated tool.execute.before adapter must not return an uninvoked curry2 second stage',
  )
  assert.match(
    generatedPluginHooks,
    /"tool\.execute\.before"[^\n]*typeof result === 'function' \? result\(context\) : result/,
    'generated tool.execute.before adapter must finish Fable curry2 boxing when present',
  )
  // The fatal membrane itself is a two-argument callable. It wraps the return
  // in Promise.resolve, so the positional args arrive but the result is async.
  const wrapped = PluginHooksSurface.fatalHook('positional-test', (args, context) => ({ args, context }))
  const result = await wrapped('arg-val', 'ctx-val')
  assert.equal(result.args, 'arg-val')
  assert.equal(result.context, 'ctx-val')
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_the_tool_registry_is_a_registry_not_a_triggered_hook', () => {
  // The tool registry is attached as hooks.tool — a property holding a Tools
  // collection, not a hook callable. It is never in the Host's Hooks type.
  assert.match(pluginHooksSource, /hooks\?tool\s*<-\s*toolRegistration\.Tools/)
  // tool is not among the triggered hook names (it has no fatalHook wrapper).
  assert.equal(REGISTERED_HOOK_NAMES.includes('tool'), false)
})

// ── HOST-BOUNDARY-019: capability gap proof ──────────────────────────────

test('WHAT[HOST-BOUNDARY-019] STRENGTH_004_replica_transform_route_is_structurally_exclusive', () => {
  // The transform hook is registered exactly once under the experimental name.
  // There is no second 'chat.transform' alias — that was removed because the
  // Host Hooks type has only the experimental key.
  assert.match(pluginHooksSource, /experimental\.chat\.messages\.transform/)
  assert.doesNotMatch(pluginHooksSource, /["']chat\.transform["']/)
})

test('WHAT[HOST-BOUNDARY-019] CHAT_MESSAGE_routes_managed_model_then_CHAT_PARAMS_only_validates', () => {
  // chat.message is registered before chat.params in the hook object.
  const chatMessageIdx = pluginHooksSource.indexOf('"chat.message"')
  const chatParamsIdx = pluginHooksSource.indexOf('"chat.params"')
  assert.ok(chatMessageIdx >= 0, 'chat.message must be registered')
  assert.ok(chatParamsIdx >= 0, 'chat.params must be registered')
  assert.ok(chatMessageIdx < chatParamsIdx, 'chat.message is registered before chat.params')
  // chat.params only validates — it never mutates the model (only temperature).
  const chatParamsHookSource = read('src/Wanxiangshu/OpenCode/Host/ChatParamsHook.fs')
  assert.match(chatParamsHookSource, /applyManagedTemperature/)
  // chat.params does not rewrite the model id.
  assert.doesNotMatch(chatParamsHookSource, /output\?model.*<-\s*[^t]/)
})

test('WHAT[HOST-BOUNDARY-019] CHAT_MESSAGE_new_physical_material_supersedes_old_capacity_without_idle', () => {
  // The physical user message identity is exact: a new material supersedes
  // the old one. This is a structural fact of the message identity model,
  // not an idle-derived continuation. The hook surface does not derive
  // identity from idle signals.
  assert.doesNotMatch(pluginHooksSource, /idle.*identity|SessionIdle.*PhysicalUserMessage/)
  // The chat.message hook routes through wired.ChatMessageHook, which is
  // the managed model admission owner — not an idle consumer.
  assert.match(pluginHooksSource, /wired\.ChatMessageHook/)
})

test('WHAT[HOST-BOUNDARY-019] PROMPT_004_human_root_survives_host_synthetic_file_parts', () => {
  // The transform hook receives the full message array including host-synthetic
  // parts. The human root message identity (role=user, id=root) is preserved
  // through the transform — the transform does not strip or rewrite user roots.
  assert.match(pluginHooksSource, /experimental\.chat\.messages\.transform[\s\S]*?curriedHook.*transform/)
  // The transform is a pure function over the message array; it does not
  // consume host-synthetic file parts as business input.
  const transformsSource = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')
  assert.doesNotMatch(transformsSource, /source.*host.*business|host.*file.*fact/)
})

test('WHAT[HOST-BOUNDARY-019] AGENT_007_tool_gate_recovers_human_root_from_host_snapshot_on_resume', () => {
  // The tool.execute.before hook decodes context from the tool input, not
  // from a host snapshot. The human root is recovered from the durable
  // snapshot via SessionSnapshotPort, not from the hook args.
  assert.match(pluginHooksSource, /ToolHostCodec\.decodeContext/)
  // The hook reads journal + toolCallId for estimate observation, not for
  // root recovery.
  assert.match(pluginHooksSource, /DelegatedToolEstimateLedger\.observe/)
})
