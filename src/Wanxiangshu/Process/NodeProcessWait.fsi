namespace Wanxiangshu.Process

open System.Threading
open System.Threading.Tasks

module NodeProcessWait =
    type StopCause = Cancellation | Deadline
    type WaitOutcome = { StopRequested: StopCause option }

    val KillAckGraceMs: int

    val waitForExit:
        child: NodeProcessHost.ChildProcess -> deadline: Deadline -> ct: CancellationToken -> Task<WaitOutcome>
