import { setImmediate } from 'node:timers/promises'
import { afterEach } from 'node:test'

// Completed leaves must let Node's reporter flush before the next microtask chain.
// This emits no progress and cannot run while a leaf remains unfinished.
afterEach(() => setImmediate())
