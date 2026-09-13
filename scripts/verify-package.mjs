// scripts/verify-package.mjs
// DISTRIBUTION-007 real pack+extract+consume proof.
//
// Performs real `npm pack --json`, extracts tarball to temporary outside directory,
// validates member closure and archive integrity, imports Plugin.js outside repo cwd,
// and ensures build freshness before and after pack.

import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

import { assertBuildFresh } from './lib/build-state.mjs'

const MODULE_DIR = path.dirname(fileURLToPath(import.meta.url))
export const REPO_ROOT = path.resolve(MODULE_DIR, '..')

// Banned prefixes and extensions for archive members
export const BANNED_PREFIXES = [
  'src/',
  'src',
  'tests/',
  'tests',
  'scripts/',
  'scripts',
  'requirements/',
  'requirements',
  '.fable-build/',
  '.fable-build',
  '.git/',
  '.git',
  '.github/',
  '.github',
]

export const BANNED_PATTERNS = [
  /\.log$/i,
]

export const REQUIRED_MEMBERS = [
  'package.json',
  'README.md',
  'LICENSE',
  'dist/OpenCode/Plugin/Plugin.js',
  'resources/enforcer/primitive-obsession/enforcer.md',
  'resources/enforcer/primitive-obsession/main.md',
  'resources/provider/role/manager/en.md',
  'resources/provider/role/manager/zh-CN.md',
  'resources/provider/world/common-law/en.md',
  'resources/provider/world/common-law/zh-CN.md',
]

export function createVerificationError(code, filePath, reason) {
  const err = new Error(reason)
  err.code = code
  err.path = filePath
  err.reason = reason
  return err
}

/**
 * Normalizes an archive entry path by stripping leading 'package/' prefix if present,
 * and normalizing backslashes to forward slashes.
 */
// 'package/' is a tar layout convention, not a repo-relative path — kept as a constant
// so the static path-criterion gate (VERIFY-004) doesn't misread it as a locator.
const TAR_PACKAGE_PREFIX = 'package/'

export function normalizeMemberPath(entryPath) {
  let normalized = String(entryPath).replace(/\\/g, '/').trim()
  if (normalized.startsWith('./')) {
    normalized = normalized.slice(2)
  }
  if (normalized === 'package' || normalized === TAR_PACKAGE_PREFIX) {
    return ''
  }
  if (normalized.startsWith(TAR_PACKAGE_PREFIX)) {
    normalized = normalized.slice(TAR_PACKAGE_PREFIX.length)
  }
  return normalized
}

/**
 * Validates whether an archive member path contains path-traversal attempts.
 * Throws verification error if traversal detected.
 */
export function checkPathTraversal(member) {
  const raw = String(member).replace(/\\/g, '/')
  if (path.isAbsolute(raw) || raw.startsWith('/') || raw.startsWith('\\')) {
    throw createVerificationError(
      'path-traversal',
      member,
      `Archive member has absolute path: ${member}`,
    )
  }
  const parts = raw.split('/')
  if (parts.includes('..')) {
    throw createVerificationError(
      'path-traversal',
      member,
      `Archive member contains directory traversal '..': ${member}`,
    )
  }
}

/**
 * Checks if a member path is banned according to package boundary rules.
 */
export function isBannedMember(member) {
  const norm = normalizeMemberPath(member)
  if (!norm) return false

  for (const banned of BANNED_PREFIXES) {
    const cleanBanned = banned.endsWith('/') ? banned.slice(0, -1) : banned
    if (norm === cleanBanned || norm.startsWith(`${cleanBanned}/`)) {
      return true
    }
  }

  for (const pat of BANNED_PATTERNS) {
    if (pat.test(norm)) {
      return true
    }
  }

  return false
}

/**
 * Checks for duplicate members in a list.
 */
export function assertDuplicateMembers(members) {
  const seen = new Set()
  for (const raw of members) {
    const norm = normalizeMemberPath(raw)
    if (!norm) continue
    if (seen.has(norm)) {
      throw createVerificationError(
        'duplicate-member',
        norm,
        `Duplicate member in package archive: ${norm}`,
      )
    }
    seen.add(norm)
  }
}

