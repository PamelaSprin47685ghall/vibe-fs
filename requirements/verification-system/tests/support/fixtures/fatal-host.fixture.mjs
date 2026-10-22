import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import http from 'node:http'
import { SupervisedProcess } from 'wanxiangli/supervision'

const session = new SupervisedProcess({
  executable: process.execPath,
  args: ['-e', "require('node:fs').watch(process.cwd(), () => {}); process.stdout.write('ready\\n')"],
  cwd: process.cwd(), env: {}, tailBytes: 1024, maxWriteBytes: 1024,
})
const started = await session.started()
assert.equal(started.kind, 'Started')
const guardian = session.inspect().supervisor.pid
const rows = execFileSync('/bin/ps', ['-eo', 'pid=,ppid=,pgid=,stat='], { encoding: 'utf8' })
  .trim().split('\n').map(line => {
    const [pid, parent, group, state] = line.trim().split(/\s+/)
    return { pid: Number(pid), parent: Number(parent), group: Number(group), state }
  })
const root = rows.find(row => row.pid === process.pid)
assert.equal(root.group, process.pid)
assert.equal(rows.find(row => row.pid === guardian).parent, process.pid)
assert.equal(rows.find(row => row.pid === guardian).group, guardian)
assert.equal(rows.find(row => row.pid === started.pid).parent, guardian)
assert.equal(rows.find(row => row.pid === started.pid).group, guardian)
const receipt = { root: root.pid, guardian, target: started.pid }
fs.writeFileSync(process.env.WXS_FATAL_HOST_RECEIPT, JSON.stringify(receipt))

const server = http.createServer((request, response) => {
  if (request.url === '/global/event') {
    response.writeHead(200, { 'content-type': 'text/event-stream' })
    response.write(': connected\n\n')
    return
  }
  response.setHeader('content-type', 'application/json')
  response.end(JSON.stringify(request.url === '/global/health' ? { healthy: true } : {
    home: process.env.HOME, state: process.env.XDG_STATE_HOME, config: process.env.XDG_CONFIG_HOME,
    worktree: process.cwd(), directory: process.cwd(),
  }))
})
server.listen(0, '127.0.0.1', () => {
  process.stdout.write('opencode server listening on http://127.0.0.1:' + server.address().port + '\n')
})
