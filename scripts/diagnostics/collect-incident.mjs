#!/usr/bin/env node

/**
 * 万象术 & OpenCode 精准事故现场专项目录采集脚本 (Incident Log Bundle)
 * 
 * 精准原则：
 * 1. 批次锚定 (Run-Scoped)：根据 run=<runId> 边界提取当前运行周期完整日志，排除历史干扰。
 * 2. 核心要素提炼：直接提取会话 SessionId、角色 Agent、模型 Model 与 Provider。
 * 3. 原生崩溃解析：支持 macOS .ips 转储信号提取 (SIGTRAP / SIGSEGV 等)。
 * 4. 事件账本切片：全量 ndjson 快照 + 末尾 10 条 Fact 跃迁切片。
 * 5. 插件产物检查：自动核查 dist/OpenCode/Plugin/Plugin.js 状态。
 * 6. 安全脱敏：敏感 API Key 自动掩码处理。
 */

import fs from 'node:fs'
import path from 'node:path'
import os from 'node:os'
import { execSync } from 'node:child_process'

function formatDateTime(d) {
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}-${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}`
}

const now = new Date()
const timeStr = formatDateTime(now)
const userTag = process.argv.slice(2).join('-').replace(/[^a-zA-Z0-9_\u4e00-\u9fa5-]/g, '').trim()

// ==========================================
// 1. 精准分析 OpenCode 主运行时日志
// ==========================================
const ocLogPath = path.join(os.homedir(), '.local', 'share', 'opencode', 'log', 'opencode.log')
let ocAllLines = []
let targetRunId = null
let runLines = []
let lastErrorLines = []
let sessionContext = {
  sessionId: '未知',
  agent: '未知',
  providerId: '未知',
  modelId: '未知',
  lastErrorMessage: '未知'
}
let autoErrorTag = 'crash'

if (fs.existsSync(ocLogPath)) {
  try {
    const raw = fs.readFileSync(ocLogPath, 'utf8')
    ocAllLines = raw.split('\n').filter(Boolean)

    for (let i = ocAllLines.length - 1; i >= 0; i--) {
      const line = ocAllLines[i]
      const isErr = /level=ERROR|FATAL|panic|IntentRejected|InvalidExplicitAgent|Unexpected|ProviderModelNotFound/i.test(line)
      const m = line.match(/\brun=([a-f0-9]+)\b/)
      if (isErr && m && !targetRunId) {
        targetRunId = m[1]
      }
      if (isErr) {
        lastErrorLines.unshift(line)
        if (lastErrorLines.length >= 15) break
      }
    }

    if (!targetRunId && ocAllLines.length > 0) {
      const lastLine = ocAllLines[ocAllLines.length - 1]
      const m = lastLine.match(/\brun=([a-f0-9]+)\b/)
      if (m) targetRunId = m[1]
    }

    if (targetRunId) {
      runLines = ocAllLines.filter(l => l.includes(`run=${targetRunId}`))
    } else {
      runLines = ocAllLines.slice(-200)
    }

    for (const l of runLines) {
      const sMatch = l.match(/\bsession\.id=([a-zA-Z0-9_-]+)\b/)
      if (sMatch) sessionContext.sessionId = sMatch[1]

      const aMatch = l.match(/\bagent=([a-zA-Z0-9_-]+)\b/)
      if (aMatch) sessionContext.agent = aMatch[1]

      const pMatch = l.match(/\bproviderID=([a-zA-Z0-9_-]+)\b/)
      if (pMatch) sessionContext.providerId = pMatch[1]

      const mMatch = l.match(/\bmodelID=([a-zA-Z0-9_.\/:-]+)\b/)
      if (mMatch) sessionContext.modelId = mMatch[1]

      const errMsgMatch = l.match(/\berror="([^"]+)"/) || l.match(/\berror=([^\s]+)/)
      if (errMsgMatch) sessionContext.lastErrorMessage = errMsgMatch[1]
    }

    const errBlock = lastErrorLines.join('\n')
    if (errBlock.includes('InvalidExplicitAgent')) autoErrorTag = 'InvalidAgent'
    else if (errBlock.includes('Model not found') || errBlock.includes('ProviderModelNotFound')) autoErrorTag = 'ModelNotFound'
    else if (errBlock.includes('IntentRejected')) autoErrorTag = 'IntentRejected'
    else if (errBlock.includes('TimeoutError') || errBlock.includes('timed out')) autoErrorTag = 'Timeout'
    else if (errBlock.includes('not a git repository')) autoErrorTag = 'NotGitRepo'
    else if (errBlock.includes('Aborted')) autoErrorTag = 'Aborted'
    else if (lastErrorLines.length > 0) autoErrorTag = 'Error'
  } catch {}
}

const finalTag = userTag || autoErrorTag
const incidentDirName = `incident-${timeStr}-${finalTag}`

let baseDiagnosticsDir = path.join(process.cwd(), '.diagnostics')
try {
  execSync('git rev-parse --is-inside-work-tree 2>/dev/null')
} catch {
  baseDiagnosticsDir = path.join(os.homedir(), '.local', 'state', 'wanxiang', 'diagnostics')
}

const incidentPath = path.join(baseDiagnosticsDir, incidentDirName)
fs.mkdirSync(incidentPath, { recursive: true })

// ==========================================
// 2. 收集环境与 Git 精确状态
// ==========================================
let gitBranch = 'unknown'
let gitCommit = 'unknown'
let gitCommonDir = null
let gitStatus = 'clean'
try {
  gitBranch = execSync('git rev-parse --abbrev-ref HEAD 2>/dev/null', { encoding: 'utf8' }).trim()
  gitCommit = execSync('git log -1 --format="%h - %s (%ci)" 2>/dev/null', { encoding: 'utf8' }).trim()
  const rawCommon = execSync('git rev-parse --git-common-dir 2>/dev/null', { encoding: 'utf8' }).trim()
  gitCommonDir = path.resolve(process.cwd(), rawCommon)
  gitStatus = execSync('git status -s 2>/dev/null', { encoding: 'utf8' }).trim() || 'clean'
} catch {}

let opencodeVersion = 'unknown'
try {
  opencodeVersion = execSync('opencode --version 2>/dev/null', { encoding: 'utf8' }).trim()
} catch {}

// ==========================================
// 3. 原生系统崩溃报告深度提炼 (.ips)
// ==========================================
let systemCrashDetail = null
let capturedSystemReports = []

if (os.platform() === 'darwin') {
  const diagDir = path.join(os.homedir(), 'Library', 'Logs', 'DiagnosticReports')
  if (fs.existsSync(diagDir)) {
    try {
      const nowMs = Date.now()
      const oneDayMs = 24 * 60 * 60 * 1000
      const reports = fs.readdirSync(diagDir)
        .filter(f => /^(opencode|node|bun).*\.ips$/i.test(f))
        .map(f => ({
          name: f,
          fullPath: path.join(diagDir, f),
          mtime: fs.statSync(path.join(diagDir, f)).mtimeMs
        }))
        .filter(r => (nowMs - r.mtime) <= oneDayMs)
        .sort((a, b) => b.mtime - a.mtime)

      if (reports.length > 0) {
        const topReport = reports[0]
        const destName = `system-crash-${topReport.name}`
        fs.copyFileSync(topReport.fullPath, path.join(incidentPath, destName))
        capturedSystemReports.push(destName)

        const ipsRaw = fs.readFileSync(topReport.fullPath, 'utf8')
        try {
          const lines = ipsRaw.split('\n')
          for (const line of lines) {
            const trimmed = line.trim()
            if (trimmed.startsWith('{') && (trimmed.includes('"exception"') || trimmed.includes('"termination"'))) {
              const parsed = JSON.parse(trimmed)
              systemCrashDetail = {
                signal: parsed.exception?.signal || parsed.termination?.indicator || parsed.termination?.signal || '已转储',
                exceptionType: parsed.exception?.type || 'NativeCrash',
                terminationBy: parsed.termination?.byProc || 'System',
                faultingThread: parsed.faultingThread
              }
              break
            }
          }
        } catch {}
      }
    } catch {}
  }
}

// ==========================================
// 4. 精炼写入万象术事件账本快照与尾部切片
// ==========================================
let latestJournalFile = null
let journalEventCount = 0
let recentJournalTail = []

if (gitCommonDir) {
  const eventsDir = path.join(gitCommonDir, 'wanxiang', 'events')
  if (fs.existsSync(eventsDir)) {
    const ndjsons = fs.readdirSync(eventsDir)
      .filter(f => f.endsWith('.ndjson'))
      .map(f => ({
        name: f,
        fullPath: path.join(eventsDir, f),
        mtime: fs.statSync(path.join(eventsDir, f)).mtimeMs
      }))
      .sort((a, b) => b.mtime - a.mtime)

    if (ndjsons.length > 0) {
      latestJournalFile = ndjsons[0].name
      fs.copyFileSync(ndjsons[0].fullPath, path.join(incidentPath, 'latest-events.ndjson'))
      
      const content = fs.readFileSync(ndjsons[0].fullPath, 'utf8')
      const lines = content.split('\n').filter(Boolean)
      journalEventCount = lines.length

      recentJournalTail = lines.slice(-10).map(l => {
        try {
          const j = JSON.parse(l)
          return {
            event_id: j.event_id,
            event_type: j.event_type,
            fact_type: Array.isArray(j.payload?.Fact) ? j.payload.Fact[0] : (typeof j.payload?.Fact === 'string' ? j.payload.Fact : 'UnknownFact')
          }
        } catch {
          return { raw: l.slice(0, 100) }
        }
      })
      fs.writeFileSync(path.join(incidentPath, 'events-tail.json'), JSON.stringify(recentJournalTail, null, 2), 'utf8')
    }
  }
}

// ==========================================
// 5. 检查万象术插件产物与配置
// ==========================================
let pluginBuildStatus = '未检测到项目编译产物'
const pluginArtifactPath = path.join(process.cwd(), 'dist', 'OpenCode', 'Plugin', 'Plugin.js')
if (fs.existsSync(pluginArtifactPath)) {
  try {
    const stat = fs.statSync(pluginArtifactPath)
    pluginBuildStatus = `产物就绪 (${(stat.size / 1024).toFixed(1)} KB, 变更于 ${new Date(stat.mtimeMs).toLocaleString()})`
  } catch {}
}

const mjsPath = path.join(os.homedir(), '.config', 'opencode', 'wanxiangshu.mjs')
let mjsSyntax = '文件未找到'
if (fs.existsSync(mjsPath)) {
  try {
    execSync(`node --check "${mjsPath}" 2>&1`)
    mjsSyntax = '语法正确 (Syntax OK)'
  } catch (err) {
    mjsSyntax = `语法异常 (Syntax Error): ${err.message}`
  }
  fs.copyFileSync(mjsPath, path.join(incidentPath, 'wanxiangshu.mjs'))
}

const jsonPath = path.join(os.homedir(), '.config', 'opencode', 'opencode.json')
if (fs.existsSync(jsonPath)) {
  try {
    const rawCfg = JSON.parse(fs.readFileSync(jsonPath, 'utf8'))
    if (rawCfg.provider) {
      for (const p of Object.values(rawCfg.provider)) {
        if (p.options && p.options.apiKey) {
          const k = String(p.options.apiKey)
          p.options.apiKey = k.length > 8 ? `${k.slice(0, 4)}...${k.slice(-4)}` : '******'
        }
      }
    }
    fs.writeFileSync(path.join(incidentPath, 'opencode-config.json'), JSON.stringify(rawCfg, null, 2), 'utf8')
  } catch {
    fs.copyFileSync(jsonPath, path.join(incidentPath, 'opencode-config.json'))
  }
}

fs.writeFileSync(path.join(incidentPath, 'run-scoped.log'), runLines.join('\n'), 'utf8')

const metadata = {
  incidentId: incidentDirName,
  timestamp: now.toISOString(),
  localTime: now.toLocaleString(),
  tag: finalTag,
  runId: targetRunId,
  sessionContext,
  systemCrashDetail,
  pluginBuildStatus,
  environment: {
    nodeVersion: process.version,
    platform: os.platform(),
    arch: os.arch(),
    opencodeVersion,
    cwd: process.cwd(),
    git: {
      branch: gitBranch,
      commit: gitCommit,
      status: gitStatus
    }
  }
}
fs.writeFileSync(path.join(incidentPath, 'metadata.json'), JSON.stringify(metadata, null, 2), 'utf8')

// ==========================================
// 6. 生成高精准事故简报 summary.md
// ==========================================
const summaryMd = `# 事故现场精准诊断报告: ${incidentDirName}

