import { lstatSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'

const SKIP = new Set(['node_modules', '.git', 'build', 'artifacts', 'obj', 'bin', '.slim', '.omo'])

/**
 * Synchronous directory walker that fails closed.
 *
 * On success returns sorted absolute paths whose basename matches one of
 * `extensions` (or all paths when `extensions` is falsy). SKIP directory names
 * are preserved. Symlink entries are rejected — never followed — so hidden
 * content cannot evade a scan. Missing root, non-directory root, and nested
 * readdir/stat failures throw an Error naming the path and operation.
 *
 * Callers that probe an optional root MUST guard existence explicitly
 * (existsSync + statSync().isDirectory()) before calling walk; walk no longer
 * swallows a missing root as an empty array.
 */
export function walk(root, extensions) {
  const out = []
  const visit = (dir) => {
    let entries
    try {
      entries = readdirSync(dir, { withFileTypes: true })
    } catch (err) {
      throw new Error(`walk: readdir failed on '${dir}': ${err.message}`)
    }
    for (const entry of entries) {
      if (SKIP.has(entry.name)) continue
      const full = join(dir, entry.name)
      if (entry.isSymbolicLink()) {
        throw new Error(`walk: refusing to traverse symlink entry '${full}'`)
      }
      if (entry.isDirectory()) visit(full)
      else if (!extensions || extensions.some((ext) => entry.name.endsWith(ext))) out.push(full)
    }
  }
  let rootStat
  try {
    rootStat = lstatSync(root)
  } catch (err) {
    throw new Error(`walk: root '${root}' is not accessible: ${err.message}`)
  }
  if (rootStat.isSymbolicLink()) {
    throw new Error(`walk: refusing to traverse symlink root '${root}'`)
  }
  if (!rootStat.isDirectory()) {
    throw new Error(`walk: root '${root}' is not a directory`)
  }
  visit(root)
  return out.sort()
}

export function readLines(file) {
  return readFileSync(file, 'utf8').split('\n')
}

export function countLiteral(files, needle) {
  const hits = []
  for (const file of files) {
    readLines(file).forEach((text, index) => {
      if (text.includes(needle)) hits.push({ file, line: index + 1, text: text.trim() })
    })
  }
  return hits
}
