import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'

const SKIP = new Set(['node_modules', '.git', 'build', 'artifacts', 'obj', 'bin', '.slim', '.omo'])

export function walk(root, extensions) {
  const out = []
  const visit = (dir) => {
    let entries
    try {
      entries = readdirSync(dir, { withFileTypes: true })
    } catch {
      return
    }
    for (const entry of entries) {
      if (SKIP.has(entry.name)) continue
      const full = join(dir, entry.name)
      if (entry.isDirectory()) visit(full)
      else if (!extensions || extensions.some((ext) => entry.name.endsWith(ext))) out.push(full)
    }
  }
  try {
    if (!statSync(root).isDirectory()) return [root]
  } catch {
    return []
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
