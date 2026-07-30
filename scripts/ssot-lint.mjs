#!/usr/bin/env node
// SSOT/STATUS 纯文本规范检查。
// 休克期唯一允许的规范反馈通道：不编译、不运行测试，只验证规范文本自身一致。
//
// 检查项：
//   1. 条款 ID 唯一（同一 ID 只允许定义一次）
//   2. 无悬空引用（正文引用的 ID 必须有定义）
//   3. 前缀归属正确（ID 前缀必须出现在 SSOT/00 索引指定的文件里）
//   4. 词汇表 SSOT/99 指向的条款存在
//   5. 禁止 SSOT 出现 STATUS 专属状态词（规范不描述实现进度）
//
// 用法：node scripts/ssot-lint.mjs

import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const SSOT_DIR = 'SSOT'
const CLAUSE_RE = /\b(ARCH|AGENT|PROMPT|FALLBACK|REVIEW|ORCH|HOST|COMPANION|EXEC|VERIFY|PERSIST|CTX)-(\d{3})\b/g
const DEFINITION_RE =
  /^##\s+((?:ARCH|AGENT|PROMPT|FALLBACK|REVIEW|ORCH|HOST|COMPANION|EXEC|VERIFY|PERSIST|CTX)-\d{3})/gm

/** SSOT/00 声明的前缀 → 文件归属。硬编码是故意的：这是规范的一部分。 */
const PREFIX_OWNER = {
  ARCH: '01.md',
  AGENT: '02.md',
  PROMPT: '03.md',
  FALLBACK: '04.md',
  REVIEW: '05.md',
  ORCH: '06.md',
  HOST: '07.md',
  COMPANION: '08.md',
  EXEC: '09.md',
  VERIFY: '10.md',
  PERSIST: '11.md',
  CTX: '12.md',
}

/** SSOT 是规范，不是状态报告。这些词属于 STATUS/。 */
const STATUS_ONLY_WORDS = ['NOT_IMPLEMENTED', 'PARTIAL', 'CONTRADICTS', 'UNVERIFIED', 'CONFORMANT']

const failures = []
const fail = (file, line, msg) => failures.push({ file, line, msg })

const files = readdirSync(SSOT_DIR)
  .filter((f) => f.endsWith('.md'))
  .sort()

/** @type {Map<string, {file: string, line: number}>} */
const definitions = new Map()
/** @type {{id: string, file: string, line: number}[]} */
const references = []
const sources = new Map()

for (const file of files) {
  const text = readFileSync(join(SSOT_DIR, file), 'utf8')
  sources.set(file, text)
  const lines = text.split('\n')

  for (const match of text.matchAll(DEFINITION_RE)) {
    const id = match[1]
    const line = text.slice(0, match.index).split('\n').length
    const previous = definitions.get(id)
    if (previous) {
      fail(file, line, `条款 ID 重复定义：${id}（已在 ${previous.file}:${previous.line} 定义）`)
      continue
    }
    definitions.set(id, { file, line })

    const prefix = id.split('-')[0]
    const owner = PREFIX_OWNER[prefix]
    if (owner && file !== owner) {
      fail(file, line, `条款 ${id} 定义在 ${file}，但 SSOT/00 索引规定 ${prefix}- 属于 SSOT/${owner}`)
    }
  }

  lines.forEach((content, index) => {
    for (const match of content.matchAll(CLAUSE_RE)) {
      references.push({ id: match[0], file, line: index + 1 })
    }
    for (const word of STATUS_ONLY_WORDS) {
      if (content.includes(word)) {
        fail(file, index + 1, `SSOT 不得出现实现状态词 "${word}"（属于 STATUS/conformance.md）`)
      }
    }
  })
}

for (const { id, file, line } of references) {
  if (!definitions.has(id)) {
    fail(file, line, `悬空条款引用：${id} 无定义`)
  }
}

// SSOT/00 索引表必须覆盖所有实际存在的规范文件
const navigation = sources.get('00.md') ?? ''
for (const file of files) {
  if (file === '00.md') continue
  if (!navigation.includes(`SSOT/${file}`)) {
    fail('00.md', 0, `导航索引缺少 SSOT/${file}`)
  }
}

// 每个条款前缀至少被 SSOT/00 提及一次
for (const prefix of Object.keys(PREFIX_OWNER)) {
  if (!navigation.includes(`\`${prefix}-\``)) {
    fail('00.md', 0, `导航索引缺少条款前缀 ${prefix}-`)
  }
}

const definedCount = definitions.size
if (failures.length === 0) {
  console.log(`ssot-lint: OK — ${definedCount} 条款，${references.length} 处引用，${files.length} 个文件`)
  process.exit(0)
}

console.error(`ssot-lint: ${failures.length} 处问题`)
for (const { file, line, msg } of failures) {
  console.error(`  SSOT/${file}:${line}  ${msg}`)
}
process.exit(1)
