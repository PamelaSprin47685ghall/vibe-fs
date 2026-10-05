import { setImmediate } from 'node:timers/promises'
import { beforeEach } from 'node:test'

// The previous verdict exists only after its afterEach hooks have finished.
// Yield before starting the next leaf so synchronous work cannot hold that verdict.
beforeEach(context => {
  process.stdout.write(`[verification-test-start] ${JSON.stringify({
    pid: process.pid, parentPid: process.ppid,
    entryFile: process.argv[1], name: context.name, fullName: context.fullName,
  })}\n`)
  return setImmediate()
})
