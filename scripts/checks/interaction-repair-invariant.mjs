#!/usr/bin/env node

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../..', import.meta.url))
const SRC = join(ROOT, 'src/Wanxiangshu')
const REPAIR = 'Interaction/Repair/InteractionRepair.fs'
const DISPATCH = 'Interaction/Dispatch/OpenCode/SessionNudge.fs'
const violations = []

const files = (dir) =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    return statSync(path).isDirectory() ? files(path) : path.endsWith('.fs') ? [path] : []
  })

const source = new Map(files(SRC).map((path) => [relative(SRC, path), readFileSync(path, 'utf8')]))
const repair = source.get(REPAIR) ?? ''
const dispatch = source.get(DISPATCH) ?? ''

const exhaustionOwners = [...source.entries()]
  .filter(([, text]) => text.includes('INTERACTION_REPAIR_EXHAUSTED'))
  .map(([path]) => path)

if (exhaustionOwners.length !== 1 || exhaustionOwners[0] !== REPAIR) {
  violations.push(`INTERACTION_REPAIR_EXHAUSTED must be owned only by ${REPAIR}; found ${exhaustionOwners.join(', ')}`)
}

for (const legacy of [
  'BudgetExhausted',
  'RepairFamilyAlreadyClaimed',
  'repairFamilyAlreadyClaimed',
  'RepairFamilyAdmissionSpent',
  'repairFamilyAdmissionSpent',
]) {
  const owners = [...source.entries()].filter(([, text]) => text.includes(legacy)).map(([path]) => path)
  if (owners.length > 0) violations.push(`legacy claim=outcome vocabulary ${legacy}: ${owners.join(', ')}`)
}

if (!dispatch.includes('AlreadyAdmitted')) {
  violations.push('dispatch must expose duplicate repair admission as AlreadyAdmitted')
}
if (!repair.includes('CompletedTurnClassifier.decideRepairDefect')) {
  violations.push('repair workflow must route defect handling through the owner decision algebra')
}

if (violations.length > 0) {
  console.error('interaction-repair-invariant: VIOLATIONS')
  for (const violation of violations) console.error(`  ${violation}`)
  process.exit(1)
}

console.log('interaction-repair-invariant: OK — admission, in-flight, and exhaustion remain separate')
