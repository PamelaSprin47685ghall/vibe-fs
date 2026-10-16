import assert from 'node:assert/strict'
import fs from 'node:fs'
import net from 'node:net'
import { dirname, join } from 'node:path'
import { syncBuiltinESMExports } from 'node:module'
import { setTimeout as delay } from 'node:timers/promises'
import { withSharedAdmittedChildren } from './admitted-child-work.mjs'
import { ToolRuntimeScopeSurface } from '../../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js'
import { ptyRecoveryDrain } from '../../../../dist/Process/Surface.js'

const scenario = process.argv[2]
const missingBun = scenario === 'missing-bun'
const streamLoss = scenario === 'stream-loss'
const until = async (condition, message) => {
  const deadline = Date.now() + 8000
  while (!condition()) {
    assert.ok(Date.now() < deadline, message)
    await delay(10)
  }
}

await withSharedAdmittedChildren(`local-evidence-${scenario}`, 'manager-pty', [
  { agentId: 'devops', sessionId: 'devops-pty', role: 'devops' },
], async (journal, _profiles, workspace) => {
  await ToolRuntimeScopeSurface.startPtyExecution(journal, 'devops-pty', `physical-local-evidence-${scenario}-devops`, 'pty-admission-provider')
  const host = ToolRuntimeScopeSurface.ptyAdmissionProbe(journal, workspace, 'devops-pty', true)
  const originalPath = process.env.PATH
  const originalOn = net.Socket.prototype.on
  const marker = join(workspace, 'launches')
  let endpointDirectory
  let disposalFailure
  try {
    if (missingBun) process.env.PATH = join(workspace, 'no-bun')
    else {
      net.Socket.prototype.on = function (event, listener) {
        if (event !== 'connect') return originalOn.call(this, event, listener)
        const socket = this
        return originalOn.call(this, event, (...args) => {
          if (streamLoss) {
            listener(...args)
            socket.pause()
            setTimeout(() => socket.destroy(), 500)
          } else socket.destroy()
        })
      }
      syncBuiltinESMExports()
    }
    const opened = await host.open(`printf 'launched\\n' >> '${marker}'; printf 'not-observed'; exit 23`)
    assert.equal(opened.ok, true, opened.error)
    const root = join(workspace, '.git', 'wanxiangshu', 'pty')
    await until(() => fs.existsSync(root) && fs.readdirSync(root).length === 1, 'the exact physical request must exist')
    const directory = join(root, fs.readdirSync(root)[0])
    await until(() => fs.existsSync(join(directory, 'terminal')), 'the real owner must seal its terminal evidence')
    if (streamLoss) await delay(600)
    process.env.PATH = originalPath
    net.Socket.prototype.on = originalOn
    syncBuiltinESMExports()
    const request = JSON.parse(fs.readFileSync(join(directory, 'request'), 'utf8'))
    endpointDirectory = dirname(request.endpoint)
    const physical = await ptyRecoveryDrain(directory, request.key, request.incarnation, 20)
    assert.equal(physical.kind, streamLoss ? 'Ended' : 'NotStarted')
    assert.equal(streamLoss ? fs.readFileSync(marker, 'utf8') : fs.existsSync(marker), streamLoss ? 'launched\n' : false)
    const first = await host.stop()
    const second = await host.stop()
    if (streamLoss) {
      assert.equal(first.finished, false)
      assert.match(first.failure, /connection|stream/i)
      assert.equal(second.finished, false)
      assert.deepEqual(host.blockers(), [opened.id])
      const fresh = ToolRuntimeScopeSurface.ptyAdmissionProbe(journal, workspace, 'devops-pty', true)
      const recovered = await fresh.stop()
      assert.equal(recovered.finished, true, recovered.failure)
      await fresh.dispose()
    } else {
      assert.equal(first.finished, true, first.failure)
      assert.equal(second.finished, true, second.failure)
      assert.deepEqual(host.blockers(), [])
      if (missingBun) assert.equal(fs.existsSync(endpointDirectory), false, 'failed custodian spawn must remove its private endpoint directory')
    }
    try { await host.dispose() } catch (error) { disposalFailure = error.message }
    if (!streamLoss) assert.equal(disposalFailure, undefined)
    console.log(JSON.stringify({ scenario, physical: physical.kind, commands: streamLoss ? 1 : 0, first, second, blockers: host.blockers(), disposalFailure }))
  } finally {
    process.env.PATH = originalPath
    net.Socket.prototype.on = originalOn
    syncBuiltinESMExports()
    try { await host.dispose() } catch {}
    if (endpointDirectory && fs.existsSync(endpointDirectory) && fs.readdirSync(endpointDirectory).length === 0) fs.rmdirSync(endpointDirectory)
  }
})
