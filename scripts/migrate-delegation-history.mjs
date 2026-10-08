#!/usr/bin/env node
/**
 * Offline one-shot migration of pre-delegation Strength history into the
 * readonly-delegation protocol (requirements/speculative-investigation [015],
 * requirements/durable-events [026]).
 *
 * It runs on a copy of the EventStore (a .git common dir) and appends only
 * DelegationHistoryImported facts. It never touches the live store, Git
 * objects or refs, or user configuration, and it never mutates append-only
 * history: every effect is a new fact appended through the EventStore owner
 * surface.
 *
 * Usage:
 *   node scripts/migrate-delegation-history.mjs \
 *     --backup <copy-of-.git> --input-version pre-delegation \
 *     --contract-revision 1 --report <file> [--execute]
 *
 * Without --execute the run is a dry run on a temporary copy of the backup
 * and nothing is appended. Re-running --execute is idempotent: the import
 * events derive deterministically from the old event ids.
 */

import { cpSync, existsSync, mkdirSync, mkdtempSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { tmpdir } from 'node:os'
import { dirname, join, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')

const fail = (message) => {
  console.error(message)
  process.exit(2)
}

const loadDist = async (modulePath) => {
  try {
    return await import(join(repoRoot, 'dist', modulePath))
  } catch (error) {
    fail(`无法加载 dist 模块 ${modulePath}：${error && error.message}；请先运行 node scripts/build.mjs`)
  }
}

const sha256 = (text) => createHash('sha256').update(text).digest('hex')
const STRENGTH_STREAM_PREFIX = 'strength/'
const IMPORT_EVENT_TYPE = 'DelegationHistoryImported'
// 登记表之外一律拒绝：分类器只认定一种旧协议，运行时契约修订以 Delegate.fs 为准。
const INPUT_VERSIONS = ['pre-delegation']
const CONTRACT_REVISIONS = [1]

const usage = () =>
  fail(
    [
      '用法: node scripts/migrate-delegation-history.mjs --backup <copy-of-.git>',
      '  --input-version pre-delegation --contract-revision 1 --report <file> [--execute]',
      '不带 --execute 时为 dry run：只在临时副本上规划并写报告，不追加任何事实。',
    ].join('\n'),
  )

// 1. 前置校验：参数完整、目标是目录副本、绝不是活库。
const argv = process.argv.slice(2)
const options = {}

for (let index = 0; index < argv.length; index += 1) {
  const token = argv[index]
  if (token === '--execute') {
    options.execute = true
  } else if (token.startsWith('--')) {
    const value = argv[index + 1]
    if (value === undefined || value.startsWith('--')) usage()
    options[token.slice(2)] = value
    index += 1
  } else {
    usage()
  }
}

if (!options.backup || !options['input-version'] || !options['contract-revision'] || !options.report) usage()

// 版本只认登记表：未知输入版本或契约修订一律拒绝，报告里的版本从此是已校验值。
const inputVersion = options['input-version']
if (!INPUT_VERSIONS.includes(inputVersion)) {
  fail(`--input-version 必须是已登记的值: ${INPUT_VERSIONS.join(' | ')}；收到: ${inputVersion || '(空)'}`)
}

const contractRevision = Number(options['contract-revision'])
if (!CONTRACT_REVISIONS.includes(contractRevision)) {
  fail(
    `--contract-revision 必须是已登记的值: ${CONTRACT_REVISIONS.join(' | ')}（运行时契约修订，见 src/Wanxiangshu/Strength/OpenCode/Delegate.fs:40 的 DelegationContractRevisions.create 1）；收到: ${options['contract-revision']}`,
  )
}

const backup = resolve(options.backup)
if (!existsSync(backup) || !statSync(backup).isDirectory()) fail(`--backup 必须是已存在的 .git common dir 目录: ${backup}`)

const liveStore = resolve(join(repoRoot, '.git'))
if (backup === liveStore || backup === repoRoot || backup.startsWith(liveStore + sep)) {
  fail(`拒绝在活库上运行: ${backup}；请先复制一份 .git 备份再迁移。`)
}

const EventStore = await loadDist('Persistence/EventStore/Surface.js')
// 分类、读取与规划只经 Strength 的 JS 语义面（[015] 的登记入口）：dist 内部分级
// 模块的导出名带 Fable 模块前缀，不是脚本契约。
const Strength = await loadDist('Strength/Surface.js')

// 临时副本（dry run）或直接操作备份（execute：唯一允许的写入）。
let workDir = backup
let ownedTemp = null

if (!options.execute) {
  const base = mkdtempSync(join(tmpdir(), 'wxs-migrate-'))
  workDir = join(base, '.git')
  cpSync(backup, workDir, { recursive: true })
  ownedTemp = base
}

const writerId = options.execute ? `migrate-delegation-${contractRevision}` : 'migrate-dry-run'
let handle = EventStore.create(workDir, writerId)

const cleanup = () => {
  if (handle) {
    try {
      EventStore.dispose(handle)
    } catch {
      // dispose 失败不掩盖原始结果；进程退出即释放。
    }
    handle = null
  }

  if (ownedTemp) {
    rmSync(ownedTemp, { recursive: true, force: true })
    ownedTemp = null
  }
}

process.on('exit', cleanup)

// 分类器要 canonical payload 文本；EventStore 面的信封 payload 可能是已解析对象。
const payloadJsonOf = (envelope) =>
  typeof envelope.payload === 'string' ? envelope.payload : JSON.stringify(envelope.payload)

// 2. 清单快照：经 EventStore 所有者面枚举 strength 流，不读 writer 文件。
const readStreamEnvelopes = (store, streamId) => {
  const found = new Map()
  const missing = []
  const pending = [...EventStore.heads(store, streamId)]

  while (pending.length > 0) {
    const eventId = pending.pop()
    if (found.has(eventId)) continue
    const envelope = EventStore.read(store, eventId)
    if (!envelope) {
      missing.push(eventId)
      continue
    }
    found.set(eventId, envelope)
    for (const parent of envelope.parents ?? []) pending.push(parent)
  }

  return { envelopes: [...found.values()], missing }
}

const causalOrder = (envelopes) => {
  const byId = new Map(envelopes.map((envelope) => [envelope.id, envelope]))
  const emitted = new Set()
  const ordered = []
  let remaining = [...byId.keys()].sort()

  while (remaining.length > 0) {
    const ready = remaining.filter((eventId) =>
      (byId.get(eventId).parents ?? []).every((parent) => emitted.has(parent) || !byId.has(parent)),
    )

    if (ready.length === 0) {
      throw new Error('决策内的父边在保留窗口内无法解析')
    }

    for (const eventId of ready) {
      ordered.push(byId.get(eventId))
      emitted.add(eventId)
    }

    remaining = remaining.filter((eventId) => !emitted.has(eventId))
  }

  return ordered
}

const strengthStreams = EventStore.streams(handle).filter((streamId) => streamId.startsWith(STRENGTH_STREAM_PREFIX))
const legacyDecisions = []
const anomalies = []
let strengthEnvelopeCount = 0

for (const streamId of strengthStreams) {
  const { envelopes, missing } = readStreamEnvelopes(handle, streamId)
  strengthEnvelopeCount += envelopes.length
  if (missing.length > 0) {
    anomalies.push({ streamId, missingParents: missing })
    continue
  }

  const ordered = causalOrder(envelopes)
  const isLegacyDecision = ordered.some(
    (envelope) => Strength.migrationClassifyEnvelope(envelope.type, payloadJsonOf(envelope)).kind === 'legacy',
  )

  if (isLegacyDecision) legacyDecisions.push({ streamId, envelopes: ordered })
}

if (anomalies.length > 0) {
  cleanup()
  fail(
    `存在保留窗口内父边缺失的 strength 流，无法安全规划迁移: ${JSON.stringify(anomalies)}；`,
  )
}

// 3./4. 纯分类后的规划与 dry-run：所有旧协议线格式知识只在 F# 分类器与规划器里。
const planned = []
const rejected = []
let payloadRefsToVerify = []

for (const decision of legacyDecisions) {
  try {
    const legacyEnvelopes = []
    for (const envelope of decision.envelopes) {
      const read = Strength.migrationReadLegacyEnvelope(JSON.stringify(envelope))
      if (read) legacyEnvelopes.push(read)
    }

    if (legacyEnvelopes.length !== decision.envelopes.length) {
      throw new Error('legacy 决策含有规划器无法读取的信封')
    }

    const plan = Strength.migrationPlanDecision(sha256, contractRevision, legacyEnvelopes)
    if (!plan.ok) throw new Error(plan.error)
    for (const fact of plan.value) {
      const imported = Strength.migrationImportEvent(sha256, fact)
      if (!imported.ok) throw new Error(imported.error)
      planned.push({ fact, event: imported.value })
      if (fact.outcomeKind === 'adopted') {
        for (const payloadRef of fact.materialPayloads ?? []) payloadRefsToVerify.push([fact.importId, payloadRef])
      }
    }
  } catch (error) {
    rejected.push({ streamId: decision.streamId, reason: String((error && error.message) || error) })
  }
}

if (rejected.length > 0) {
  cleanup()
  fail(`以下 legacy 决策无法规划，已中止且未追加任何事实: ${JSON.stringify(rejected, null, 2)}`)
}

const payloadProblems = []
for (const [importId, payloadRef] of payloadRefsToVerify) {
  const bytes = await EventStore.readPayload(handle, payloadRef)
  if (!bytes) payloadProblems.push({ importId, payloadRef })
}

if (payloadProblems.length > 0) {
  cleanup()
  fail(`adopted 材料引用的 payload 在备份中缺失: ${JSON.stringify(payloadProblems)}`)
}

// 5. WritePayload 不需要：导入事实按内容哈希引用既有 payload，不复制、不改写。
//    execute 才追加；dry run 到此为止。
let appended = 0

if (options.execute) {
  for (const { event } of planned) {
    const result = await EventStore.append(handle, [event])
    if (!result || result.ok !== true) {
      const detail = result && result.error ? JSON.stringify(result.error) : String(result)
      cleanup()
      fail(`append 导入事实失败: ${detail}`)
    }
    appended += 1
  }
}

// 6. cold replay 对照：迁移后 strength 流里不得再有 legacy 信封，
//    每个导入事实都必须可按其确定性事件 id 原样读回。读回用一个显式的
//    只读 verify 句柄，不依赖 append 对同一 writer 句柄的立即可见性。
const coldReplay = { legacyEnvelopesAfter: [], readBackOk: 0, readBackFailed: [] }
let verifyHandle = null

if (options.execute) {
  verifyHandle = EventStore.create(workDir, `migrate-verify-${contractRevision}`)
}

const readHandle = verifyHandle ?? handle

for (const streamId of EventStore.streams(readHandle).filter((streamId) => streamId.startsWith(STRENGTH_STREAM_PREFIX))) {
  const { envelopes } = readStreamEnvelopes(readHandle, streamId)
  for (const envelope of envelopes) {
    if (Strength.migrationClassifyEnvelope(envelope.type, payloadJsonOf(envelope)).kind === 'legacy') {
      coldReplay.legacyEnvelopesAfter.push(envelope.id)
    }
  }
}

if (options.execute) {
  for (const { fact, event } of planned) {
    const stored = EventStore.read(readHandle, event.id)
    if (stored && stored.type === IMPORT_EVENT_TYPE && stored.stream === event.stream) {
      coldReplay.readBackOk += 1
    } else {
      coldReplay.readBackFailed.push(fact.importId)
    }
  }

  if (coldReplay.legacyEnvelopesAfter.length > 0 || coldReplay.readBackFailed.length > 0) {
    if (verifyHandle) EventStore.dispose(verifyHandle)
    cleanup()
    fail(`cold replay 对照未通过: ${JSON.stringify(coldReplay)}`)
  }
}

if (verifyHandle) EventStore.dispose(verifyHandle)

// 7. 报告落盘：旧档位预算只随证据字段与报告出现，不进入任何生产预算选择。
const bigintText = (value) => (typeof value === 'bigint' ? value.toString() : value)

const report = {
  schema: 'delegation-history-migration/v1',
  mode: options.execute ? 'execute' : 'dry-run',
  inputVersion,
  contractRevision,
  backup,
  writerId,
  inventory: {
    streams: EventStore.streams(handle).length,
    strengthStreams: strengthStreams.length,
    strengthEnvelopes: strengthEnvelopeCount,
    legacyDecisions: legacyDecisions.length,
  },
  plan: {
    importFacts: planned.length,
    adopted: planned.filter((entry) => entry.fact.outcomeKind === 'adopted').length,
    relinquished: planned.filter((entry) => entry.fact.outcomeKind === 'relinquished').length,
    payloadRefsVerified: payloadRefsToVerify.length,
    appended,
  },
  decisions: legacyDecisions.map((decision) => ({
    streamId: decision.streamId,
    facts: planned
      .filter((entry) => entry.fact.sourceStreamId === decision.streamId)
      .map((entry) => ({
        sourceEventId: entry.fact.sourceEventId,
        importId: entry.fact.importId,
        outcomeKind: entry.fact.outcomeKind,
        oldBudgetEvidence: entry.fact.oldBudgetEvidence ?? null,
        targetProviderRun: entry.fact.targetProviderRun ?? null,
        frameDigest: entry.fact.frameDigest ?? null,
        byteLength: entry.fact.byteLength ?? null,
        materialPayloads: entry.fact.materialPayloads ?? [],
        tracedStartInclusive: bigintText(entry.fact.tracedStartInclusive ?? null) ?? null,
        tracedEndExclusive: bigintText(entry.fact.tracedEndExclusive ?? null) ?? null,
        relinquishReason: entry.fact.relinquishReason ?? null,
      })),
  })),
  coldReplay,
}

const reportPath = resolve(options.report)
mkdirSync(dirname(reportPath), { recursive: true })
writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`)
console.log(`迁移${options.execute ? '已执行' : 'dry run 完成'}：报告写入 ${reportPath}`)
