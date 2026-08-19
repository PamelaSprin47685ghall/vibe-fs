import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const daemonDir = path.join(root, '.fable-daemon')
const barrierJs = path.join(root, 'dist/FableBarrier.js')
const ackJson = path.join(daemonDir, 'ack.json')
const ackFile = path.join(root, '.fable-ack')

try {
  fs.mkdirSync(daemonDir, { recursive: true })

  let token = 'unknown'
  if (fs.existsSync(barrierJs)) {
    const content = fs.readFileSync(barrierJs, 'utf8')
    const match = content.match(/token\s*=\s*"([^"]+)"/)
    if (match) {
      token = match[1]
    }
  }

  const payload = JSON.stringify({
    token,
    timestamp: Date.now(),
    status: 'ok',
  })

  fs.writeFileSync(ackJson, payload, 'utf8')
  fs.writeFileSync(ackFile, token, 'utf8')
} catch (err) {
  console.error('fable-ack error:', err)
}