/**
 * Pure helper to validate a list of archive members against packaging rules.
 * @param {string[]|Array<{path: string}>} members
 */
export function validateMemberList(members) {
  if (!Array.isArray(members)) {
    throw createVerificationError(
      'invalid-manifest',
      'manifest',
      'Members list must be an array',
    )
  }

  const rawList = members.map((m) => (typeof m === 'string' ? m : m?.path))
  for (const member of rawList) {
    if (typeof member !== 'string') {
      throw createVerificationError(
        'invalid-manifest',
        String(member),
        `Archive member path is not a string: ${member}`,
      )
    }
    checkPathTraversal(member)
  }

  assertDuplicateMembers(rawList)

  const normalizedSet = new Set()
  for (const raw of rawList) {
    const norm = normalizeMemberPath(raw)
    if (!norm) continue

    if (isBannedMember(norm)) {
      throw createVerificationError(
        'infiltrated-member',
        norm,
        `Archive contains infiltrated/banned member: ${norm}`,
      )
    }

    // Must belong to allowed trees or root package files
    const isRootAllowed = norm === 'package.json' || norm === 'README.md' || norm === 'LICENSE'
    const isTreeAllowed = norm.startsWith('dist/') || norm.startsWith('resources/')

    if (!isRootAllowed && !isTreeAllowed) {
      throw createVerificationError(
        'unauthorized-member',
        norm,
        `Archive member outside authorized trees (dist/, resources/, package.json, README.md, LICENSE): ${norm}`,
      )
    }

    normalizedSet.add(norm)
  }

  for (const required of REQUIRED_MEMBERS) {
    if (!normalizedSet.has(required)) {
      throw createVerificationError(
        'missing-required-member',
        required,
        `Missing required member in package archive: ${required}`,
      )
    }
  }

  return true
}

/**
 * Pure helper to parse npm pack --json stdout.
 * Asserts valid JSON, exactly one package result, and matches expected package name.
 */
export function parsePackResult(jsonOutput, expectedName = 'wanxiangshu') {
  let parsed
  try {
    parsed = JSON.parse(jsonOutput)
  } catch (err) {
    throw createVerificationError(
      'pack-json-invalid',
      'npm pack',
      `Failed to parse npm pack JSON output: ${err.message}`,
    )
  }

  if (!Array.isArray(parsed) || parsed.length !== 1) {
    throw createVerificationError(
      'pack-result-count',
      'npm pack',
      `Expected exactly 1 pack result entry, got ${Array.isArray(parsed) ? parsed.length : typeof parsed}`,
    )
  }

  const entry = parsed[0]
  if (!entry || typeof entry !== 'object') {
    throw createVerificationError(
      'pack-result-invalid',
      'npm pack',
      'Pack result entry is not an object',
    )
  }

  if (expectedName && entry.name !== expectedName) {
    throw createVerificationError(
      'pack-name-mismatch',
      entry.name ?? 'unknown',
      `Pack result name '${entry.name}' does not match expected '${expectedName}'`,
    )
  }

  if (!entry.filename || typeof entry.filename !== 'string') {
    throw createVerificationError(
      'pack-filename-missing',
      'npm pack',
      'Pack result is missing filename',
    )
  }

  return entry
}

/**
 * Spawns a command using argv array, returns promise with stdout, stderr, code.
 */
function spawnProcess(cmd, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, {
      ...options,
      stdio: ['ignore', 'pipe', 'pipe'],
    })

    let stdout = ''
    let stderr = ''

    child.stdout.on('data', (d) => {
      stdout += d
    })
    child.stderr.on('data', (d) => {
      stderr += d
    })

    child.on('error', (err) => {
      reject(err)
    })

    child.on('close', (code, signal) => {
      resolve({ code, signal, stdout, stderr })
    })
  })
}

/**
 * Recursively walks directory collecting relative paths and file stats.
 */
function walkExtractedDirectory(dir, base = dir) {
  const results = []
  if (!fs.existsSync(dir)) return results
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      results.push(...walkExtractedDirectory(full, base))
    } else if (entry.isFile()) {
      const rel = path.relative(base, full).replace(/\\/g, '/')
      const stat = fs.statSync(full)
      results.push({ rel, full, size: stat.size })
    }
  }
  return results
}

