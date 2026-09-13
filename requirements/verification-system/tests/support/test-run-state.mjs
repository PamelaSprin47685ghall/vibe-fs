// requirements/verification-system/tests/support/test-run-state.mjs
//
// 本模块是 TestsStream 归一化与运行状态的唯一属主。
// 纯函数式/受控可变累加器，无 IO 无终端输出。
//

import { resolve } from 'node:path'

/**
 * 判定事件是否为文件完成事件（file wrapper test:complete）。
 *
 * 语义：event.type === 'test:complete' 且 data.file 是 string 且
 * （data.name === data.file 或 resolve(data.name) === resolve(data.file)）。
 *
 * @param {any} event
 * @returns {boolean}
 */
export function isFileCompletionEvent(event) {
  if (event?.type !== 'test:complete') return false
  const file = event?.data?.file
  const name = event?.data?.name
  if (typeof file !== 'string' || typeof name !== 'string') return false
  return name === file || resolve(name) === resolve(file)
}

/**
 * 判定事件是否为文件包装器（file wrapper），不管是 test:start, test:pass, test:fail 还是 test:complete。
 *
 * @param {any} event
 * @returns {boolean}
 */
export function isFileWrapper(event) {
  const file = event?.data?.file
  const name = event?.data?.name
  if (typeof file !== 'string' || typeof name !== 'string') return false
  return name === file || resolve(name) === resolve(file)
}

/**
 * 创建一个不可变/受控可变状态容器。
 *
 * @returns {TestRunState}
 */
export function createRunState() {
  return new TestRunState()
}

export class TestRunState {
  constructor() {
    this._startTime = Date.now()
    this._explicitWallMs = null
    this._filesCompleted = new Set()
    this._fileSet = new Set()
    this._byFile = new Map() // filePath -> { file, testCount, passed, failed, durationMs }
    this._leaves = new Map() // key -> { file, name, nesting, status, durationMs, details, error }
    this._containerFailures = [] // { name, file, nesting, durationMs, error }
    this._leafDurations = [] // { name, ms }
    this._failures = [] // { name, file, line, column, durationMs, error }
  }

  _getFileRecord(file) {
    const key = file || '(unknown)'
    if (!this._byFile.has(key)) {
      this._byFile.set(key, { file: key, testCount: 0, passed: 0, failed: 0, durationMs: 0 })
    }
    return this._byFile.get(key)
  }

