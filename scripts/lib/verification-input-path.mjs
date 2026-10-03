import fs from 'node:fs'
import path from 'node:path'

export function rejectSymbolicVerificationInput(filePath) {
  const stat = fs.lstatSync(filePath)
  if (stat.isSymbolicLink()) {
    const error = new Error(`Unsealed symbolic verification input: ${filePath}`)
    error.code = 'verification-inputs-symbolic-link'
    error.path = filePath
    throw error
  }
  return stat
}

export function verificationInputExists(root, filePath) {
  let current = root
  try {
    rejectSymbolicVerificationInput(current)
    for (const segment of path.relative(root, filePath).split(path.sep)) {
      current = path.join(current, segment)
      rejectSymbolicVerificationInput(current)
    }
    return true
  } catch (error) {
    if (error.code === 'ENOENT') return false
    throw error
  }
}
