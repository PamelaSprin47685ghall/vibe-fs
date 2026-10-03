import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { execFileSync } from 'node:child_process'

import { loopDetectorRepositoryInputFiles } from './loop-detector-repository-corpus.mjs'
import { collectTrackedInputs, computeFileHash } from './owner-compile.mjs'

const MODULE_DIR = path.dirname(fileURLToPath(import.meta.url))
export const REPO_ROOT = path.resolve(MODULE_DIR, '../..')
export const MANIFEST_SCHEMA = 'build-manifest-v1'
export const DEFAULT_MANIFEST_REL = '.fable-build/build-manifest.json'

function norm(filePath) {
  return path.resolve(filePath).replace(/\\/g, '/')
}

function relPath(root, filePath) {
  return path.relative(root, filePath).replace(/\\/g, '/')
}

export function computeDigest(entries) {
  const sorted = [...entries].sort((a, b) => a.path.localeCompare(b.path))
  const hasher = crypto.createHash('sha256')
  for (const entry of sorted) {
    const hash = entry.sha256 ?? entry.hash
    if (entry.loopDetectorCorpus) hasher.update('loop-detector-corpus\0')
    hasher.update(`${entry.path}:${hash}\n`)
  }
  return hasher.digest('hex')
}

const REQUIRED_VERIFICATION_DIRS = ['src', 'scripts', 'requirements', 'resources']
const OPTIONAL_VERIFICATION_DIRS = ['.github', '.config']
const SKIP_DIR_NAMES = new Set([
  'node_modules',
  '.git',
  'dist',
  '.fable-build',
  'artifacts',
  'coverage',
  '.wireit',
  'bin',
  'obj',
  '.reasonix',
  '.nuget',
])

function isIgnoredVerificationFile(fileName) {
  // `.git` 目录形态由 SKIP_DIR_NAMES 排除；worktree/gitfile 布局下它是根
  // 文件，同样不属于验证输入闭包。快照物化会把它写为指向原仓库的运行
  // 环境装置；若被收集，嵌套 verify 的物化将因复制后覆盖 gitfile 而触发
  // preparation-mismatch，故两种形态一律排除。
  if (fileName === '.git') return true
  if (fileName === '.env.example') return false
  if (fileName.startsWith('.env')) return true
  if (fileName.endsWith('.pem') || fileName.endsWith('.key') || fileName.endsWith('.tgz')) return true
  return false
}

export function collectVerificationInputs(root = REPO_ROOT) {
  const resolvedRoot = path.resolve(root)

  for (const dirName of REQUIRED_VERIFICATION_DIRS) {
    const dirPath = path.join(resolvedRoot, dirName)
    if (!fs.existsSync(dirPath) || !fs.statSync(dirPath).isDirectory()) {
      const err = new Error(`Verification inputs required root directory missing: ${dirPath}`)
      err.code = 'verification-inputs-root-missing'
      err.path = dirPath
      throw err
    }
  }

  const collectedMap = new Map()

  const rootEntries = fs.readdirSync(resolvedRoot, { withFileTypes: true })
  for (const entry of rootEntries) {
    if (entry.isFile() && !isIgnoredVerificationFile(entry.name)) {
      const absPath = path.join(resolvedRoot, entry.name)
      const rel = relPath(resolvedRoot, absPath)
      const stat = fs.statSync(absPath)
      const sha256 = computeFileHash(absPath)
      collectedMap.set(rel, {
        path: rel,
        sha256,
        size: stat.size,
        mtimeMs: stat.mtimeMs,
      })
    }
  }

  function walk(currentDir) {
    const entries = fs.readdirSync(currentDir, { withFileTypes: true })
    for (const entry of entries) {
      const fullPath = path.join(currentDir, entry.name)
      if (entry.isDirectory()) {
        if (SKIP_DIR_NAMES.has(entry.name)) continue
        walk(fullPath)
      } else if (entry.isFile()) {
        if (isIgnoredVerificationFile(entry.name)) continue
        const rel = relPath(resolvedRoot, fullPath)
        const stat = fs.statSync(fullPath)
        const sha256 = computeFileHash(fullPath)
        collectedMap.set(rel, {
          path: rel,
          sha256,
          size: stat.size,
          mtimeMs: stat.mtimeMs,
        })
      }
    }
  }

  for (const dirName of REQUIRED_VERIFICATION_DIRS) {
    walk(path.join(resolvedRoot, dirName))
  }

  for (const dirName of OPTIONAL_VERIFICATION_DIRS) {
    const dirPath = path.join(resolvedRoot, dirName)
    if (fs.existsSync(dirPath) && fs.statSync(dirPath).isDirectory()) {
      walk(dirPath)
    }
  }

  for (const entry of collectGeneratedInputs(resolvedRoot)) {
    collectedMap.set(entry.path, { ...entry, loopDetectorCorpus: true })
  }

  return Array.from(collectedMap.values()).sort((a, b) => a.path.localeCompare(b.path))
}

