namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process

module PhysicalRecoverySurface =
    let settleChildRuns (handle: JournalHandle) (commonDir: string) : Task<unit> =
        let drainPtys work =
            HostForkRuntime.CloseUnboundPtyRun(PtyBackend.recover, commonDir, work)

        ChildWorkRecovery.settleOrphanedChildRuns drainPtys handle.Journal
