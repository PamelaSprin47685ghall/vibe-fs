import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { compilerEvidenceReferences } from './support/compiler-evidence-scan.mjs'

test('WHAT[verification-system-018] compiler evidence guard rejects direct FCS use and allows ordinary Fable compilation', () => {
  assert.deepEqual(compilerEvidenceReferences('dotnet tool run fable\nnode scripts/build.mjs\n'), [])
  for (const source of [
    '#r "nuget: FSharp.Compiler.Service"',
    'open FSharp.Compiler.CodeAnalysis',
    'FSharpChecker.Create()',
    'load("Fable.Compiler.dll")',
    'load("Fable.AST.dll")',
    'scanCompilerObservationsV1()',
  ]) assert.equal(compilerEvidenceReferences(source).length, 1, source)
})

test('WHAT[verification-system-018] source and tooling contain no known compiler evidence entry points', () => {
  const root = fileURLToPath(new URL('../../..', import.meta.url))
  const hits = []
  const scan = (directory) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name)
      if (entry.isDirectory()) {
        if (!['obj', 'bin', 'node_modules', '.git'].includes(entry.name)) scan(path)
      } else if (/\.(?:mjs|cjs|js|fs|fsi|fsx|fsproj|sh|ps1|json)$/.test(entry.name)) {
        for (const hit of compilerEvidenceReferences(readFileSync(path, 'utf8'))) hits.push({ path, ...hit })
      }
    }
  }
  scan(join(root, 'scripts'))
  scan(join(root, 'src'))
  assert.deepEqual(hits, [])
})