export function diffVerificationInputs(before, after) {
  const beforeMap = new Map(before.map((e) => [e.path, e]))
  const afterMap = new Map(after.map((e) => [e.path, e]))

  if (beforeMap.size !== afterMap.size) {
    return { equal: false, reason: 'file-set-changed' }
  }

  for (const [p, bEntry] of beforeMap.entries()) {
    const aEntry = afterMap.get(p)
    if (!aEntry) {
      return { equal: false, reason: 'file-set-changed' }
    }
    if (bEntry.sha256 !== aEntry.sha256) {
      return { equal: false, reason: `content-changed:${p}` }
    }
    if (Boolean(bEntry.loopDetectorCorpus) !== Boolean(aEntry.loopDetectorCorpus)) {
      return { equal: false, reason: `corpus-membership-changed:${p}` }
    }
  }

  return { equal: true }
}

export function collectCompilerInputs(root = REPO_ROOT, aggregatePath) {
  const resolvedRoot = path.resolve(root)

  const tracked = collectTrackedInputs({
    root: resolvedRoot,
    aggregatePath: aggregatePath ? path.resolve(aggregatePath) : null,
  })

  const results = []
  for (const abs of tracked) {
    if (fs.existsSync(abs)) {
      const stat = fs.statSync(abs)
      const sha256 = computeFileHash(abs)
      results.push({
        path: relPath(resolvedRoot, abs),
        sha256,
        mtimeMs: stat.mtimeMs,
        size: stat.size,
      })
    }
  }
  return results.sort((a, b) => a.path.localeCompare(b.path))
}

export function collectGeneratedInputs(root = REPO_ROOT) {
  const resolvedRoot = path.resolve(root)
  const files = loopDetectorRepositoryInputFiles(resolvedRoot)

  const results = []
  for (const abs of files) {
    if (fs.existsSync(abs)) {
      const stat = fs.statSync(abs)
      const content = fs.readFileSync(abs)
      const sha256 = crypto.createHash('sha256').update(content).digest('hex')
      results.push({
        path: relPath(resolvedRoot, abs),
        sha256,
        mtimeMs: stat.mtimeMs,
        size: stat.size,
      })
    }
  }
  return results.sort((a, b) => a.path.localeCompare(b.path))
}

function walkDir(dir) {
  let list = []
  if (!fs.existsSync(dir)) return list
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      list.push(...walkDir(full))
    } else if (entry.isFile()) {
      list.push(full)
    }
  }
  return list
}

export function collectArtifactInputs(root = REPO_ROOT) {
  const resolvedRoot = path.resolve(root)
  let files = []
  try {
    files = execFileSync('git', ['-C', resolvedRoot, 'ls-files', '--cached', '-z', 'resources'])
      .toString('utf8')
      .split('\0')
      .filter(Boolean)
      .map((rel) => path.resolve(resolvedRoot, rel))
  } catch {
    const resourcesDir = path.resolve(resolvedRoot, 'resources')
    files = walkDir(resourcesDir)
  }

  const results = []
  for (const abs of files) {
    if (fs.existsSync(abs)) {
      const stat = fs.statSync(abs)
      const content = fs.readFileSync(abs)
      const sha256 = crypto.createHash('sha256').update(content).digest('hex')
      results.push({
        path: relPath(resolvedRoot, abs),
        sha256,
        mtimeMs: stat.mtimeMs,
        size: stat.size,
      })
    }
  }
  return results.sort((a, b) => a.path.localeCompare(b.path))
}

