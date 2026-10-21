import { writeFileSync } from 'node:fs'
import { spawn } from 'node:child_process'
import { once } from 'node:events'
import { createHash } from 'node:crypto'
import { setTimeout as delay } from 'node:timers/promises'
import { NodeTime } from 'wanxiangli/time-node'

const [mode, directory, ending] = process.argv.slice(2)
const unit = Buffer.from('\uFEFF中🧭\r\n')
const burst = Buffer.alloc(3 * 1024 * 1024)
for (let offset = 0; offset < burst.length; offset += unit.length) unit.copy(burst, offset)
const write = async (stream, bytes) => {
  if (!stream.write(bytes)) await once(stream, 'drain')
}

if (mode === 'keeper') {
  const before = performance.now()
  const exitWon = await NodeTime.raceExitKeepingAlive(new Promise(() => {}), 4 * 60 * 60 * 1000)
  process.stdout.write(JSON.stringify({ exitWon, elapsedMs: performance.now() - before }) + '\n')
} else if (mode === 'descendant') {
  process.on('SIGTERM', () => {
    process.stderr.write('DESCENDANT_TERM\r\n', () => writeFileSync(directory + '/term-seen', 'seen'))
  })
  await write(process.stdout, Buffer.from('DESCENDANT_READY\r\n'))
  process.send('ready')
  setInterval(() => {}, 60000)
} else if (mode === 'paced') {
  writeFileSync(directory + '/writer.ready', String(process.pid))
  for (const channel of ['stdout', 'stderr']) {
    const header = Buffer.from(`\uFEFFSTREAM:${channel}\r\n`)
    await write(process[channel], header.subarray(0, 1))
    await write(process[channel], header.subarray(1, 2))
    await write(process[channel], header.subarray(2))
  }
  await Promise.all(['stdout', 'stderr'].map(async channel => {
    for (let offset = 0; offset < burst.length; offset += 65521) {
      await write(process[channel], burst.subarray(offset, Math.min(offset + 65521, burst.length)))
    }
  }))
  writeFileSync(directory + '/writer.burst-done', 'done')
  let wakeInput = null
  const inputHash = createHash('sha256')
  let inputBytes = 0
  let finished = false
  const input = (async () => {
    for await (const bytes of process.stdin) {
      inputHash.update(bytes)
      inputBytes += bytes.length
    }
    finished = true
    wakeInput?.()
  })()
  let frames = 0
  while (!finished) {
    for (const channel of ['stdout', 'stderr']) {
      await write(process[channel], Buffer.from(`\uFEFF${channel}|${String(frames).padStart(6, '0')}|中🧭\r\n`))
    }
    frames += 1
    await new Promise(resolve => {
      const timer = setTimeout(() => { wakeInput = null; resolve() }, 10000)
      wakeInput = () => { clearTimeout(timer); wakeInput = null; resolve() }
      if (finished) wakeInput()
    })
  }
  await input
  let descendant = null
  if (ending === 'descendant') {
    const child = spawn(process.execPath, [process.argv[1], 'descendant', directory], {
      detached: false, stdio: ['ignore', 'inherit', 'inherit', 'ipc'], env: {},
    })
    await once(child, 'message')
    descendant = child.pid
  }
  writeFileSync(directory + '/witness.json', JSON.stringify({ frames, inputBytes, inputHash: inputHash.digest('hex'), descendant }))
  process.exit(ending === 'descendant' ? 23 : 0)
} else {
  throw Error('Unknown worker mode')
}
