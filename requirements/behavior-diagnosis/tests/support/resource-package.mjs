import { cpSync, mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const root = fileURLToPath(new URL('../../../..', import.meta.url))

export async function withRulebookPackage(run) {
  const directory = mkdtempSync(join(tmpdir(), 'rulebook-package-'))
  try {
    cpSync(join(root, 'dist'), join(directory, 'dist'), { recursive: true })
    writeFileSync(join(directory, 'package.json'), '{"type":"module"}\n')
    mkdirSync(join(directory, 'node_modules'), { recursive: true })
    symlinkSync(join(root, 'node_modules/wanxiangli'), join(directory, 'node_modules/wanxiangli'), 'junction')
    const rulebook = join(directory, 'resources/enforcer')
    mkdirSync(rulebook, { recursive: true })
    const write = (name, file, content) => {
      const rule = join(rulebook, name)
      mkdirSync(rule, { recursive: true })
      writeFileSync(join(rule, file), content)
    }
    const surface = await import(pathToFileURL(join(directory, 'dist/Enforcer/Surface.js')).href)
    await run({ directory, rulebook, write, surface })
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}
