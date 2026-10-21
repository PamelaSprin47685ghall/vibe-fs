import assert from 'node:assert/strict'
const nonce = process.argv[2]
assert.ok(typeof nonce === 'string' && nonce.length > 0)
async function run() {
  let keep = 41
  let buffered = ''
  const messages = []
  const inputEnded = new Promise((resolve, reject) => {
    process.stdin.on('data', chunk => {
      buffered += chunk.toString('utf8')
      while (buffered.includes('\n')) {
        const end = buffered.indexOf('\n')
        messages.push(JSON.parse(buffered.slice(0, end)))
        buffered = buffered.slice(end + 1)
      }
    })
    process.stdin.once('error', reject)
    process.stdin.once('end', resolve)
  })
  process.stdout.write(JSON.stringify({ kind: 'parked', pid: process.pid, parentPid: process.ppid, nonce, keep }) + '\n')
  await inputEnded
  assert.equal(buffered, '')
  assert.deepEqual(messages, [{ id: nonce, value: 'queued-once-汉🔥' }])
  process.stdout.write(JSON.stringify({ kind: 'resumed', pid: process.pid, parentPid: process.ppid, nonce, keep: ++keep, messageCount: messages.length, messages }) + '\n')
}
await run()
