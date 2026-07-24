namespace Wanxiangshu.Next.Process

open System
open Fable.Core
open Fable.Core.JsInterop

module RunnerPrimitives =

    let calculateDeadline (now: DateTimeOffset) (RuntimeSeconds estSecs: EstimatedRuntime) : Deadline =
        Deadline.ofBudget now (TimeSpan.FromSeconds(3.0 * estSecs))

    let killProcessGroup (child: obj) : unit =
        emitJsExpr
            child
            """
            try {
                if ($0 && $0.pid) {
                    process.kill(-$0.pid, 'SIGKILL');
                }
            } catch (_) {
                try { if ($0 && typeof $0.kill === 'function') $0.kill('SIGKILL'); } catch (_) {}
            }
        """
        |> ignore
