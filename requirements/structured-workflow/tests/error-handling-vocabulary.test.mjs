import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(join(root, path), 'utf8')
const productionFiles = (dir) =>
  readdirSync(join(root, dir), { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    return entry.isDirectory() ? productionFiles(path) : entry.name.endsWith('.fs') ? [path] : []
  })

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_PREREQ_FsToolkit_ErrorHandling_is_the_repo_Result_vocabulary', () => {
  const fsproj = read('src/Wanxiangshu/Wanxiangshu.fsproj')
  const decode = read('src/Wanxiangshu/Sphinx/DecodePrimitives.fs')
  const codec = read('src/Wanxiangshu/Sphinx/ObservationCodec.fs')

  assert.match(
    fsproj,
    /<PackageReference Include="FsToolkit\.ErrorHandling" Version="5\.2\.0"\/>/,
  )
  assert.match(decode, /open FsToolkit\.ErrorHandling/)
  assert.match(codec, /open FsToolkit\.ErrorHandling/)
  assert.doesNotMatch(decode, /type ResultBuilder\b/)
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_PREREQ_project_owns_a_Fable_compatible_TaskResult_CE', () => {
  const fsproj = read('src/Wanxiangshu/Wanxiangshu.fsproj')
  const source = read('src/Wanxiangshu/Foundation/TaskResult.fs')

  assert.match(fsproj, /<Compile Include="Foundation\/TaskResult\.fs"\/>/)
  assert.ok(
    fsproj.indexOf('Foundation/TaskResult.fs') <
      fsproj.indexOf('Persistence/EventStore/WriterStreamSync.fs'),
    'TaskResult CE must compile before workflow consumers',
  )
  assert.match(source, /type TaskResultBuilder\(\)/)
  assert.match(source, /Task<Result<'value, 'error>>/)
  assert.match(source, /member _\.TryWith/)
  assert.match(source, /member _\.TryFinally/)
  assert.match(source, /member this\.Using/)
  assert.match(source, /member this\.While/)
  assert.match(source, /member this\.For/)
  assert.match(source, /let taskResult = TaskResultBuilder\(\)/)
  assert.match(source, /let ofTask /)
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_PREREQ_Fable_async_Result_plumbing_is_repo_owned_not_FsToolkit_dotnet_only_API', () => {
  const fsproj = read('src/Wanxiangshu/Wanxiangshu.fsproj')
  const source = read('src/Wanxiangshu/Foundation/FsToolkitFableCompat.fs')
  const production = productionFiles('src/Wanxiangshu').map(read).join('\n')

  assert.match(fsproj, /<Compile Include="Foundation\/FsToolkitFableCompat\.fs"\/>/)
  assert.match(source, /module TaskValue =/)
  assert.match(source, /module TaskResult =/)
  assert.match(source, /module TaskResultList =/)
  assert.match(source, /let traverseM/)
  assert.doesNotMatch(production, /\bTask\.map\b/)
  assert.doesNotMatch(production, /\bList\.traverseTaskResultM\b/)
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_PREREQ_representative_Task_Result_pyramid_uses_the_vocabulary', () => {
  const source = read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(source, /open Wanxiangshu\.Foundation/)
  assert.match(source, /open FsToolkit\.ErrorHandling/)
  assert.match(source, /let private readRemote[\s\S]*?taskResult \{/)
  assert.match(source, /let private importRemote[\s\S]*?result \{/)
  assert.match(source, /let syncWriterStreams[\s\S]*?taskResult \{/)
  assert.doesNotMatch(
    source,
    /match! readRequiredTree raw writerTree\.Oid "writers" with[\s\S]*?match! readBlobList raw writerEntries with/,
  )
})
