import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(join(root, path), 'utf8')

test('CONTROL_PYRAMID_PREREQ_FsToolkit_ErrorHandling_is_the_repo_Result_vocabulary', () => {
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

test('CONTROL_PYRAMID_PREREQ_project_owns_a_Fable_compatible_TaskResult_CE', () => {
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

test('CONTROL_PYRAMID_PREREQ_representative_Task_Result_pyramid_uses_the_vocabulary', () => {
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
