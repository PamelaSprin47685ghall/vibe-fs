import fs from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { registerHooks } from 'node:module'
import { pathToFileURL } from 'node:url'

const receipt = process.env.WXS_MIGRATION_LOAD_RECEIPT
if (!receipt) throw new Error('missing migration load receipt')
const root = resolve(dirname(process.argv[1]), '..')
const frontDoors = new Set([
  pathToFileURL(join(root, 'dist/Persistence/EventStore/Surface.js')).href,
  pathToFileURL(join(root, 'dist/Strength/Surface.js')).href,
])
let sequence = 0
const record = (phase, extra = {}) => {
  if (sequence >= 8) throw new Error('migration load receipt exceeded its finite bound')
  fs.appendFileSync(receipt, `${JSON.stringify({ phase, sequence: sequence++, pid: process.pid, ...extra })}\n`)
}
fs.writeFileSync(receipt, '', { flag: 'wx' })
record('ready', { entry: resolve(process.argv[1]) })
registerHooks({
  load(url, context, nextLoad) {
    const loaded = nextLoad(url, context)
    if (frontDoors.has(url)) record('actual-next-load-complete', { url })
    return loaded
  },
})
process.on('exit', code => record('exit', { code }))
