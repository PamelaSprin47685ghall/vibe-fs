import { createVirtualClock } from './temporal-harness.mjs'
import { Watchdog } from '../e2e/support/watchdog.js'

export function createVirtualTimers(clock) {
  const handles = new Map()
  let nextId = 1
  return {
    schedule(ms, callback) {
      const id = nextId++
      const handle = clock.port.delay(ms)
      handles.set(id, handle)
      handle.delay().then(() => {
        if (handles.delete(id)) callback()
      })
      return id
    },
    cancel(id) {
      const handle = handles.get(id)
      if (handle) {
        handles.delete(id)
        handle.cancel()
      }
    },
  }
}

export function createWatchdogHarness(options = {}) {
  const clock = createVirtualClock()
  const diagnostics = []
  const trace = []
  let terminateCount = 0
  const watchdog = new Watchdog({
    timeoutMs: 500,
    label: 'test-target',
    ...options,
    deps: {
      clock: { nowMs: () => clock.nowMs() },
      timers: createVirtualTimers(clock),
      diagnostic: {
        write(message) {
          diagnostics.push(message)
          trace.push('diagnostic')
          options.deps?.diagnostic?.write?.(message)
        },
      },
      terminate() {
        terminateCount++
        trace.push('terminate')
      },
    },
  })
  return {
    clock, watchdog, diagnostics, trace,
    get terminated() { return terminateCount > 0 },
    get terminateCount() { return terminateCount },
    async advance(ms) {
      clock.advance(ms)
      for (let index = 0; index < 12; index++) await Promise.resolve()
    },
  }
}
