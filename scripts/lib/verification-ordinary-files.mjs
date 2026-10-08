import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'

export function captureOrdinaryVerificationFiles(root, invalidEntry) {
  const entries = []
  function inspect(relative) {
    const file = path.join(root, relative)
    const stat = fs.lstatSync(file)
    if (stat.isDirectory()) {
      entries.push({ path: relative, type: 'Directory', mode: stat.mode & 0o777 })
      for (const name of fs.readdirSync(file).sort()) inspect(relative === '.' ? name : `${relative}/${name}`)
    } else if (stat.isFile()) {
      const bytes = fs.readFileSync(file)
      entries.push({ path: relative, type: 'File', mode: stat.mode & 0o777, size: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex') })
    } else {
      throw invalidEntry(`Verification requires ordinary owned members: ${relative}`)
    }
  }
  inspect('.')
  return entries
}
