import { run } from 'node:test'
import { inspect } from 'node:util'

const [file, loader] = process.argv.slice(2)
const stream = run({
  files: [file],
  concurrency: 1,
  execArgv: ['--experimental-loader', loader],
  testNamePatterns: ['actual admitted retry candidate'],
})
let failures = 0
stream.on('test:complete', data => {
  if (!data.details.passed) failures += 1
  process.stdout.write(JSON.stringify({
    type: 'complete', name: data.name, file: data.file, runEntry: file,
    passed: data.details.passed, skipped: data.skip !== undefined, todo: data.todo !== undefined,
    error: inspect(data.details.error),
  }) + '\n')
})
for await (const _event of stream) {}
process.stdout.write(JSON.stringify({ type: 'drained' }) + '\n')
process.exitCode = failures > 0 ? 1 : 0
