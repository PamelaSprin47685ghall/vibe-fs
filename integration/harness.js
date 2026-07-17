// Shim: re-exports start() from the contract harness.
// F# integration tests import this as [<Import("start", "./harness.js")>].
export { start } from './opencode-plugin-contract-harness.js';
