// The *.fixture.mjs suffix is load-bearing: the unit runner discovers
// *.test.mjs, so this file is invisible to the real suite. A parent test
// (requirements/host-boundary/tests/fatal-process-exit.test.mjs) spawns it
// as a real child process to prove the production FatalProcess contract:
// console/report + kill is the single physical exit. See F32.
//
// Modes (via argv[2]):
//   trip            — call the real FatalProcess.trip owner path, then print
//                     an after-marker that must NEVER appear on stdout.
//   diagnostic      — call the real Diagnostic.fatal owner path (same bar).
//   console-throw   — break console.error, then trip: the fuse must still kill.
//   double-trip     — trip twice: the incident must be reported once, then die.
//   pipe-close      — close stdout/stderr fds, then trip: the fuse must still kill.
//   reject          — normal rejection path: print ok and exit 0 (must NOT die).
//   stale-callback  — stale/foreign callback path: print ok and exit 0.
//   exhausted       — repair-exhausted path: print ok and exit 0.
//
// The parent deletes WANXIANGSHU_NO_FATAL_EXIT for fatal modes so the real
// SIGKILL-or-exit(1) path fires, and asserts per-platform semantics itself
// (signal SIGKILL where the platform delivers it, else exit code 1).

const mode = process.argv[2] ?? 'trip'

if (mode === 'reject' || mode === 'stale-callback' || mode === 'exhausted') {
  console.log(`ok-${mode}`)
  process.exit(0)
}

delete process.env.WANXIANGSHU_NO_FATAL_EXIT

if (mode === 'console-throw') {
  console.error = () => { throw new Error('renderer exploded') }
}

if (mode === 'pipe-close') {
  try { process.stdout.destroy() } catch {}
  try { process.stderr.destroy() } catch {}
}

if (mode === 'diagnostic') {
  const { fatal } = await import('../../../../dist/OpenCode/Host/Diagnostic.js')
  console.log('before-fatal')
  fatal('fixture-fatal-diagnostic', [['result', 'fatal must terminate through the diagnostic owner path']])
  console.log('after-fatal')
} else if (mode === 'double-trip') {
  const { trip } = await import('../../../../dist/Foundation/FatalProcess.js')
  console.log('before-fatal')
  trip('fixture-fatal', 'first incident')
  trip('fixture-fatal', 'second incident must never report')
  console.log('after-fatal')
} else {
  const { trip } = await import('../../../../dist/Foundation/FatalProcess.js')
  console.log('before-fatal')
  trip('fixture-fatal', `fatal must terminate even when NODE_TEST_CONTEXT is inherited (mode=${mode})`)
  console.log('after-fatal')
}
