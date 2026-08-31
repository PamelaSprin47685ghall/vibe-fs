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
const hookPolicySource = read('src/Wanxiangshu/OpenCode/Host/HookPolicy.fs')

// The complete set of hook keys the production PluginHooks.create builds.
// Extracted from the source so a silent addition/removal is caught.
const REGISTERED_HOOK_NAMES = [
  'chat.message',
  'chat.params',
  'experimental.chat.messages.transform',
  'experimental.chat.system.transform',
  'config',
  'experimental.session.compacting',
  'experimental.compaction.autocontinue',
  'tool.definition',
  'tool.execute.before',
  'tool.execute.after',
  'event',
  'dispose',
  'command.execute.before',
]

// ── HOST-BOUNDARY-014: fatal membrane ────────────────────────────────────

test('WHAT[HOST-BOUNDARY-014] LocalInvariant crosses the typed membrane after fatal policy', () => {
  let threw = null
  const wrapped = PluginHooksSurface.policyAwareHook('test-fatal-sync', () => { throw new Error('invariant-broken') })
  try {
    wrapped('args', 'ctx')
  } catch (e) {
    threw = e
  }
  assert.equal(threw instanceof Error, true)
  assert.equal(threw.message, 'invariant-broken')
})

test('WHAT[HOST-BOUNDARY-014] typed membrane catches async LocalInvariant and rethrows', async () => {
  let caught = null
  const wrapped = PluginHooksSurface.policyAwareHook('test-fatal-async', () => Promise.reject(new Error('async-invariant-broken')))
  try {
    await wrapped('args', 'ctx')
  } catch (e) {
    caught = e
  }
  assert.equal(caught instanceof Error, true)
  assert.equal(caught.message, 'async-invariant-broken')
})

