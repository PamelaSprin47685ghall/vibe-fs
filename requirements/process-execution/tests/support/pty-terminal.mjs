// The physical terminal belongs to wanxiangli/pty (real-PTY proofs live in its own tests,
// which run bun-pty under Bun). Host tests here exercise only the routing around it, so they
// open a genuine library Terminal over a scripted driver handle with bun-pty's shape.
import { Terminal } from 'wanxiangli/pty'

export function driverHandle(pid = 4242) {
  const handle = {
    pid,
    writes: [],
    resizes: [],
    dataCb: null,
    exitCb: null,
    onData: (cb) => { handle.dataCb = cb },
    onExit: (cb) => { handle.exitCb = cb },
    write: (text) => handle.writes.push(text),
    resize: (width, height) => handle.resizes.push([width, height]),
  }
  return handle
}

export function libraryTerminal(handle = driverHandle()) {
  const opened = Terminal.open({ driver: () => handle, command: 'scripted', cwd: process.cwd(), cols: 80, rows: 24 })
  if (opened.kind !== 'Opened') throw new Error(JSON.stringify(opened))
  return opened.terminal
}

export const settleTerminal = () => new Promise((resolve) => setTimeout(resolve, 0))
