import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as warmStart from '../../../../dist/Repository/Investigation/WarmStartSurface.js'

export { warmStart }

export async function withWorkspace(run) {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-investigation-'))
  try {
    return await run(directory)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

export const hint = (filePath, content, extra = {}) => ({
  filePath, content, startLine: 1, endLine: 3, score: 0.9, totalLines: 20, ...extra,
})

export const searchResult = (ordinal, query, hints) => ({
  ordinal, query,
  hints: hints.map((value, index) => ({ ...value, keywordOrdinal: ordinal, localRank: index + 1 })),
})