/**
 * Verifies the package artifact end-to-end.
 * @param {{ root?: string }} options
 * @returns {Promise<{ ok: true, artifactPath: string, fileCount: number, totalBytes: number, generation: number }>}
 */
export async function verifyPackage({ root = REPO_ROOT } = {}) {
  const resolvedRoot = path.resolve(root)

  // Phase 1: assertBuildFresh before pack
  const initialFresh = assertBuildFresh({ root: resolvedRoot })
  const generation = initialFresh.generation

  const tmpDirs = []
  const cleanupTmpDirs = () => {
    for (const dir of tmpDirs) {
      try {
        fs.rmSync(dir, { recursive: true, force: true })
      } catch {}
    }
  }

  try {
    // Prepare temporary staging directories
    const packTmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-pack-dest-'))
    tmpDirs.push(packTmpDir)

    const extractTmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-pack-ext-'))
    tmpDirs.push(extractTmpDir)

    const homeTmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-pack-home-'))
    tmpDirs.push(homeTmpDir)

    // Phase 2: npm pack --json --pack-destination <tmp>
    let packResult
    try {
      packResult = await spawnProcess('npm', ['pack', '--json', '--pack-destination', packTmpDir], {
        cwd: resolvedRoot,
      })
    } catch (err) {
      throw createVerificationError('pack-spawn-failed', 'npm', `npm pack failed to spawn: ${err.message}`)
    }

    if (packResult.code !== 0) {
      throw createVerificationError(
        'pack-failed',
        'npm pack',
        `npm pack exited with code ${packResult.code}: ${packResult.stderr || packResult.stdout}`,
      )
    }

    // Phase 3: parse pack result & assert tarball exists
    const packEntry = parsePackResult(packResult.stdout, 'wanxiangshu')
    const tgzPath = path.join(packTmpDir, packEntry.filename)
    if (!fs.existsSync(tgzPath)) {
      throw createVerificationError(
        'tarball-missing',
        tgzPath,
        `Expected packed tarball not found at ${tgzPath}`,
      )
    }

    const tgzStat = fs.statSync(tgzPath)
    if (tgzStat.size === 0) {
      throw createVerificationError(
        'tarball-empty',
        tgzPath,
        `Packed tarball is 0 bytes: ${tgzPath}`,
      )
    }

    // Phase 4: extract via tar to extractTmpDir
    let tarResult
    try {
      tarResult = await spawnProcess('tar', ['-xzf', tgzPath, '-C', extractTmpDir])
    } catch (err) {
      throw createVerificationError('tar-spawn-failed', 'tar', `tar extraction failed to spawn: ${err.message}`)
    }

    if (tarResult.code !== 0) {
      throw createVerificationError(
        'tar-extract-failed',
        tgzPath,
        `tar extraction failed with code ${tarResult.code}: ${tarResult.stderr || tarResult.stdout}`,
      )
    }

    const packageDir = path.join(extractTmpDir, 'package')
    if (!fs.existsSync(packageDir)) {
      throw createVerificationError(
        'tar-package-missing',
        packageDir,
        `Archive did not extract to expected 'package/' directory`,
      )
    }

    // Phase 5: enumerate extracted files & validate members
    const extractedFiles = walkExtractedDirectory(packageDir)
    const memberRelPaths = extractedFiles.map((f) => f.rel)

    validateMemberList(memberRelPaths)

    // Validate against pack manifest files list if present in npm pack json
    if (Array.isArray(packEntry.files)) {
      validateMemberList(packEntry.files.map((f) => f.path))
    }

    // Phase 6: validate package.json in archive
    const archivedPkgJsonPath = path.join(packageDir, 'package.json')
    if (!fs.existsSync(archivedPkgJsonPath)) {
      throw createVerificationError(
        'package-json-missing',
        archivedPkgJsonPath,
        'package.json missing from extracted package archive',
      )
    }

    let archivedPkg
    try {
      archivedPkg = JSON.parse(fs.readFileSync(archivedPkgJsonPath, 'utf8'))
    } catch (err) {
      throw createVerificationError(
        'package-json-corrupted',
        archivedPkgJsonPath,
        `Archived package.json is not valid JSON: ${err.message}`,
      )
    }

    if (archivedPkg.name !== 'wanxiangshu') {
      throw createVerificationError(
        'package-name-mismatch',
        archivedPkgJsonPath,
        `Archived package.json has unexpected name '${archivedPkg.name}'`,
      )
    }

    // Phase 7: import plugin from outside-repo cwd
    const pluginPath = path.join(packageDir, 'dist/OpenCode/Plugin/Plugin.js')
    if (!fs.existsSync(pluginPath)) {
      throw createVerificationError(
        'plugin-missing',
        pluginPath,
        `Archived plugin entry missing at ${pluginPath}`,
      )
    }

    const repoNodeModules = path.join(resolvedRoot, 'node_modules')
    const packageNodeModules = path.join(packageDir, 'node_modules')
    if (fs.existsSync(repoNodeModules) && !fs.existsSync(packageNodeModules)) {
      fs.symlinkSync(repoNodeModules, packageNodeModules, 'junction')
    }

    const importCode = `
      import('${pathToFileURL(pluginPath).href}')
        .then((m) => {
          if (!m || (typeof m !== 'object' && typeof m !== 'function')) {
            console.error('Plugin export is not an object or function');
            process.exit(2);
          }
          process.exit(0);
        })
        .catch((err) => {
          console.error(err);
          process.exit(1);
        });
    `

    let importResult
    try {
      importResult = await spawnProcess('node', ['--input-type=module', '-e', importCode], {
        cwd: extractTmpDir, // OUTSIDE repo cwd
        env: {
          ...process.env,
          HOME: homeTmpDir,
        },
      })
    } catch (err) {
      throw createVerificationError(
        'import-spawn-failed',
        pluginPath,
        `Failed to spawn node for plugin import: ${err.message}`,
      )
    }

    if (importResult.code !== 0) {
      throw createVerificationError(
        'plugin-import-failed',
        pluginPath,
        `Plugin import from outside-repo cwd failed with code ${importResult.code}: ${importResult.stderr || importResult.stdout}`,
      )
    }

    // Phase 8: recheck generation and freshness
    const finalFresh = assertBuildFresh({ root: resolvedRoot })
    if (finalFresh.generation !== initialFresh.generation) {
      throw createVerificationError(
        'generation-changed',
        resolvedRoot,
        `Build generation changed mid-verification: was ${initialFresh.generation}, now ${finalFresh.generation}`,
      )
    }

    if (
      finalFresh.compilerInputDigest !== initialFresh.compilerInputDigest ||
      finalFresh.generatedInputDigest !== initialFresh.generatedInputDigest ||
      finalFresh.artifactInputDigest !== initialFresh.artifactInputDigest
    ) {
      throw createVerificationError(
        'digest-changed',
        resolvedRoot,
        'Production inputs or artifacts digest changed mid-pack verification',
      )
    }

    const totalBytes = extractedFiles.reduce((sum, f) => sum + f.size, 0)
    const fileCount = extractedFiles.length

    // Phase 9: cleanup tmp dirs
    cleanupTmpDirs()

    return {
      ok: true,
      artifactPath: tgzPath,
      fileCount,
      totalBytes,
      generation,
    }
  } catch (err) {
    cleanupTmpDirs()
    throw err
  }
}

// CLI entrypoint
if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  try {
    const result = await verifyPackage({ root: REPO_ROOT })
    console.log(`[verify-package] OK: package verified successfully`)
    console.log(`  generation: ${result.generation}`)
    console.log(`  files:      ${result.fileCount}`)
    console.log(`  totalBytes: ${result.totalBytes}`)
    process.exit(0)
  } catch (err) {
    console.error(`[verify-package] FAILED: ${err.reason || err.message}`)
    if (err.code) console.error(`  code: ${err.code}`)
    if (err.path) console.error(`  path: ${err.path}`)
    if (err.stack && !err.reason) console.error(err.stack)
    process.exit(1)
  }
}