export function readManifest({ root = REPO_ROOT, manifestPath } = {}) {
  const resolvedPath = manifestPath
    ? path.resolve(manifestPath)
    : path.resolve(root, DEFAULT_MANIFEST_REL)

  if (!fs.existsSync(resolvedPath)) {
    return null
  }
  const raw = fs.readFileSync(resolvedPath, 'utf8')
  return JSON.parse(raw)
}

export function writeManifest({ root = REPO_ROOT, manifest, manifestPath } = {}) {
  const resolvedPath = manifestPath
    ? path.resolve(manifestPath)
    : path.resolve(root, DEFAULT_MANIFEST_REL)

  const dir = path.dirname(resolvedPath)
  fs.mkdirSync(dir, { recursive: true })

  const tmpPath = `${resolvedPath}.${process.pid}.${Date.now()}.${Math.random().toString(36).slice(2)}.tmp`
  const content = JSON.stringify(manifest, null, 2)
  fs.writeFileSync(tmpPath, content, 'utf8')
  fs.renameSync(tmpPath, resolvedPath)
}

export function invalidateManifest({ root = REPO_ROOT, manifestPath } = {}) {
  const resolvedPath = manifestPath
    ? path.resolve(manifestPath)
    : path.resolve(root, DEFAULT_MANIFEST_REL)

  if (fs.existsSync(resolvedPath)) {
    try {
      fs.rmSync(resolvedPath, { force: true })
    } catch {
      // ignore
    }
  }
}

export function collectOutputs(outputDir) {
  const outputs = {}
  const files = walkDir(outputDir)
  for (const abs of files) {
    const stat = fs.statSync(abs)
    const content = fs.readFileSync(abs)
    const sha256 = crypto.createHash('sha256').update(content).digest('hex')
    const rel = relPath(outputDir, abs)
    outputs[rel] = [sha256, stat.size, stat.mtimeMs]
  }
  return outputs
}

function createFreshnessError(code, filePath, reason) {
  const err = new Error(reason)
  err.code = code
  err.path = filePath
  err.reason = reason
  return err
}

