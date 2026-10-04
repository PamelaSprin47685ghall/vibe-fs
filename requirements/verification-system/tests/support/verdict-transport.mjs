import { setImmediate } from 'node:timers/promises'
import { beforeEach } from 'node:test'

// The previous verdict exists only after its afterEach hooks have finished.
// Yield before starting the next leaf so synchronous work cannot hold that verdict.
beforeEach(() => setImmediate())
