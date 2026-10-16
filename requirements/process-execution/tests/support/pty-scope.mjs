import { execFile, spawnSync } from 'node:child_process'
import { promisify } from 'node:util'
import { fileURLToPath } from 'node:url'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

export const verifyPtyScope = async (t, mode, verify) => {
  if (spawnSync('bun', ['--version'], { encoding: 'utf8' }).status !== 0) {
    t.skip('Bun is required to run the production PTY backend')
    return
  }
  const helper = fileURLToPath(new URL('../../../managed-session-lifecycle/tests/support/pty-scope-probe.mjs', import.meta.url))
  const { stdout } = await promisify(execFile)('bun', [helper, mode], { timeout: 20000 })
  verify(JSON.parse(stdout.trim()))
}

export const verifyCompiledPtyScope = async (t, original) => {
  const binary = fileURLToPath(new URL(`../../../../node_modules/opencode-${process.platform}-${process.arch}/bin/opencode`, import.meta.url))
  if (!existsSync(binary)) {
    t.skip('The platform OpenCode executable is required for the compiled Host proof')
    return
  }
  const helper = fileURLToPath(new URL('../../../managed-session-lifecycle/tests/support/pty-scope-probe.mjs', import.meta.url))
  const directory = mkdtempSync(join(tmpdir(), 'wxs-compiled-pty-'))
  try {
    await promisify(execFile)(binary, [helper, 'compiled-host', original], {
      env: { ...process.env, BUN_BE_BUN: '1', HOME: directory, XDG_CONFIG_HOME: join(directory, 'config'), XDG_DATA_HOME: join(directory, 'data') },
      timeout: 20000,
    })
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}