export function assertBuildFresh({ root = REPO_ROOT, manifestPath } = {}) {
  const resolvedRoot = path.resolve(root)
  const resolvedManifestPath = manifestPath
    ? path.resolve(manifestPath)
    : path.resolve(resolvedRoot, DEFAULT_MANIFEST_REL)

  if (!fs.existsSync(resolvedManifestPath)) {
    throw createFreshnessError(
      'manifest-missing',
      resolvedManifestPath,
      `Build manifest not found at ${resolvedManifestPath}`,
    )
  }

  let manifest
  try {
    const raw = fs.readFileSync(resolvedManifestPath, 'utf8')
    manifest = JSON.parse(raw)
  } catch (err) {
    throw createFreshnessError(
      'manifest-corrupt',
      resolvedManifestPath,
      `Build manifest is corrupted: ${err.message}`,
    )
  }

  if (!manifest || manifest.schema !== MANIFEST_SCHEMA) {
    throw createFreshnessError(
      'schema-mismatch',
      resolvedManifestPath,
      `Manifest schema mismatch: expected '${MANIFEST_SCHEMA}', got '${manifest?.schema}'`,
    )
  }

  const outputDir = manifest.outputDir
    ? path.resolve(resolvedRoot, manifest.outputDir)
    : path.resolve(resolvedRoot, 'dist')

  if (!fs.existsSync(outputDir)) {
    throw createFreshnessError(
      'output-missing',
      outputDir,
      `Output directory does not exist: ${outputDir}`,
    )
  }

  // Check outputs against manifest.outputs
  const currentOutputs = collectOutputs(outputDir)
  const recordedOutputs = manifest.outputs ?? {}

  const currentKeys = Object.keys(currentOutputs)
  const recordedKeys = Object.keys(recordedOutputs)

  if (currentKeys.length === 0) {
    throw createFreshnessError(
      'output-empty',
      outputDir,
      `Output directory contains no emitted files: ${outputDir}`,
    )
  }

  for (const file of recordedKeys) {
    const recorded = recordedOutputs[file]
    const current = currentOutputs[file]
    if (!current) {
      throw createFreshnessError(
        'output-missing',
        path.join(outputDir, file),
        `Output file missing: ${file}`,
      )
    }
    const [recordedSha] = recorded
    const [currentSha] = current
    if (currentSha !== recordedSha) {
      throw createFreshnessError(
        'output-stale',
        path.join(outputDir, file),
        `Output file modified: ${file} (hash mismatch)`,
      )
    }
  }

  for (const file of currentKeys) {
    if (!recordedOutputs[file]) {
      throw createFreshnessError(
        'output-extra',
        path.join(outputDir, file),
        `Unrecorded extra output file: ${file}`,
      )
    }
  }

  // Verify compiler inputs
  const currentCompilerInputs = collectCompilerInputs(resolvedRoot)
  const currentCompilerDigest = computeDigest(currentCompilerInputs)
  if (currentCompilerDigest !== manifest.compiler?.inputDigest) {
    throw createFreshnessError(
      'compiler-input-stale',
      resolvedManifestPath,
      `Compiler inputs changed (digest mismatch: expected ${manifest.compiler?.inputDigest}, got ${currentCompilerDigest})`,
    )
  }

  // Verify generated inputs
  const currentGeneratedInputs = collectGeneratedInputs(resolvedRoot)
  const currentGeneratedDigest = computeDigest(currentGeneratedInputs)
  if (currentGeneratedDigest !== manifest.generated?.inputDigest) {
    throw createFreshnessError(
      'generated-input-stale',
      resolvedManifestPath,
      `Generated inputs changed (digest mismatch: expected ${manifest.generated?.inputDigest}, got ${currentGeneratedDigest})`,
    )
  }

  // Verify artifact inputs
  const currentArtifactInputs = collectArtifactInputs(resolvedRoot)
  const currentArtifactDigest = computeDigest(currentArtifactInputs)
  if (currentArtifactDigest !== manifest.artifacts?.inputDigest) {
    throw createFreshnessError(
      'artifact-input-stale',
      resolvedManifestPath,
      `Artifact inputs changed (digest mismatch: expected ${manifest.artifacts?.inputDigest}, got ${currentArtifactDigest})`,
    )
  }

  return {
    generation: manifest.generation ?? 1,
    compilerInputDigest: currentCompilerDigest,
    generatedInputDigest: currentGeneratedDigest,
    artifactInputDigest: currentArtifactDigest,
  }
}

// ── VS-016 固定输入快照 ─────────────────────────────────────────────────────
// verify() 在执行前把输入闭包物化为一份完整、固定的快照：输入文件与目录物理
// 只读（444/555），快照根保持可写以承载合法构建输出（.fable-build 等）。
// node_modules 以 symlink、.git 以 gitfile 按原树锁定身份进入运行环境，均不
// 进入输入 digest。完整性校验组合内容哈希与 inode/ctime 元数据，使「改写后
// 还原」在权限模型失效（如 root）的平台仍可被发现。

function createSnapshotError(code, filePath, reason) {
  const err = new Error(reason)
  err.code = code
  err.path = filePath
  return err
}

