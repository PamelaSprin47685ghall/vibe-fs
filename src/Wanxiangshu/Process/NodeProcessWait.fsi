namespace Wanxiangshu.Process

open System.Threading
open System.Threading.Tasks

module NodeProcessWait =
    type WaitOutcome = { ExitCode: int; TimedOut: bool }

    val KillAckGraceMs: int

    val waitForExit:
        child: NodeProcessHost.ChildProcess -> deadline: Deadline -> ct: CancellationToken -> Task<WaitOutcome>
