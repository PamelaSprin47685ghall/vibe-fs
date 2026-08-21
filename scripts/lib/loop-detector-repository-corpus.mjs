import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')

const repositoryTextEntries = (root = defaultRoot) => {
  const decoder = new TextDecoder('utf-8', { fatal: true })
  const paths = execFileSync('git', [
    '-C', root, 'ls-files', '--cached', '--others', '--exclude-standard', '-z',
  ])
    .toString('utf8')
    .split('\0')
    .filter(Boolean)

  const readable = []
  for (const relPath of paths) {
    if (relPath === 'src/Wanxiangshu/FableBarrier.fs' || relPath.endsWith('/FableBarrier.fs')) continue
    const file = path.join(root, relPath)
    try {
      readable.push({ file, text: decoder.decode(readFileSync(file)) })
    } catch {
      // Repository SSOT is strict UTF-8 text; binary/invalid UTF-8 is outside the corpus.
    }
  }
  return readable
}

export const loopDetectorRepositoryInputFiles = (root = defaultRoot) =>
  repositoryTextEntries(root).map(({ file }) => file)

export const loopDetectorRepositoryTexts = (root = defaultRoot) =>
  repositoryTextEntries(root).map(({ text }) => text)
