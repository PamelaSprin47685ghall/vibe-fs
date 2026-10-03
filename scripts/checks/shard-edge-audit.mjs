#!/usr/bin/env node
// shard-edge-audit.mjs — focused 编译 shard 缺边审计（纯词法）。
//
// 背景：focused 增量编译按 shard 的 ProjectReference 传递闭包选源；闭包
// 算法（planImpactFromInventory）递归完整，缺陷在 shard 依赖图数据层——
// shard 源码引用了其闭包外 shard 定义的命名空间/模块/类型时，以该 shard
// 为根的 focused 编译报「类型未定义」。全量编译因 compile-order.txt 的
// 全序掩盖缺边，只有 focused 才暴露。
//
// 本工具复用 build 侧 fsproj 图解析（compile-shards.mjs 的
// readCompileShardInventory），对每个 shard 的 .fs/.fsi 做受限词法扫描，
// 对照传递闭包输出「源码引用了符号但其 shard 闭包不含定义所在 shard」
// 的疑似缺边清单。纯文本扫描，不引入 FCS、不启动编译器（WHAT-018）。
//
// 这是审计工具，不是构建兜底：不进构建路径、不改闭包。清单是排查起点
// 不是结论；误报可接受，漏报尽量少（宁可多报）。
//
// 置信度分级：
//   high   — open 命名空间 / 全限定引用 / 模块前缀引用，其全部定义 shard
//            都不在闭包（词法证据与图证据直接矛盾）。
//   medium — 同名模块/命名空间多 shard 定义且全部不在闭包（无法唯一指认）。
//   low    — 纯类型名粗匹配：全局唯一定义且不在闭包。同名多 shard 定义
//            （如 SessionId）不报——词法无法定位该补哪条边。
//
// 已知漏报边界（词法扫描原理性看不见）：
//   - AutoOpen module 的隐式可见符号（无显式 open/前缀）；
//   - CE builder 等小写 let 绑定值（taskResult 等，非 type/module 声明）；
//   - 继承/接口实现的隐式类型依赖、运算符解析依赖；
//   - open 命名空间部分覆盖时，实际使用的具体类型恰在缺失 shard（open
//     规则不报，只有纯类型名规则可能兜住）。
// 已知误报边界：
//   - 局部标识符/字段名撞全局唯一类型名（low 级）；
//   - module 与 type 同名时按 module 规则优先，可能错报置信度级别。

