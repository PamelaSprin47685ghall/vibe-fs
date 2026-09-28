import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

export const resourceFixture = () => {
  const directory = mkdtempSync(join(tmpdir(), 'provider-language-'))
  return {
    directory,
    write: (semantic, locale, content) => {
      const parent = join(directory, semantic)
      mkdirSync(parent, { recursive: true })
      writeFileSync(join(parent, locale), content)
    },
    dispose: () => rmSync(directory, { recursive: true, force: true }),
  }
}
