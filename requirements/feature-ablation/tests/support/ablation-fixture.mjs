import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const root = fileURLToPath(new URL('../../../..', import.meta.url))

export function withAblationEnv(entries, run) {
  const names = Object.keys(process.env).filter((name) => name.startsWith('WANXIANGSHU_ABLATION_'))
  const previous = Object.fromEntries(names.map((name) => [name, process.env[name]]))
  try {
    for (const name of names) delete process.env[name]
    for (const [name, value] of entries) {
      if (value !== undefined) process.env[name] = value
    }
    return run()
  } finally {
    for (const name of Object.keys(process.env)) {
      if (name.startsWith('WANXIANGSHU_ABLATION_')) delete process.env[name]
    }
    Object.assign(process.env, previous)
  }
}

export async function withAblationFixture(mutate, run) {
  const dir = mkdtempSync(join(tmpdir(), 'ablation-contract-'))
  try {
    cpSync(join(root, 'dist'), join(dir, 'dist'), { recursive: true })
    cpSync(join(root, 'resources/ablation'), join(dir, 'resources/ablation'), { recursive: true })
    writeFileSync(join(dir, 'package.json'), '{"type":"module"}\n')
    const nodesPath = join(dir, 'resources/ablation/nodes.json')
    const nodes = JSON.parse(readFileSync(nodesPath, 'utf8'))
    mutate(nodes)
    writeFileSync(nodesPath, JSON.stringify(nodes))
    const surface = await import(pathToFileURL(join(dir, 'dist/Ablation/Surface.js')).href)
    return withAblationEnv([], () => run(surface))
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}
