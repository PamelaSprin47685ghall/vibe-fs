namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module DelegatedToolEstimateLedger =

    let tryState (port: DelegatedToolEstimatePort) sessionId = port.TryState sessionId

    let tryRemaining port sessionId =
        tryState port sessionId |> Option.map DelegatedToolEstimateProjection.remaining

    let private append (port: DelegatedToolEstimatePort) sessionId fact =
        task {
            try
                let! _ = port.Append sessionId fact
                return ()
            with _ ->
                return ()
        }

    let replace (port: DelegatedToolEstimatePort) sessionId expectedToolCalls : Task<unit> =
        append
            port
            sessionId
            (DelegationFactCases.DelegatedToolEstimateReplaced
                {| SessionId = sessionId
                   ExpectedToolCalls = expectedToolCalls |})

    let observe (port: DelegatedToolEstimatePort) sessionId toolCallId : Task<unit> =
        task {
            match tryRemaining port sessionId with
            | Some remaining when remaining > 0 ->
                do!
                    append
                        port
                        sessionId
                        (DelegationFactCases.DelegatedToolCallObserved
                            {| SessionId = sessionId
                               ToolCallId = toolCallId |})
            | _ -> ()
        }
