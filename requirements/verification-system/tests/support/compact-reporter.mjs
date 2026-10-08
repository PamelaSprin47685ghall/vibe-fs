// requirements/verification-system/tests/support/compact-reporter.mjs
//
// Compact reporter consuming node:test TestsStream events.
// Default mode: suppresses per-test success chatter, prints compact summary to stderr.
// Verbose mode (--verbose or NODE_TEST_VERBOSE=1): prints per-test lines to stdout.
// Test stdout / stderr is always tee'd (never suppressed).
// Failures always print full details.

import util from 'node:util'
import { createRunState, isFileWrapper, applyEvent, summarize } from './test-run-state.mjs'

export function isVerbose(options = {}) {
  if (options.verbose !== undefined) return Boolean(options.verbose)
  if (process.env.NODE_TEST_VERBOSE === '1' || process.env.NODE_TEST_VERBOSE === 'true') return true
  if (process.argv.includes('--verbose')) return true
  return false
}

function printFailure(failure, stderr) {
  const sourceFile = failure.sourceFile || failure.file
  if (failure.file && failure.file !== sourceFile) stderr.write(`test entry ${failure.file}\n`)
  const location = sourceFile ? `test at ${sourceFile}${failure.line ? `:${failure.line}:${failure.column || 1}` : ''}\n` : ''
  stderr.write(`${location}✖ ${failure.name} (${failure.durationMs.toFixed(3)}ms)\n`)
  const error = failure.error?.cause ?? failure.error
  if (error) {
    const formatted = error?.stack || util.inspect(error, { colors: false })
    stderr.write(formatted.split('\n').map(line => `  ${line}`).join('\n') + '\n\n')
  }
}

/**
 * Creates an async generator reporter function suitable for stream.compose(...) or manual iteration.
 *
 * @param {Object} [options]
 * @param {boolean} [options.verbose]
 * @param {NodeJS.WritableStream} [options.stdout]
 * @param {NodeJS.WritableStream} [options.stderr]
 * @param {Function} [options.onSummary]
 * @param {import('./test-run-state.mjs').TestRunState} [options.state]
 * @returns {AsyncGeneratorFunction & { state: import('./test-run-state.mjs').TestRunState }}
 */
export function createCompactReporter(options = {}) {
  const verbose = isVerbose(options)
  const out = options.stdout ?? process.stdout
  const err = options.stderr ?? process.stderr
  const state = options.state ?? createRunState()

  const reporterFn = async function* compactReporter(source) {
    const printedFailures = new Set()
    let failureHeaderPrinted = false
    const printNewFailures = summary => {
      for (const failure of [...summary.failures, ...summary.containerFailureDetails]) {
        if (printedFailures.has(failure)) continue
        if (!failureHeaderPrinted) {
          err.write('\n✖ failing tests:\n\n')
          failureHeaderPrinted = true
        }
        printFailure(failure, err)
        printedFailures.add(failure)
      }
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

      applyEvent(state, event)
      if (type === 'test:fail') printNewFailures(summarize(state))

      if (verbose && (type === 'test:pass' || type === 'test:fail')) {
        const isSuite = data.details?.type === 'suite'
        const isSubtestsParent = data.details?.error?.failureType === 'subtestsFailed'
        const isWrapper = isFileWrapper(event)

        if (!isSuite && !isSubtestsParent && !isWrapper) {
          const name = data.name ?? '<unnamed>'
          const ms = Number(data.details?.duration_ms ?? data.durationMs)
          const duration = Number.isFinite(ms) && ms >= 0 ? ms : 0
          const isSkip = Boolean(data.skip)
          const isTodo = Boolean(data.todo)
          const isCancelled = Boolean(data.cancelled || data.details?.error?.failureType === 'testAborted')

          if (isCancelled) {
            out.write(`﹣ ${name} (${duration.toFixed(3)}ms) # CANCELLED\n`)
          } else if (isSkip) {
            out.write(`﹣ ${name} (${duration.toFixed(3)}ms) # SKIP\n`)
          } else if (isTodo) {
            out.write(`﹣ ${name} (${duration.toFixed(3)}ms) # TODO\n`)
          } else if (type === 'test:fail') {
            out.write(`✖ ${name} (${duration.toFixed(3)}ms)\n`)
          } else {
            out.write(`✔ ${name} (${duration.toFixed(3)}ms)\n`)
          }
        }
      }
    }

    const summary = summarize(state)

    printNewFailures(summary)

    // Default mode prints compact summary to stderr
    const summaryText =
      `\n[test-summary] ${summary.files} file(s), ${summary.passed} passed, ${summary.failed} failed` +
      (summary.skipped > 0 ? `, ${summary.skipped} skipped` : '') +
      (summary.todo > 0 ? `, ${summary.todo} todo` : '') +
      (summary.cancelled > 0 ? `, ${summary.cancelled} cancelled` : '') +
      (summary.containerFailures > 0 ? `, ${summary.containerFailures} failed containers` : '') +
      ` (${(summary.wallMs / 1000).toFixed(2)}s wall, ${(summary.sumTestMs / 1000).toFixed(2)}s test time)\n`
    err.write(summaryText)
    for (const item of summary.exclusions) {
      err.write(`  ${item.status}: ${item.file}:${item.name} — ${item.reason}\n`)
    }

    options.onSummary?.(summary)
  }

  reporterFn.state = state
  return reporterFn
}