- **发生时间**: ${metadata.localTime}
- **事故分类标签**: \`${finalTag}\`
- **运行批次 (RunID)**: \`${targetRunId || '未捕获'}\` (已隔离提取该批次共 ${runLines.length} 行真实上下文)
- **代码基线**: 分支 \`${gitBranch}\` | 提交 \`${gitCommit}\`
- **事故专项目录**: \`${incidentPath}\`

---

## 1. 核心异常定位 (Accident Profile)
| 关键指标 | 现场事实 |
| :--- | :--- |
| **发生会话 (SessionId)** | \`${sessionContext.sessionId}\` |
| **触发角色 (Agent)** | \`${sessionContext.agent}\` |
| **目标模型 (Model/Provider)** | \`${sessionContext.providerId} / ${sessionContext.modelId}\` |
| **最后错误概要** | \`${sessionContext.lastErrorMessage}\` |
| **系统级信号 (OS Signal)** | ${systemCrashDetail ? `\`${systemCrashDetail.signal}\` (${systemCrashDetail.exceptionType})` : (capturedSystemReports.length > 0 ? '已捕获底层转储文件' : '无底层崩溃信号')} |

---

## 2. 现场最新错误流水切片 (Error Log Snippet)
\`\`\`text
${lastErrorLines.join('\n') || '[未捕获到显式 ERROR 标记，请查阅 run-scoped.log]'}
\`\`\`

---

## 3. 万象术组件与证据快照
| 检查项 | 检查结论 | 证据文件 |
| :--- | :--- | :--- |
| **插件编译产物 (Plugin.js)** | ${pluginBuildStatus} | \`dist/OpenCode/Plugin/Plugin.js\` |
| **操作系统核心转储 (.ips)** | ${capturedSystemReports.length > 0 ? `已固化近 24 小时底层崩溃报告` : '近 24 小时无底层崩溃'} | ${capturedSystemReports.length > 0 ? `\`${capturedSystemReports[0]}\`` : '无'} |
| **批次运行时日志 (Run-Scoped)** | 精准截取本生命周期共 ${runLines.length} 行日志 | \`run-scoped.log\` |
| **模型调度器 (wanxiangshu.mjs)** | ${mjsSyntax} | \`wanxiangshu.mjs\` |
| **持久化事件账本 (EventStore)** | ${latestJournalFile ? `已捕获账本 \`${latestJournalFile}\` (共 ${journalEventCount} 条，含末尾事件切片)` : '未检测到账本文件'} | \`latest-events.ndjson\` / \`events-tail.json\` |
| **OpenCode 客户端配置** | 已脱敏固化当前 Provider 与 Agent 映射 | \`opencode-config.json\` |

---

## 4. 本次事故目录清单
\`\`\`text
${incidentDirName}/
├── summary.md            # 事故全景精准总览（优先阅读）
├── metadata.json         # 结构化事故元数据（供自动化/AI检索）
├── run-scoped.log        # 本次运行周期的完整上下文（去粗取精）
├── events-tail.json      # 崩溃前万象术内核最后 10 条状态机跳变切片
├── latest-events.ndjson  # 万象术完整事件账本原始快照
├── wanxiangshu.mjs       # 当时的调度策略快照
├── opencode-config.json  # 当时的模型凭据配置快照
${capturedSystemReports.map(f => `├── ${f}  # 操作系统核心转储`).join('\n')}
\`\`\`
`

fs.writeFileSync(path.join(incidentPath, 'summary.md'), summaryMd, 'utf8')

console.log(`\n🚨 精准事故现场已完整固化！`)
console.log(`📁 事故专项目录：${incidentPath}`)
console.log(`\n📌 查看精准简报：`)
console.log(`   cat "${path.join(incidentPath, 'summary.md')}"`)
console.log(`\n💡 在 Cursor 聊天框中直接输入：`)
console.log(`   “分析本次事故目录 ${incidentPath}”\n`)