export function materializeVerificationSnapshot(root, inputs, parentDir) {
  const resolvedRoot = path.resolve(root)
  const snapshotDir = fs.mkdtempSync(path.join(path.resolve(parentDir), 'verify-snapshot-'))
  const lockDirs = new Set()
  const snapshotOf = (rel) => path.join(snapshotDir, rel)

  try {
    for (const entry of inputs) {
      fs.mkdirSync(path.dirname(snapshotOf(entry.path)), { recursive: true })
      fs.copyFileSync(path.join(resolvedRoot, entry.path), snapshotOf(entry.path))
    }

    const nodeModulesPath = path.join(resolvedRoot, 'node_modules')
    if (fs.existsSync(nodeModulesPath)) {
      fs.symlinkSync(nodeModulesPath, path.join(snapshotDir, 'node_modules'), 'dir')
    }
    const gitDirPath = path.join(resolvedRoot, '.git')
    if (fs.existsSync(gitDirPath)) {
      fs.writeFileSync(path.join(snapshotDir, '.git'), `gitdir: ${gitDirPath}\n`)
    }

    // VS-016：合法构建输出锚点必须在目录锁定前物化为可写目录。owner-compile
    // 将 flatAnchor 输出锚定在 src/Wanxiangshu/.fable-build/output-compile/，
    // 而 .fable-build 属于构建产物、不进入输入闭包；若不预创建，锁定后的
    // 只读父目录会让 build 阶段的 mkdir 直接 EACCES。该锚点目录不参与输入
    // digest，完整性校验的未知新增检查也只针对文件，不受此目录影响。
    fs.mkdirSync(path.join(snapshotDir, 'src/Wanxiangshu', '.fable-build'), { recursive: true })

    for (const entry of inputs) {
      let dir = path.dirname(snapshotOf(entry.path))
      while (dir !== snapshotDir) {
        lockDirs.add(dir)
        dir = path.dirname(dir)
      }
    }
    for (const dir of lockDirs) fs.chmodSync(dir, 0o555)
    for (const entry of inputs) fs.chmodSync(snapshotOf(entry.path), 0o444)

    // 准备一致性：物化副本必须逐字节等于执行前捕获的输入身份；物化期间
    // 原树被并发改写会在此立即失败，不形成混合世代。
    const files = inputs.map((entry) => {
      const abs = snapshotOf(entry.path)
      const sha256 = computeFileHash(abs)
      if (sha256 !== entry.sha256) {
        throw createSnapshotError(
          'snapshot-preparation-mismatch',
          abs,
          `Snapshot copy diverged from captured input: ${entry.path}`,
        )
      }
      const stat = fs.statSync(abs)
      return {
        path: entry.path,
        sha256,
        size: stat.size,
        mtimeMs: stat.mtimeMs,
        ctimeMs: stat.ctimeMs,
        ino: stat.ino,
      }
    })

    return {
      dir: snapshotDir,
      root: resolvedRoot,
      digest: computeDigest(inputs),
      files,
      lockedDirs: [...lockDirs],
    }
  } catch (err) {
    releaseVerificationSnapshot({ dir: snapshotDir, files: [], lockedDirs: [...lockDirs] })
    throw err
  }
}

export function verifyVerificationSnapshotIntegrity(snapshot) {
  const knownPaths = new Set(snapshot.files.map((file) => file.path))
  for (const record of snapshot.files) {
    const abs = path.join(snapshot.dir, record.path)
    let stat
    try {
      stat = fs.statSync(abs)
    } catch {
      return { equal: false, reason: `snapshot-file-missing:${record.path}` }
    }
    if (stat.ino !== record.ino) {
      return { equal: false, reason: `snapshot-replaced:${record.path}` }
    }
    if (stat.ctimeMs !== record.ctimeMs || stat.mtimeMs !== record.mtimeMs || stat.size !== record.size) {
      return { equal: false, reason: `snapshot-metadata-changed:${record.path}` }
    }
    if (computeFileHash(abs) !== record.sha256) {
      return { equal: false, reason: `snapshot-content-changed:${record.path}` }
    }
  }
  // 输入区新增文件：目录只读在普通用户平台物理阻止新增；权限模型失效的
  // 平台由该集合校验发现。快照输入目录树只含物化输入，故任何未知文件都
  // 是运行期新增。
  for (const dir of snapshot.lockedDirs) {
    let entries
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true })
    } catch {
      return { equal: false, reason: 'snapshot-input-dir-unreadable' }
    }
    for (const entry of entries) {
      if (entry.isFile() && !knownPaths.has(relPath(snapshot.dir, path.join(dir, entry.name)))) {
        return { equal: false, reason: 'snapshot-file-set-changed' }
      }
    }
  }
  return { equal: true }
}

export function releaseVerificationSnapshot(snapshot) {
  if (!snapshot?.dir) return
  try {
    for (const record of snapshot.files ?? []) {
      try {
        fs.chmodSync(path.join(snapshot.dir, record.path), 0o644)
      } catch {}
    }
    for (const dir of snapshot.lockedDirs ?? []) {
      try {
        fs.chmodSync(dir, 0o755)
      } catch {}
    }
    // rmSync 对 symlink 只删除链接本身，不递归原树 node_modules。
    fs.rmSync(snapshot.dir, { recursive: true, force: true })
  } catch {}
}
