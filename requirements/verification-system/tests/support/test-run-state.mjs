// requirements/verification-system/tests/support/test-run-state.mjs
//
// 本模块是 TestsStream 归一化与运行状态的唯一属主（后续批次扩展）。
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
