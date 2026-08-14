// tests/integration/persist/dumb-remote.mjs
//
// Dumb Git remote fixture for §12 / §38 / Phase 3.4.
// This module is the "server" side of dumb-server tests: bare repo + git plumbing.
// It MUST NOT import Wanxiang Domain / event codecs / projections — only node + git.

import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

/** Canonical store ref as a plain Git ref name — not decided via Domain codecs. */
export const STORE_REF = 'refs/wanxiang/store'

const git = (cwd, args, env = process.env) => {
  try {
    const stdout = execFileSync('git', cwd ? ['-C', cwd, ...args] : args, {
      encoding: 'utf8',
      env,
      maxBuffer: 64 * 1024 * 1024,
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    return { code: 0, stdout: stdout ?? '', stderr: '' }
  } catch (error) {
    return {
      code: typeof error.status === 'number' ? error.status : 1,
      stdout: error.stdout?.toString?.() ?? '',
      stderr: error.stderr?.toString?.() ?? String(error.message ?? error),
    }
  }
}

const requireOk = (label, result) => {
  if (result.code !== 0) {
    const detail = (result.stderr || result.stdout || '').trim()
    throw new Error(`git ${label} failed (${result.code}): ${detail}`)
  }
  return result
}

/** Create a bare remote + optional named client workspaces under one temp root. */
export const createBareWorkspace = (clientNames = ['client']) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-dumb-'))
  const bare = join(root, 'remote.git')
  requireOk('init --bare', git(null, ['init', '--bare', bare]))

  const clients = {}
  for (const name of clientNames) {
    const path = join(root, name)
    requireOk('init', git(null, ['init', path]))
    requireOk('config email', git(path, ['config', 'user.email', 'dumb-server@test']))
    requireOk('config name', git(path, ['config', 'user.name', 'dumb-server']))
    requireOk('remote add', git(path, ['remote', 'add', 'origin', bare]))
    clients[name] = path
  }

  return {
    root,
    bare,
    clients,
    client: (name = clientNames[0]) => {
      const path = clients[name]
      if (!path) throw new Error(`unknown client workspace: ${name}`)
      return path
    },
    cleanup: () => rmSync(root, { recursive: true, force: true }),
  }
}

/** Observe bare remote store tip via show-ref — OIDs only, no Domain. */
export const readRemoteStoreOid = (barePath) => {
  const result = git(barePath, ['show-ref', '--hash', '--verify', STORE_REF])
  if (result.code !== 0) return null
  const oid = result.stdout.trim()
  return /^[0-9a-f]{40}$/.test(oid) ? oid : null
}

/** Prove an object is present on the bare remote (upload acceptance). */
export const remoteHasObject = (barePath, oid) => {
  const result = git(barePath, ['cat-file', '-e', oid])
  return result.code === 0
}

/** Read object type on bare remote. */
export const remoteObjectType = (barePath, oid) => {
  const result = git(barePath, ['cat-file', '-t', oid])
  if (result.code !== 0) return null
  return result.stdout.trim()
}

/**
 * Lease-push a store tip from a client workspace onto the bare remote.
 * Used to inject concurrent remote advances without Domain / GitGateway.
 */
export const leasePushStore = (clientPath, newOid, expectedOldOid) => {
  const dest = `${newOid}:${STORE_REF}`
  const lease =
    expectedOldOid == null || expectedOldOid === ''
      ? `--force-with-lease=${STORE_REF}:`
      : `--force-with-lease=${STORE_REF}:${expectedOldOid}`
  return git(clientPath, ['push', lease, 'origin', dest])
}

/** Low-level git runner for GitGatewaySyncRunner adapters. */
export const runGitIn = (repoPath, args, envOverlay) => {
  const env = envOverlay ? { ...process.env, ...envOverlay } : process.env
  return git(repoPath, args, env)
}
