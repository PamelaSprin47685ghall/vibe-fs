import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { appendFileSync, writeFileSync } from 'node:fs'
import { spawn } from 'node:child_process'
import { once } from 'node:events'

const [mode, directory] = process.argv.slice(2)
const write = text => new Promise((resolve, reject) => process.stdout.write(text, error => error ? reject(error) : resolve()))
if (mode === 'descendant') {
  process.on('SIGTERM', () => {
    appendFileSync(directory + '/descendant-signals.jsonl', JSON.stringify({ signal: 'SIGTERM', pid: process.pid, at: new Date().toISOString() }) + '\n')
  })
  process.on('SIGHUP', () => {})
  await write(`DESC_READY|${process.pid}\r\n`)
  process.send('ready')
  setInterval(() => {}, 60000)
} else {
  assert.equal(mode, 'target')
  assert.equal(Boolean(process.stdin.isTTY && process.stdout.isTTY && process.stderr.isTTY), true)
  const input = createHash('sha256')
  const decoder = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true })
  let pending = ''
  let count = 0
  await write(`READY|${process.pid}|tty\r\n`)
  for await (const bytes of process.stdin) {
    input.update(bytes)
    pending += decoder.decode(bytes, { stream: true })
    while (pending.includes('\n')) {
      const end = pending.indexOf('\n')
      const line = pending.slice(0, end)
      pending = pending.slice(end + 1)
      const request = JSON.parse(line)
      assert.equal(request.sequence, count)
      const value = Buffer.from(request.value, 'utf8')
      const hash = createHash('sha256').update(value).digest('hex')
      await write(`ACK|${count}|${value.length}|${hash}|${value.toString('base64')}\r\n`)
      count += 1
      appendFileSync(directory + '/target-input.jsonl', JSON.stringify({ sequence: request.sequence, bytes: value.length, hash }) + '\n')
    }
  }
  pending += decoder.decode()
  assert.equal(pending, '')
  const digest = input.digest('hex')
  await write(`EOF|${count}|${digest}\r\n`)
  const child = spawn(process.execPath, [process.argv[1], 'descendant', directory], { detached: false, stdio: ['ignore', 'inherit', 'inherit', 'ipc'], env: {} })
  await once(child, 'message')
  writeFileSync(directory + '/target-eof.json', JSON.stringify({ pid: process.pid, descendant: child.pid, count, inputHash: digest, at: new Date().toISOString() }))
  process.exit(23)
}
