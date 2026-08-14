// A test that never finishes while printing, held open by a live handle.
//
// The `*.fixture.mjs` suffix is load-bearing: `runner.mjs` discovers `*.test.mjs`, so this file is
// invisible to the real suite. Rename it and the suite hangs.
//
// Two degradations turn this red at once:
//
//   disconnect the verdict feed  → nothing renews, nothing fires, the run parks to the backstop
//   feed `test:stdout` as blocking → `tick` renews forever, the run parks to the backstop
//
// So one fixture proves both that the heartbeat is wired and that it is wired to the right signal.
import test from 'node:test'

test('hangs while chattering', async () => {
  setInterval(() => console.log('tick'), 50)
  await new Promise(() => {})
})
