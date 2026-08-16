// CTX-001 — 不观察容量。
//
// The plugin must never read/query/derive/cache any model context-window size,
// and must not contain tokenizer / byte→token conversion. This is a tombstone
// scan: the estimator that used these synonyms was deleted (X9), and any new
// occurrence in production source trips the test before it can reach a probe or
// squash decision.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const NEXT_DIR = path.join(ROOT, 'src', 'Wanxiangshu')

function sourceFiles(dir) {
  const out = []
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) out.push(...sourceFiles(full))
    else if (entry.name.endsWith('.fs')) out.push(full)
  }
  return out
}

const files = sourceFiles(NEXT_DIR)

test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_forbidden_capacity_synonyms_never_appear_in_production_source', () => {
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
  const slot = fs.readFileSync(path.join(NEXT_DIR, 'Participant', 'Provider', 'Attempt', 'RecoverySlot.fs'), 'utf8')
  assert.ok(!slot.includes('Window'), 'recovery slot must not reference a model window')
})
