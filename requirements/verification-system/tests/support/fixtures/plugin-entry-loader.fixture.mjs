import { writeSync } from 'node:fs'

export async function resolve(specifier, context, nextResolve) {
  if (specifier === 'wanxiangshu') {
    writeSync(2, 'PLUGIN_ENTRY_RESOLVED\n')
    if (process.env.WXS_FIXTURE_REJECT_PLUGIN === '1') {
      throw new Error('controlled plugin entry rejection')
    }
  }
  return nextResolve(specifier, context)
}
