import { mkdirSync, writeFileSync, chmodSync } from 'node:fs'
import { join } from 'node:path'
import { nodeCommand } from './executor.mjs'

export const withSearchLauncher = async (directory, source, action) => {
  const bin = join(directory, '.search-test-bin')
  mkdirSync(bin)
  const launcher = join(bin, 'rg')
  const witness = join(directory, '.search-launcher-entered')
  const entered = `require('node:fs').writeFileSync(${JSON.stringify(witness)}, 'yes');\n`
  writeFileSync(launcher, `#!/bin/sh\nexec ${nodeCommand(entered + source)} -- "$@"\n`)
  chmodSync(launcher, 0o700)
  const previous = process.env.PATH
  process.env.PATH = `${bin}:${previous ?? ''}`
  try {
    return await action()
  } finally {
    if (previous === undefined) delete process.env.PATH
    else process.env.PATH = previous
  }
}