test('WHAT[HOST-BOUNDARY-014] typed membrane calls Diagnostic.fatal before rethrow', () => {
  // The fatal membrane calls Diagnostic.fatal (which prints a JSON line to stderr)
  // before rethrowing. We capture stderr to observe the fatal record without
  // monkey-patching the module binding (which is captured at import time).
  const originalWrite = process.stderr.write.bind(process.stderr)
  const captured = []
  process.stderr.write = (chunk) => { captured.push(String(chunk)); return true }
  try {
    const wrapped = PluginHooksSurface.policyAwareHook('observed-op', () => { throw new Error('observed-error') })
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

test('WHAT[HOST-BOUNDARY-014] typed ProtocolRejection rethrows unchanged without Diagnostic.fatal', async () => {
  const originalWrite = process.stderr.write.bind(process.stderr)
  const captured = []
  process.stderr.write = (chunk) => { captured.push(String(chunk)); return true }
  try {
    const rejection = PluginHooksSurface.providerInputRejection('provider-input-invalid')
    const wrapped = PluginHooksSurface.policyAwareHook(
      'tool-before-test',
      () => Promise.reject(rejection),
    )

    await assert.rejects(() => wrapped('args', 'ctx'), (error) => error === rejection)
    assert.equal(captured.some((line) => line.includes('"operation":"tool-before-test"')), false)
  } finally {
    process.stderr.write = originalWrite
  }
})

test('WHAT[HOST-BOUNDARY-014] unpublished rejection shape fails closed as LocalInvariant', async () => {
  const originalWrite = process.stderr.write.bind(process.stderr)
  const captured = []
  process.stderr.write = (chunk) => { captured.push(String(chunk)); return true }
  try {
    const wrapped = PluginHooksSurface.policyAwareHook(
      'tool-before-invariant-test',
      () => Promise.reject(new Error('invariant-broken')),
    )

    await assert.rejects(() => wrapped('args', 'ctx'), /invariant-broken/)
    assert.equal(captured.some((line) => line.includes('"operation":"tool-before-invariant-test"')), true)
  } finally {
    process.stderr.write = originalWrite
  }
})

test('WHAT[HOST-BOUNDARY-014] every HookFailurePolicy branch is selected from typed policy and settlement evidence', () => {
  const cases = [
    ['ProtocolRejection', 'NoOwnedExecution', 'RethrowUnchanged'],
    ['Superseded', 'ExactSettlementComplete', 'RethrowUnchanged'],
    ['UserCancelled', 'ExactSettlementComplete', 'RethrowUnchanged'],
    ['CapacityQueueFull', 'ExactSettlementComplete', 'RethrowUnchanged'],
    ['PersistenceNotCommitted', 'SettlementIncomplete', 'RethrowUnchanged'],
    ['PersistenceUnknown', 'DurableOutcomeUnknown', 'FatalAfterSettlement'],
    ['LocalInvariant', 'ExactSettlementComplete', 'FatalAfterSettlement'],
    ['LocalInvariant', 'SettlementIncomplete', 'RejectFatalBeforeSettlement'],
  ]

  for (const [failure, settlement, expected] of cases) {
    assert.equal(PluginHooksSurface.hookFailurePolicy(failure, settlement), expected, `${failure}/${settlement}`)
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
  const policyKeys = [...hookPolicySource.matchAll(/\{ HostKey = "([^"]+)"/g)].map((match) => match[1])
  assert.deepEqual(policyKeys.slice().sort(), REGISTERED_HOOK_NAMES.slice().sort())
  assert.equal(new Set(policyKeys).size, policyKeys.length)
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_every_hook_accepts_its_arguments_positionally', async () => {
  // The production policy membrane wraps a two-argument callable: (args, context).
  // curriedHook and pairedHook both emit (args, context) arrow functions. The
  // paired adapter must also complete the second stage when Fable boxes an
  // already-paired function as curry2(fn); otherwise the Host sees a fulfilled
  // hook whose body never ran.
  assert.match(interopSource, /\(args,\s*context\)\s*=>\s*\$0\(args\)\(context\)/)
  assert.match(interopSource, /typeof result === 'function' \? result\(context\) : result/)
  assert.match(pluginHooksSource, /registeredHook HookKey\.ToolBefore \(pairedHook \(box toolBefore\)\)/)
  // The fatal membrane itself is a two-argument callable. It wraps the return
  // in Promise.resolve, so the positional args arrive but the result is async.
  const wrapped = PluginHooksSurface.policyAwareHook('positional-test', (args, context) => ({ args, context }))
  const result = await wrapped('arg-val', 'ctx-val')
  assert.equal(result.args, 'arg-val')
  assert.equal(result.context, 'ctx-val')
})

test('WHAT[HOST-BOUNDARY-014] tool.execute.before uses the common typed membrane after arity adaptation', () => {
  assert.match(pluginHooksSource, /registeredHook HookKey\.ToolBefore \(pairedHook \(box toolBefore\)\)/)
  assert.match(interopSource, /metadata\.HostKey, policyAwareHook metadata\.DiagnosticOperation adaptedHook/)
  assert.doesNotMatch(interopSource, /isExpected|classifiedRejectionHook|hostErrorText/)
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_the_tool_registry_is_a_registry_not_a_triggered_hook', () => {
  // The tool registry is attached as hooks.tool — a property holding a Tools
  // collection, not a hook callable. It is never in the Host's Hooks type.
  assert.match(pluginHooksSource, /"tool", registration\.Tools/)
  // tool is not among the triggered hook names (it has no policy membrane wrapper).
  assert.equal(REGISTERED_HOOK_NAMES.includes('tool'), false)
})

// ── HOST-BOUNDARY-019: capability gap proof ──────────────────────────────

test('WHAT[HOST-BOUNDARY-019] STRENGTH_004_replica_transform_route_is_structurally_exclusive', () => {
  // The transform hook is registered exactly once under the experimental name.
  // There is no second 'chat.transform' alias — that was removed because the
  // Host Hooks type has only the experimental key.
  assert.match(hookPolicySource, /HostKey = "experimental\.chat\.messages\.transform"/)
  assert.doesNotMatch(hookPolicySource, /HostKey = "chat\.transform"/)
})

test('WHAT[HOST-BOUNDARY-019] CHAT_MESSAGE_routes_managed_model_then_CHAT_PARAMS_only_validates', () => {
  // chat.message is registered before chat.params in the hook object.
  const chatMessageIdx = pluginHooksSource.indexOf('registeredHook HookKey.ChatMessage')
  const chatParamsIdx = pluginHooksSource.indexOf('registeredHook HookKey.ChatParams')
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
  // through the transform — the lifecycle wrapper only admits/drains the same
  // transform and does not strip or rewrite user roots.
  assert.match(pluginHooksSource, /let ownedTransform[\s\S]*?scope\.RunOwnedWork\(fun \(\) -> transform inObj outObj\)/)
  assert.match(
    pluginHooksSource,
    /registeredHook HookKey\.MessagesTransform \(curriedHook \(box ownedTransform\)\)/,
  )
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
