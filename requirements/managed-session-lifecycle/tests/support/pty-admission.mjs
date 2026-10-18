import assert from 'node:assert/strict'
import { fork } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { withSharedAdmittedChildren } from './admitted-child-work.mjs'
import { ToolRuntimeScopeSurface } from '../../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js'

export const withAdmissionWorkers = (label, action, { realBackend = false, runtime = realBackend ? 'bun' : undefined } = {}) => withSharedAdmittedChildren(
  label, 'manager-pty', [{ agentId: 'devops', sessionId: 'devops-pty', role: 'devops' }],
  async (journal, _profiles, workspace) => {
    await ToolRuntimeScopeSurface.startPtyExecution(journal, 'devops-pty', `physical-${label}-devops`, 'pty-admission-provider')
    const workers = []
    const start = () => new Promise((resolve, reject) => {
      const process = fork(fileURLToPath(new URL(realBackend ? './pty-admission-real-worker.mjs' : './pty-admission-worker.mjs', import.meta.url)), [workspace, 'devops-pty', `physical-${label}-devops`], {
        stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
        execArgv: [],
        ...(runtime ? { execPath: runtime } : {}),
      })
      let stderr = ''
      process.stderr.on('data', chunk => { stderr += chunk })
      const pending = new Map()
      let sequence = 0
      const barriers = new Map()
      const barrierWaiters = new Map()
      const worker = {
        process,
        call(action, command, extra = {}) {
          const id = ++sequence
          return new Promise((resolveCall, rejectCall) => {
            pending.set(id, { resolve: resolveCall, reject: rejectCall })
            process.send({ id, action, command, ...extra })
          })
        },
        barrier(name) {
          if (barriers.has(name)) return Promise.resolve(barriers.get(name))
          return new Promise((resolveBarrier, rejectBarrier) => barrierWaiters.set(name, { resolve: resolveBarrier, reject: rejectBarrier }))
        },
      }
      workers.push(worker)
      process.on('message', message => {
        if (message.ready) return resolve(worker)
        if (message.barrier) {
          barriers.set(message.barrier, message)
          barrierWaiters.get(message.barrier)?.resolve(message)
          return
        }
        const request = pending.get(message.id)
        if (!request) return
        pending.delete(message.id)
        if (Object.hasOwn(message, 'error')) {
          const error = new Error(message.error ?? 'PTY worker failed without a diagnostic')
          request.reject(error)
          for (const waiter of barrierWaiters.values()) waiter.reject(error)
        }
        else request.resolve(message.value)
      })
      process.on('error', reject)
      process.on('exit', code => {
        const error = new Error(`PTY worker exited ${code}: ${stderr}`)
        reject(error)
        for (const request of pending.values()) request.reject(error)
        for (const waiter of barrierWaiters.values()) waiter.reject(error)
      })
    })
    try {
      const producer = await start()
      const stopper = await start()
      assert.notEqual(producer.process.pid, stopper.process.pid)
      return await action(producer, stopper, workspace, start)
    } finally {
      await Promise.all(workers.map(worker => new Promise(resolve => {
        if (worker.process.exitCode !== null || worker.process.signalCode !== null) return resolve()
        worker.process.once('exit', resolve)
        if (worker.process.connected) worker.process.disconnect()
      })))
    }
  },
)
