import { ownershipProbe } from '../../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js'

const [directory, action] = process.argv.slice(2)
let dispatched = 0
const owner = ownershipProbe(directory, 'cold-pty-owner', () => {
  dispatched += 1
})
if (action === 'reserve') {
  const opened = await owner.fork('controlled-external-resource')
  console.log(JSON.stringify({ ...opened, dispatched }))
} else {
  let closeError
  try { await owner.closeAll(0) } catch (error) { closeError = error.message }
  console.log(JSON.stringify({ outstanding: owner.outstanding(), dispatched, closeError }))
}
