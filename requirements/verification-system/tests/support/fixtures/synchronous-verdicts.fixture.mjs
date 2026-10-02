import test, { afterEach } from 'node:test'

const pause = new Int32Array(new SharedArrayBuffer(4))

for (let index = 0; index < 8; index++) {
  test(`synchronous work ${index}`, async () => {
    Atomics.wait(pause, 0, 0, 250)
    await Promise.resolve()
  })
}

if (process.env.VERDICT_TRANSPORT_PROBE === 'hang') {
  test('unfinished work after real verdicts', async () => {
    setInterval(() => console.log('unrelated background noise'), 50)
    await new Promise(() => {})
  })
}

if (process.env.VERDICT_TRANSPORT_PROBE === 'after-failure') {
  afterEach((context) => {
    if (context.name === 'synchronous work 7') throw new Error('after hook failure remains visible')
  })
}
