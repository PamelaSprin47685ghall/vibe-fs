import { mkdtempSync, writeFileSync, readdirSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
const surface = await import(process.argv[2] + '/dist/Process/Surface.js')
const repo = mkdtempSync(join(tmpdir(), 'cut20-red-'))
execFileSync('git', ['init', '-q', repo])
writeFileSync(join(repo, 'conflicted.txt'), Array.from({ length: 6000 }, (_, i) => `<<<<<<< ours ${i}\n`).join(''))
execFileSync('git', ['-C', repo, 'add', '-A'])
const before = readdirSync(tmpdir()).filter(n => /^spool-.*\.tmp$/.test(n)).length
const outcome = await surface.run(
  surface.command('git', ['grep', '-I', '-n', '-E', '^<<<<<<< |^>>>>>>> ', '--', '.'], repo, undefined),
  surface.estimate(30, 65536, 'medium'), surface.context(repo, 60000), surface.createCancellationToken(false))
const after = readdirSync(tmpdir()).filter(n => /^spool-.*\.tmp$/.test(n)).length
console.log(JSON.stringify({ ok: outcome.ok, kind: outcome.value?.kind, totalBytes: outcome.value?.totalBytes, exitCode: outcome.value?.exitCode, leakedSpoolFiles: after - before }))
