// Split from tests/unit/execution/handle.test.mjs (cutover Wave 2a);
// owner: behavior-diagnosis. Enforcer continuation 协调面：stopPhysicalRun 参数序
// （messages → fallback → reason）与 ctx.Stop 注入；continuation 分支不得
// 直连 stopPhysicalRun（behavior-diagnosis HOW §3.3 Enforcer 协调）。

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

test('WHAT[BD-017] ENFORCER_stopPhysicalRun_argument_order_is_messages_then_fallback', () => {
  // Definition: stopPhysicalRun (messages) (fallback) (reason).
  // Call sites must pass (rawMessages, fallback, reason), not swapped.
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const continuation = readFileSync(join(root, 'src/Wanxiangshu/Enforcer/Continuation.fs'), 'utf8')

  assert.match(
    continuation,
    /let private stopPhysicalRun\s*\(messages: obj list\)\s*\(fallback: obj list\)\s*\(reason: string\)/,
    'definition order is messages, fallback, reason',
  )
  // The only remaining direct call site is the ctx.Stop injection in mkCtx
  // (first arg rawMessages on both sides); continuation branches go through
  // ctx.Stop and must not re-call stopPhysicalRun directly.
  const calls = [...continuation.matchAll(/stopPhysicalRun\s+(\w+)\s+(\w+)\s+/g)].map((m) => [
    m[1],
    m[2],
  ])
  assert.ok(calls.length >= 1, `expected injection call site, got ${calls.length}`)
  for (const [first, second] of calls) {
    assert.equal(
      first,
      'rawMessages',
      `stopPhysicalRun first arg must be rawMessages, got ${first} ${second}`,
    )
  }
})
