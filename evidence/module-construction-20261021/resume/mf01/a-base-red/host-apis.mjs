// Real scratch Host filesystem, not production authorization or ledger proof.
import * as fs from 'node:fs'
import * as path from 'node:path'

export function hostApis(directory, options = {}) {
  const records = []
  const resolve = value => {
    if (typeof value !== 'string') throw new Error('PATH_REQUIRED')
    const target = path.resolve(directory, value)
    if (!target.startsWith(directory + path.sep)) throw new Error('PATH_DENIED')
    return target
  }
  return {
    records,
    async invoke(operation, args) {
      if (operation === 'write') {
        const file = resolve(args[0])
        if (typeof args[1] !== 'string') throw new Error('TEXT_REQUIRED')
        fs.writeFileSync(file, args[1], 'utf8')
        records.push({ operation, path: args[0], bytes: Buffer.byteLength(args[1]), accepted: true })
        return { ok: true, bytes: Buffer.byteLength(args[1]) }
      }
      if (operation === 'cat') {
        const file = resolve(args[0])
        records.push({ operation, path: args[0], accepted: true })
        if (options.catDelayMs) await new Promise(resolve => setTimeout(resolve, options.catDelayMs))
        return fs.promises.readFile(file, 'utf8')
      }
      if (operation === 'rm') {
        fs.unlinkSync(resolve(args[0]))
        records.push({ operation, path: args[0], accepted: true })
        return { ok: true }
      }
      throw new Error('UNKNOWN_OPERATION')
    },
  }
}