import { join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { createCheckContext } from '../lib/check-context.mjs'
import { maskFSharpTrivia } from '../lib/fsharp-source.mjs'

const here = join(fileURLToPath(new URL('.', import.meta.url)))
const repositoryRoot = join(here, '..', '..')

const UPPER_IDENT = /^[A-Z][A-Za-z0-9_']*/
const isUpperIdent = (name) => UPPER_IDENT.test(name)

/** ProjectReference 传递闭包（含自身）。图来自 compile-shards 解析，不重造。 */
export function transitiveClosure(projects, startPath) {
  const closure = new Set()
  const queue = [startPath]
  while (queue.length > 0) {
    const current = queue.pop()
    if (closure.has(current)) continue
    closure.add(current)
    for (const reference of projects.get(current).references) queue.push(reference)
  }
  return closure
}

/**
 * 全仓符号索引：namespace / module / type 声明 → 定义 shard 集合。
 * 只收 Wanxiangshu 仓内声明；BCL 与 FSharp.Core 符号天然不在索引中，
 * 引用它们不会产生 finding。
 */
export function buildSymbolIndex(inventory, readText) {
  const namespaces = new Map()
  const modules = new Map()
  const types = new Map()
  const fileToProject = new Map()

  const addNamespace = (name, projectPath) => {
    if (!namespaces.has(name)) namespaces.set(name, new Set())
    namespaces.get(name).add(projectPath)
  }
  const addModule = (name, projectPath) => {
    if (!modules.has(name)) modules.set(name, new Set())
    modules.get(name).add(projectPath)
  }
  const addType = (name, projectPath) => {
    if (!types.has(name)) types.set(name, new Set())
    types.get(name).add(projectPath)
  }

  // readText 约定返回 maskFSharpTrivia 后的 code（注释/字符串已遮）。
  for (const [projectPath, project] of inventory.projects) {
    for (const source of project.compileItems) {
      const code = readText(source)
      fileToProject.set(source, projectPath)

      const namespaceMatch = /^namespace\s+([A-Za-z_][A-Za-z0-9_'.]*)/m.exec(code)
      if (namespaceMatch) addNamespace(namespaceMatch[1], projectPath)

      // `module Full.Path.Name =` 隐式贡献路径命名空间 + 末段模块名；
      // `module ShortName =` 只贡献模块名。
      for (const moduleMatch of code.matchAll(/^module\s+([A-Za-z_][A-Za-z0-9_'.]*)\s*=?\s*$/gm)) {
        const segments = moduleMatch[1].split('.')
        const moduleName = segments[segments.length - 1]
        if (isUpperIdent(moduleName)) addModule(moduleName, projectPath)
        if (segments.length > 1) addNamespace(segments.slice(0, -1).join('.'), projectPath)
      }
      // 嵌套 `    module Inner =`（缩进声明，RequireQualifiedAccess 常态）。
      for (const nestedMatch of code.matchAll(/^[ \t]+module\s+([A-Za-z_][A-Za-z0-9_']*)\s*=?\s*$/gm)) {
        if (isUpperIdent(nestedMatch[1])) addModule(nestedMatch[1], projectPath)
      }

      for (const typeMatch of code.matchAll(/^[ \t]*(?:type|and)\s+([A-Za-z_][A-Za-z0-9_']*)/gm)) {
        if (isUpperIdent(typeMatch[1])) addType(typeMatch[1], projectPath)
      }
    }
  }

  return { namespaces, modules, types, fileToProject }
}

const lineAt = (code, index) => code.slice(0, index).split('\n').length

/**
 * 审计一个 shard 的全部源文件，返回疑似缺边 findings。
 *
 * @param inventory readCompileShardInventory() 的返回值
 * @param projectPath 被审计 shard 的 fsproj 绝对路径
 * @param index buildSymbolIndex() 的返回值
 * @param readText (absPath) => masked code 的读取函数
 */
export function auditShard(inventory, projectPath, index, readText) {
  const { namespaces, modules, types } = index
  const project = inventory.projects.get(projectPath)
  const closure = transitiveClosure(inventory.projects, projectPath)
  const findings = new Map()

  const record = (kind, symbol, confidence, missingShards, file, line) => {
    const key = `${kind}|${symbol}|${[...missingShards].sort().join(',')}`
    const existing = findings.get(key)
    if (existing) {
      if (existing.evidence.length < 3) existing.evidence.push({ file, line })
      return
    }
    findings.set(key, {
      shard: project.projectRepoPath,
      kind,
      symbol,
      confidence,
      missingShards: [...missingShards].sort(),
      evidence: [{ file, line }],
    })
  }

  const repoFile = (absolute) => absolute.slice(repositoryRoot.length + 1)

  for (const source of project.compileItems) {
    const code = readText(source)
    const file = repoFile(source)

    for (const openMatch of code.matchAll(/^[ \t]*open\s+(Wanxiangshu[A-Za-z0-9_'.]*)/gm)) {
      const namespace = openMatch[1]
      const defining = namespaces.get(namespace)
      if (!defining) continue
      const missing = [...defining].filter((shard) => !closure.has(shard))
      if (missing.length === defining.size) {
        record('open', namespace, 'high', defining, file, lineAt(code, openMatch.index))
      } else if (missing.length > 0) {
        // 部分覆盖：open 本身不报，等具体符号命中兜底。
      }
    }

    for (const qualifiedMatch of code.matchAll(/\b(Wanxiangshu(?:\.[A-Za-z_][A-Za-z0-9_']*)+)/g)) {
      const chain = qualifiedMatch[1]
      const segments = chain.split('.')
      for (let depth = segments.length; depth >= 2; depth -= 1) {
        const prefix = segments.slice(0, depth).join('.')
        const asNamespace = namespaces.get(prefix)
        if (asNamespace) {
          const missing = [...asNamespace].filter((shard) => !closure.has(shard))
          if (missing.length === asNamespace.size) {
            record('qualified', prefix, 'high', asNamespace, file, lineAt(code, qualifiedMatch.index))
          }
          break
        }
        const asModule = modules.get(segments[depth - 1])
        if (asModule) {
          const missing = [...asModule].filter((shard) => !closure.has(shard))
          if (missing.length === asModule.size) {
            record('module-prefix', segments[depth - 1], asModule.size === 1 ? 'high' : 'medium', asModule, file, lineAt(code, qualifiedMatch.index))
          }
          break
        }
      }
    }

    for (const prefixMatch of code.matchAll(/\b([A-Z][A-Za-z0-9_']*)\s*\.\s*[A-Za-z_][A-Za-z0-9_']*/g)) {
      const prefix = prefixMatch[1]
      if (prefix === 'Wanxiangshu') continue
      const asModule = modules.get(prefix)
      if (asModule) {
        const missing = [...asModule].filter((shard) => !closure.has(shard))
        if (missing.length === asModule.size) {
          record('module-prefix', prefix, asModule.size === 1 ? 'high' : 'medium', asModule, file, lineAt(code, prefixMatch.index))
        }
        continue
      }
      const asType = types.get(prefix)
      if (asType && asType.size === 1) {
        const defining = [...asType][0]
        if (!closure.has(defining)) {
          record('static-member', prefix, 'low', asType, file, lineAt(code, prefixMatch.index))
        }
      }
    }

    for (const identMatch of code.matchAll(/\b([A-Z][A-Za-z0-9_']*)\b/g)) {
      const name = identMatch[1]
      const defining = types.get(name)
      if (!defining || defining.size !== 1) continue
      const only = [...defining][0]
      if (!closure.has(only)) {
        record('type-name', name, 'low', defining, file, lineAt(code, identMatch.index))
      }
    }
  }

  return [...findings.values()]
}

/** 全仓审计。readText 返回 mask 后的 code（注释/字符串已遮）。 */
export function auditAllShards(inventory, readRawText) {
  const maskedCache = new Map()
  const readText = (absolute) => {
    const cached = maskedCache.get(absolute)
    if (cached !== undefined) return cached
    const masked = maskFSharpTrivia(readRawText(absolute))
    maskedCache.set(absolute, masked)
    return masked
  }

  const index = buildSymbolIndex(inventory, readText)
  const findings = []
  for (const projectPath of inventory.projects.keys()) {
    findings.push(...auditShard(inventory, projectPath, index, readText))
  }
  return findings.sort((a, b) =>
    a.shard === b.shard
      ? a.symbol.localeCompare(b.symbol)
      : a.shard.localeCompare(b.shard))
}

const formatFinding = (finding) => {
  const evidence = finding.evidence.map((spot) => `${spot.file}:${spot.line}`).join(', ')
  return `[${finding.confidence}] ${finding.shard} -> ${[...finding.missingShards].map((shard) => shard.replace(/^.*\//, '')).join(', ')}: ${finding.kind} '${finding.symbol}' (${evidence})`
}

const toIssue = (finding) => {
  const first = finding.evidence[0]
  return {
    path: finding.shard,
    line: first ? first.line : undefined,
    code: 'shard-edge-missing',
    message: `${finding.kind} '${finding.symbol}' (${finding.confidence}) references shards outside the ProjectReference closure: ${finding.missingShards.join(', ')}. Evidence: ${finding.evidence.map((spot) => `${spot.file}:${spot.line}`).join(', ')}`,
  }
}

/**
 * check.mjs 门禁入口。默认「报告不失败」：清单打印到 stderr，issues 为空，
 * 先观察规模。设 SHARD_EDGE_AUDIT_STRICT=1 升级硬门禁（issues 非空即失败）。
 * 升级硬门禁前须按 verification-system-004 补受控反例回归测试。
 */
export async function check(ctx) {
  const inventory = ctx.compileInventory()
  const findings = auditAllShards(inventory, (absolute) => ctx.readText(absolute))

  if (findings.length === 0) return { issues: [] }

  if (process.env.SHARD_EDGE_AUDIT_STRICT) {
    return { issues: findings.map(toIssue) }
  }

  const high = findings.filter((f) => f.confidence === 'high')
  const medium = findings.filter((f) => f.confidence === 'medium')
  const low = findings.filter((f) => f.confidence === 'low')
  console.error(`shard-edge-audit (report-only): ${findings.length} finding(s) [high=${high.length} medium=${medium.length} low=${low.length}]`)
  console.error('  Set SHARD_EDGE_AUDIT_STRICT=1 to make this gate fail. Findings:')
  for (const finding of findings) console.error(`  ${formatFinding(finding)}`)
  return { issues: [] }
}

export async function main(argv = process.argv.slice(2)) {
  const json = argv.includes('--json')
  const strict = argv.includes('--strict')
  const shardFilter = argv.includes('--shard') ? argv[argv.indexOf('--shard') + 1] : null

  const ctx = createCheckContext({ root: repositoryRoot })
  const inventory = ctx.compileInventory()
  let findings = auditAllShards(inventory, (absolute) => ctx.readText(absolute))
  if (shardFilter) findings = findings.filter((f) => f.shard.includes(shardFilter))

  if (json) {
    console.log(JSON.stringify({ shardCount: inventory.projects.size, findings }, null, 2))
  } else {
    console.log(`Audited ${inventory.projects.size} shards: ${findings.length} finding(s)`)
    for (const finding of findings) console.log(formatFinding(finding))
  }
  return strict && findings.length > 0 ? 1 : 0
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href
if (isMain) {
  main(process.argv.slice(2)).then((code) => {
    process.exit(code)
  })
}
