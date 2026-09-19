import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const frames = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");

const entry = ({ epoch = 0, previous = 0, next = 1, previousCutoff = 0, nextCutoff = 1, run = 'run-1' } = {}) => ({
  epoch,
  previous,
  next,
  previousCutoff,
  nextCutoff,
  digest: `digest-${run}`,
  frame: frames.frame({ kind: 'Entry', digest: `digest-${run}`, ref: `blob-${run}`, coveredFrom: previous, coveredThrough: next }),
})
const apply = (state, request) => {
  const result = frames.applyEntry(request, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const stopReason = (reason) => {
  assert.equal(typeof reason, 'string')
  return reason
}

test('WHAT[context-compression-023] ENFORCER_park_never_expires_without_an_event', async () => {
  const scope = runtime.scope()
  const parked = runtime.park(scope, 'ses-blog')
  assert.equal(runtime.offerParked(scope, 'ses-blog', runtime.main({ toml: 'fresh' })), 'Delivered')
  const wake = await parked
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'fresh')
  runtime.dispose(scope)
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");

const ROOT = new URL('../../../', import.meta.url).pathname
const main = (toml = 'delta-1') => runtime.main({ toml })

test('WHAT[context-compression-023] CTX_023_park_has_no_clock_or_timeout_dependency', () => {
  const parked = readFileSync(
    `${ROOT}src/Wanxiangshu/Context/Companion/Blogger/Runtime/ParkedTransform.fs`,
    'utf8',
  )
  const scope = readFileSync(
    `${ROOT}src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs`,
    'utf8',
  )

  for (const [name, text] of [['ParkedTransform', parked], ['PluginScope', scope]]) {
    assert.doesNotMatch(text, /TimeSpan|ITimerPort|nodeTimerPort|\.Delay\b|deadline|timeout/i, `${name} must be time-independent`)
  }
})
}
