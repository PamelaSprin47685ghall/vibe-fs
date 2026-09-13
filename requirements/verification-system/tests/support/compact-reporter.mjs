// requirements/verification-system/tests/support/compact-reporter.mjs
//
// Compact reporter consuming node:test TestsStream events.
// Default mode: suppresses per-test success chatter, prints compact summary to stderr.
// Verbose mode (--verbose or NODE_TEST_VERBOSE=1): prints per-test lines to stdout.
// Test stdout / stderr is always tee'd (never suppressed).
// Failures always print full details.

import util from 'node:util'

export function isVerbose(options = {}) {
  if (options.verbose !== undefined) return Boolean(options.verbose)
  if (process.env.NODE_TEST_VERBOSE === '1' || process.env.NODE_TEST_VERBOSE === 'true') return true
  if (process.argv.includes('--verbose')) return true
  return false
}

/**
 * Creates an async generator reporter function suitable for stream.compose(...) or manual iteration.
 *
 * @param {Object} [options]
 * @param {boolean} [options.verbose]
 * @param {NodeJS.WritableStream} [options.stdout]
 * @param {NodeJS.WritableStream} [options.stderr]
 * @param {Function} [options.onSummary]
 * @returns {AsyncGeneratorFunction}
 */
export function createCompactReporter(options = {}) {
  const verbose = isVerbose(options)
  const out = options.stdout ?? process.stdout
  const err = options.stderr ?? process.stderr

  return async function* compactReporter(source) {
    const startTime = Date.now()
    let wallMs = 0
    let sumTestMs = 0
    let passed = 0
    let failed = 0
    let skipped = 0
    let todo = 0
    let cancelled = 0
    const fileSet = new Set()
    const byFileMap = new Map()
    const failures = []

    const getFileRecord = (file) => {
      const key = file || '(unknown)'
      if (!byFileMap.has(key)) {
        byFileMap.set(key, { file: key, testCount: 0, passed: 0, failed: 0, durationMs: 0 })
      }
      return byFileMap.get(key)
    }

    for await (const event of source) {
      const type = event?.type
      const data = event?.data ?? {}

      if (type === 'test:stdout' && typeof data.message === 'string') {
        out.write(data.message)
        continue
      }
      if (type === 'test:stderr' && typeof data.message === 'string') {
        err.write(data.message)
        continue
      }

      if (type === 'test:pass' || type === 'test:fail') {
        const isSuite = data.details?.type === 'suite'
        const isFileWrapper =
          typeof data.file === 'string' &&
          typeof data.name === 'string' &&
          (data.file === data.name || data.file.endsWith(data.name))

        // In node:test, file wrapper or suite containers should not count as individual leaf tests
        if (isFileWrapper || isSuite) {
          continue
        }

        const filePath = typeof data.file === 'string' ? data.file : ''
        if (filePath) fileSet.add(filePath)
        const fileRecord = getFileRecord(filePath)
        fileRecord.testCount += 1

        const ms = Number(data.details?.duration_ms)
        const duration = Number.isFinite(ms) && ms >= 0 ? ms : 0
        sumTestMs += duration
        fileRecord.durationMs += duration

        const isSkip = Boolean(data.skip)
        const isTodo = Boolean(data.todo)
        const isCancelled = Boolean(data.cancelled)

        if (type === 'test:fail') {
          failed += 1
          fileRecord.failed += 1
          failures.push({
            name: data.name ?? '<unnamed>',
            file: data.file,
            line: data.line,
            column: data.column,
            durationMs: duration,
            error: data.details?.error,
          })

          const failLine = `✖ ${data.name ?? '<unnamed>'} (${duration.toFixed(3)}ms)\n`
          if (verbose) {
            out.write(failLine)
          }
        } else if (isSkip) {
          skipped += 1
          if (verbose) {
            out.write(`﹣ ${data.name ?? '<unnamed>'} (${duration.toFixed(3)}ms) # SKIP\n`)
          }
        } else if (isTodo) {
          todo += 1
          if (verbose) {
            out.write(`✔ ${data.name ?? '<unnamed>'} (${duration.toFixed(3)}ms) # TODO\n`)
          }
        } else if (isCancelled) {
          cancelled += 1
          if (verbose) {
            out.write(`✖ ${data.name ?? '<unnamed>'} (${duration.toFixed(3)}ms) # CANCELLED\n`)
          }
        } else {
          passed += 1
          fileRecord.passed += 1
          if (verbose) {
            out.write(`✔ ${data.name ?? '<unnamed>'} (${duration.toFixed(3)}ms)\n`)
          }
        }
      }

      if (type === 'test:summary' && Number.isFinite(data.duration_ms)) {
        wallMs = data.duration_ms
      }
    }

    if (wallMs === 0) {
      wallMs = Date.now() - startTime
    }

    // Always print failures if any
    if (failures.length > 0) {
      err.write('\n✖ failing tests:\n\n')
      for (const f of failures) {
        const loc = f.file ? `test at ${f.file}${f.line ? `:${f.line}:${f.column || 1}` : ''}\n` : ''
        err.write(`${loc}✖ ${f.name} (${f.durationMs.toFixed(3)}ms)\n`)
        const errObj = f.error?.cause ?? f.error
        if (errObj) {
          const formatted = errObj?.stack || util.inspect(errObj, { colors: false })
          const indented = formatted
            .split('\n')
            .map((line) => `  ${line}`)
            .join('\n')
          err.write(`${indented}\n\n`)
        }
      }
    }

    const byFile = Array.from(byFileMap.values())
    const summary = {
      files: fileSet.size,
      passed,
      failed,
      skipped,
      todo,
      cancelled,
      wallMs,
      sumTestMs,
      byFile,
    }

    // Default mode prints compact summary to stderr
    const summaryText =
      `\n[test-summary] ${fileSet.size} file(s), ${passed} passed, ${failed} failed` +
      (skipped > 0 ? `, ${skipped} skipped` : '') +
      (todo > 0 ? `, ${todo} todo` : '') +
      (cancelled > 0 ? `, ${cancelled} cancelled` : '') +
      ` (${(wallMs / 1000).toFixed(2)}s wall, ${(sumTestMs / 1000).toFixed(2)}s test time)\n`
    err.write(summaryText)

    options.onSummary?.(summary)
  }
}
