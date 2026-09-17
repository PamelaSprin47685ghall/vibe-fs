import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toml = await import("../../../dist/Context/Companion/Blogger/TomlSurface.js");
const owner = await import("../../../dist/Context/Companion/ProjectionSurface.js");

const ident = owner
const prompt = owner
const proj = owner
const spy = (input) => `«${input}»`
const frames = (count) =>
  Array.from({ length: count }, (_, n) => ({ digest: `sha-f${n}`, body: `frame body ${n}` }))
const dataItems = [{ role: 'user', kind: 'text', text: 'work', truncated: false }]
const dataToml = '[[new_work_to_record]]\nuser = "work"\n'
const combinedDelta = prompt.newWork(dataItems)
const isHistoricFrame = (text) => text.startsWith('[[do_not_exec]]') && text.includes('historic_frame')
const isCombinedNormalDelta = (text) =>
  text.startsWith('# Write the dense work-log continuation now') && text.includes('[[new_work_to_record]]')
const isPreviousTip = (text) => text.includes('previous_enforcer_tip')

test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_no_prompt_carries_a_token_count_or_output_budget', () => {
  const all = [
    prompt.normalInstruction,
    prompt.squashInstruction,
    prompt.memoryPreamble,
    prompt.workingRecord('body'),
    prompt.newWork(dataItems),
  ]

  for (const text of all) {
    assert.doesNotMatch(text, /token|budget|limit|KiB|bytes/i, 'no capacity vocabulary')
    assert.doesNotMatch(text, /%s|%d|\{0\}/, 'no format placeholders')
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: fs } = await import("node:fs");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const NEXT_DIR = path.join(ROOT, 'src', 'Wanxiangshu')
const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
const files = [...subsystemInventory.projects.values()]
  .filter((project) => project.subsystem === 'context')
  .flatMap((project) => project.implementationFiles)
  .sort()

test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_context_compression_owner_never_observes_forbidden_capacity_synonyms', () => {
  // CTX-001's exact forbidden vocabulary, with one allowed exception: the
  // BloggerDeltaLimitBytes input contract (CTX-003) is a byte LIMIT on one
  // delta, not a window estimate — it is tested elsewhere and must stay.
  const forbidden = [
    'contextWindow',
    'remainingTokens',
    'headroom',
    'nearLimit',
    'shouldCompact',
    'ensureCapacity',
    'tokenizer',
    'ByteToToken',
    'TokenToByte',
  ]

  assert.ok(files.length > 0, 'context subsystem must own production files')
  for (const file of files) {
    const source = fs.readFileSync(file, 'utf8')
    for (const name of forbidden) {
      assert.ok(
        !source.includes(name),
        `CTX-001: forbidden capacity synonym "${name}" found in ${path.relative(ROOT, file)}`,
      )
    }
  }
})
test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_the_only_allowed_byte_metric_is_the_delta_input_contract', () => {
  // The one legal byte quantity: BloggerDeltaLimitBytes = 200 KiB measured on
  // rendered TOML (CTX-003). It must exist and be a constant, not a query of
  // the provider window.
  const bloggerDelta = fs.readFileSync(path.join(NEXT_DIR, 'Context', 'Companion', 'Blogger', 'Delta.fs'), 'utf8')
  assert.match(bloggerDelta, /200\s*\*\s*1024|200 \* 1024|200L/, 'the 200 KiB input contract is a constant')
  // And the Domain must not compare it to any window: no "window" identifier
  // anywhere in the compression domain files.
  const probe = fs.readFileSync(path.join(NEXT_DIR, 'Context', 'Prefix', 'ProbeSelection.fs'), 'utf8')
  assert.ok(!probe.includes('Window'), 'probe selection must not reference a model window')
  const retryPolicy = fs.readFileSync(path.join(NEXT_DIR, 'Participant', 'Provider', 'Attempt', 'RetryPolicy.fs'), 'utf8')
  assert.ok(!retryPolicy.includes('Window'), 'retry policy must not reference a model window')
})
}
