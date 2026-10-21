import fs from 'node:fs'

const processRoot = '/private/tmp/vibe-fs-physical-soak-20261021-b'
const ptyRoot = '/private/tmp/vibe-fs-pty-soak-gen106-20261021-b'
const json = path => JSON.parse(fs.readFileSync(path, 'utf8'))
const processStatus = json(processRoot + '/status.json')
const processLaunch = json('/private/tmp/vibe-fs-physical-soak-20261021-a/launch.json')
Object.assign(processLaunch, {
  state: 'Running; fresh restart, launch receipt only, no four-hour acceptance yet',
  startedAt: processStatus.startedAt,
  sourceCommit: processStatus.source,
  package: '/private/tmp/vibe-fs-routing-public-pack/wanxiangli-0.11.0.tgz',
  packageSha256: '687cf81f702a2f229f7924442b9c454d9a96576e61a3ccd819323e0e716d3b93',
  preflightReceipt: processRoot + '/preflight-receipt.json',
  consumer: processRoot + '/consumer',
  command: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node ' + processRoot + '/consumer/coordinator.mjs > ' + processRoot + '/coordinator.log 2>&1',
  coordinatorPid: processStatus.coordinatorPid,
  execSession: 32732,
  initialOwned: Object.fromEntries(Object.entries(processStatus.lanes).map(([lane, value]) => [lane, { harness: value.pid, owned: value.ownedPids }])),
  expectedFourHourEnd: new Date(Date.parse(processStatus.startedAt) + 4 * 60 * 60 * 1000).toISOString(),
  status: processRoot + '/status.json',
  previousRun: { root: '/private/tmp/vibe-fs-physical-soak-20261021-a', state: 'Canceled by user; incomplete, zero completed phases; not resumed or added to this clock.' },
})
fs.writeFileSync(processRoot + '/launch.json', JSON.stringify(processLaunch, null, 2) + '\n')
const ptyStatus = json(ptyRoot + '/status.json')
const controller = json(ptyRoot + '/controller.json')
const ptyLaunch = json('/private/tmp/vibe-fs-pty-soak-gen100-20261021/launch.json')
Object.assign(ptyLaunch, {
  state: 'Running; fresh restart, launch receipt only, no 120-minute acceptance yet',
  generation: 106,
  manifestSha256: '331e32dd1401edcedccef5f93dc5d2a03676b96267b6e06c1e2ccf1705df4d17',
  artifactReceipt: ptyRoot + '/copied-artifact-receipt.json',
  cloneAndPreflightReceipt: ptyRoot + '/preflight-receipt.json',
  artifacts: '833 byte-equal copied files: full gen106 dist, actual bun-pty 0.4.10 including native libraries, and actual installed wanxiangli 0.11.0 runtime outputs/resources/package.json. No symlinks or fallback into main. Gen106 build predates tracked pause evidence; no claim of aad HEAD build freshness.',
  consumer: ptyRoot + '/consumer',
  controllerPid: controller.pid,
  harnessPid: controller.harnessPid,
  execSession: 77963,
  activeStartedAt: ptyStatus.activeStartedAt,
  expectedActiveEnd: new Date(Date.parse(ptyStatus.activeStartedAt) + 120 * 60 * 1000).toISOString(),
  command: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node ' + ptyRoot + '/consumer/controller.mjs > ' + ptyRoot + '/runner.log 2>&1',
  publicEntry: 'Copied gen106 Process Surface ptyRecoveryOpen/ptyRecoveryDrain, real public PTY scope and bun-pty',
  onePhysicalPty: { custodian: ptyStatus.custodian, scopeOwner: ptyStatus.scope.owner, target: ptyStatus.scope.target, anchor: ptyStatus.ownedPids.find(pid => pid !== ptyStatus.custodian && pid !== ptyStatus.scope.owner && pid !== ptyStatus.scope.target), scopeIncarnation: ptyStatus.scope.incarnation, extraDescendant: 'Only at final EOF/cancellation phase; recorded before target exits' },
  status: ptyRoot + '/status.json',
  controllerStatus: ptyRoot + '/controller-exit.json',
  previousRun: { root: '/private/tmp/vibe-fs-pty-soak-gen100-20261021', state: 'Canceled by user; incomplete, zero completed phases; not resumed or added to this clock.' },
  firstSetupAttempt: { root: '/private/tmp/vibe-fs-pty-soak-gen106-20261021', state: 'Failed before owner creation: physical cwd absent; 0 requests, ownedPids empty. Original logs retained. This second directory was prepared before launch.' },
})
fs.writeFileSync(ptyRoot + '/launch.json', JSON.stringify(ptyLaunch, null, 2) + '\n')
console.log(JSON.stringify({ process: { root: processRoot, pid: processLaunch.coordinatorPid, expectedEnd: processLaunch.expectedFourHourEnd }, pty: { root: ptyRoot, controller: controller.pid, harness: controller.harnessPid, owned: ptyStatus.ownedPids, activeStarted: ptyStatus.activeStartedAt, expectedEnd: ptyLaunch.expectedActiveEnd } }))