  /**
   * 应用单个 TestsStream 事件。
   *
   * @param {any} event
   */
  applyEvent(event) {
    if (!event || typeof event !== 'object') return

    const type = event.type
    const data = event.data ?? {}

    if (type === 'test:summary') {
      if (Number.isFinite(data.duration_ms)) {
        this._explicitWallMs = data.duration_ms
      }
      return
    }

    if (isFileCompletionEvent(event)) {
      if (typeof data.file === 'string') {
        this._filesCompleted.add(resolve(data.file))
      }
      return
    }

    // 文件级 wrapper (例如整个文件以 test:complete / test:pass / test:fail 出现)
    if (isFileWrapper(event)) {
      if (typeof data.file === 'string') {
        this._fileSet.add(data.file)
      }
      return
    }

    if (type === 'test:start') {
      const file = typeof data.file === 'string' ? data.file : ''
      if (file) this._fileSet.add(file)
      return
    }

    if (type === 'test:pass' || type === 'test:fail') {
      const isSuite = data.details?.type === 'suite'
      const isSubtestsParent = data.details?.error?.failureType === 'subtestsFailed'
      const isContainer = isSuite || isSubtestsParent

      const ms = Number(data.details?.duration_ms ?? data.durationMs)
      const duration = Number.isFinite(ms) && ms >= 0 ? ms : 0
      const filePath = typeof data.file === 'string' ? data.file : ''
      if (filePath) this._fileSet.add(filePath)

      if (isContainer) {
        if (type === 'test:fail') {
          this._containerFailures.push({
            name: data.name ?? '<unnamed suite>',
            file: data.file,
            nesting: data.nesting,
            durationMs: duration,
            error: data.details?.error,
          })
        }
        return
      }

      // 判定为叶子测试 (leaf test)
      const file = filePath || '(unknown)'
      const name = data.name ?? '<unnamed>'
      const nesting = Number(data.nesting ?? 0)

      // Key 组合：优先用 testId，否则 file + nesting + name
      const key = data.testId != null
        ? `${file}::${data.testId}`
        : `${file}::${nesting}::${name}`

      const isSkip = Boolean(data.skip)
      const isTodo = Boolean(data.todo)
      const isCancelled = Boolean(data.cancelled || data.details?.error?.failureType === 'testAborted')

      let verdict = 'pass'
      if (type === 'test:fail') {
        verdict = 'fail'
      } else if (isSkip) {
        verdict = 'skip'
      } else if (isTodo) {
        verdict = 'todo'
      } else if (isCancelled) {
        verdict = 'cancelled'
      }

      const fileRecord = this._getFileRecord(filePath)
      const isExisting = this._leaves.has(key)

      this._leaves.set(key, {
        file: filePath,
        name,
        nesting,
        verdict,
        durationMs: duration,
        error: data.details?.error,
      })

      // 仅当首次记录叶子或从非判定状态转为判定时更新度量
      if (!isExisting) {
        fileRecord.testCount += 1
        fileRecord.durationMs += duration
        this._leafDurations.push({ name, ms: duration })

        if (verdict === 'pass') {
          fileRecord.passed += 1
        } else if (verdict === 'fail') {
          fileRecord.failed += 1
          this._failures.push({
            name,
            file: data.file,
            line: data.line,
            column: data.column,
            durationMs: duration,
            error: data.details?.error,
          })
        }
      }
    }
  }

  /**
   * 生成当前状态的只读汇总报告。
   *
   * @returns {{
   *   files: number,
   *   filesCompleted: number,
   *   passed: number,
   *   failed: number,
   *   skipped: number,
   *   todo: number,
   *   cancelled: number,
   *   containerFailures: number,
   *   leafDurations: Array<{ name: string, ms: number }>,
   *   failures: Array<{ name: string, file?: string, line?: number, column?: number, durationMs: number, error?: any }>,
   *   byFile: Array<{ file: string, testCount: number, passed: number, failed: number, durationMs: number }>,
   *   wallMs: number,
   *   sumTestMs: number,
   * }}
   */
  summarize() {
    let passed = 0
    let failed = 0
    let skipped = 0
    let todo = 0
    let cancelled = 0
    let sumTestMs = 0

    for (const leaf of this._leaves.values()) {
      sumTestMs += leaf.durationMs
      switch (leaf.verdict) {
        case 'pass':
          passed += 1
          break
        case 'fail':
          failed += 1
          break
        case 'skip':
          skipped += 1
          break
        case 'todo':
          todo += 1
          break
        case 'cancelled':
          cancelled += 1
          break
      }
    }

    const wallMs = this._explicitWallMs != null
      ? this._explicitWallMs
      : Date.now() - this._startTime

    return {
      files: this._fileSet.size,
      filesCompleted: this._filesCompleted.size,
      passed,
      failed,
      skipped,
      todo,
      cancelled,
      containerFailures: this._containerFailures.length,
      leafDurations: [...this._leafDurations],
      failures: [...this._failures],
      byFile: Array.from(this._byFile.values()).map((rec) => ({ ...rec })),
      wallMs,
      sumTestMs,
    }
  }
}

/**
 * 纯函数式事件应用入口，支持 applyEvent(state, event)。
 *
 * @param {TestRunState} state
 * @param {any} event
 * @returns {TestRunState}
 */
export function applyEvent(state, event) {
  state.applyEvent(event)
  return state
}

/**
 * 纯函数式汇总入口，支持 summarize(state)。
 *
 * @param {TestRunState} state
 */
export function summarize(state) {
  return state.summarize()
}
