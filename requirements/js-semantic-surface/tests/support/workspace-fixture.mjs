import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

export function createWorkspaceFixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'js-surface-'))
  t.after(() => rmSync(root, { recursive: true, force: true }))
  const write = (relativePath, source) => {
    const file = join(root, relativePath)
    mkdirSync(dirname(file), { recursive: true })
    writeFileSync(file, source)
    return file
  }
  write('package.json', '{"type":"module"}')
  return { root, write }
}
