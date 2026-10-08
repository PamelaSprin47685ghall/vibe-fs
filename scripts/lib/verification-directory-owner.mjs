import fs from 'node:fs'
import path from 'node:path'

export function allocateVerificationDirectory(parentDirectory, prefix, invalidEntry) {
  const parent = fs.realpathSync(parentDirectory)
  const parentIdentity = fs.lstatSync(parent, { bigint: true })
  if (!parentIdentity.isDirectory()) throw invalidEntry('Verification directory requires a real parent')
  const root = fs.mkdtempSync(path.join(parent, prefix))
  const rootIdentity = fs.lstatSync(root, { bigint: true })
  function assertOwned(allowMissing = false) {
    for (const [directory, identity] of [[parent, parentIdentity], [root, rootIdentity]]) {
      let current
      try {
        current = fs.lstatSync(directory, { bigint: true })
      } catch (cause) {
        if (allowMissing && directory === root && cause.code === 'ENOENT') return false
        throw Object.assign(invalidEntry(`Cannot verify owned directory identity: ${directory}`), { cause })
      }
      if (!current.isDirectory() || current.dev !== identity.dev || current.ino !== identity.ino) throw invalidEntry(`Cannot verify owned directory identity: ${directory}`)
    }
    return true
  }
  return {
    root,
    assertOwned,
    dispose() {
      if (assertOwned(true)) fs.rmSync(root, { recursive: true, force: true })
    },
  }
}
