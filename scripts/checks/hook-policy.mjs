#!/usr/bin/env node

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../..', import.meta.url))

export function check(context) {
  const root = context?.root ?? ROOT
  const read = (path) => context?.readText ? context.readText(path) : readFileSync(join(root, path), 'utf8')
  const policy = read('src/Wanxiangshu/OpenCode/Host/HookPolicy.fs')
  const hooks = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')
  const { failures, rowsCount } = scanHookPolicy(policy, hooks)
  return {
    issues: failures.map((f) => ({
      code: 'hook-policy-violation',
      message: f,
    })),
    rowsCount,
  }
}

export function runCli() {
  const result = check()
  if (result.issues.length > 0) {
    for (const issue of result.issues) console.error(`hook-policy: ${issue.message}`)
    return 1
  }
  console.log(`hook-policy: OK — ${result.rowsCount} live Hook keys have exact closed metadata and static registration`)
  return 0
}

export function scanHookPolicy(policy, hooks) {
  const rows = [...policy.matchAll(/\| HookKey\.([A-Z][A-Za-z]+) ->\s*\n\s*\{ HostKey = "([^"]+)"([\s\S]*?)(?=\n\s*\| HookKey\.|\n\s*let )/g)]
  const registrations = [...hooks.matchAll(/registeredHook\s+HookKey\.([A-Z][A-Za-z]+)/g)]
  const failures = []

  const duplicates = (values) => values.filter((value, index) => values.indexOf(value) !== index)
  const hostKeys = rows.map((row) => row[2])
  const registeredNames = registrations.map((registration) => registration[1])
  const rowNames = rows.map((row) => row[1])

  if (rows.length === 0) failures.push('no HookPolicy metadata rows found')
  for (const duplicate of new Set(duplicates(hostKeys))) failures.push(`duplicate Host key: ${duplicate}`)
  for (const duplicate of new Set(duplicates(registeredNames))) failures.push(`duplicate registration: ${duplicate}`)

  const missingRows = registeredNames.filter((name) => !rowNames.includes(name))
  const unregisteredRows = rowNames.filter((name) => !registeredNames.includes(name))
  for (const name of missingRows) failures.push(`registered Hook has no metadata row: ${name}`)
  for (const name of unregisteredRows) failures.push(`metadata row is not registered: ${name}`)

for (const [, name, , body] of rows) {
  if (!/Identity = IdentityPermission\.(?:NoIdentityAccess|ObserveIdentity)/.test(body)) {
    failures.push(`${name} has unsafe or missing identity permission`)
  }
  if (!/Admission = AdmissionPermission\.(?:NoAdmissionAccess|OwnedAdmissionGate)/.test(body)) {
    failures.push(`${name} has unsafe or missing admission permission`)
  }
  if (/Criticality = HookCriticality\.(?:Security|Workflow|Invariant)/.test(body)
      && /Failure = HookFailureDisposition\.BestEffortDiagnostic/.test(body)) {
    failures.push(`${name} downgrades critical failure to best effort`)
  }
}

if (/hooks\?[^\s]+\s*<-/.test(hooks)) failures.push('dynamic Hook property assignment is forbidden')
if (/\bpolicyAwareHook\b/.test(hooks)) failures.push('Hook bypasses metadata-driven registeredHook composition')
if (/List\.(?:map|fold|iter)[\s\S]{0,120}registeredHook/.test(hooks)) failures.push('dynamic registration from a list is forbidden')
if (/MutateIdentity|BypassAdmission/.test(policy)) failures.push('Hook authority can express identity mutation or admission bypass')

  return { failures, rowsCount: rows.length }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const code = runCli()
  if (code !== 0) process.exit(code)
}
