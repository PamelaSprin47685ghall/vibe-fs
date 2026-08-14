#!/usr/bin/env node

import { readFileSync } from 'node:fs'

// This process is launched by Git hooks and must remain independent of the
// OpenCode/Wanxiangshu plugin lifecycle. It imports only the standalone sync
// module compiled into the installed package.
if (process.env.WANXIANG_GIT_SYNC_ACTIVE === '1') {
  process.exit(0)
}

const HookSync = await import(new URL('../../dist/Git/Hook/Sync.js', import.meta.url))
const [kind, arg1] = process.argv.slice(2)

let error = null

switch (kind) {
  case 'pre-push':
    error = await HookSync.runPrePush(arg1 ?? '')
    break
  case 'reference-transaction': {
    const stdin = readFileSync(0, 'utf8')
    error = await HookSync.runReferenceTransaction(arg1 ?? '', stdin)
    break
  }
  default:
    error = `Wanxiang hook runner received unknown hook kind: ${String(kind)}`
    break
}

if (error) {
  console.error(error)
  process.exit(1)
}
