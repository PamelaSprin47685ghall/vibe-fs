namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for explicit restart disclosure.
/// Journal and snapshot capabilities remain private to the Host owner.
module ExplicitResumeSurface =

    let registerCommand (config: obj) : unit =
        ExplicitSessionResume.registerCommand config

    let run (command: string) (sessionId: string) (arguments: string) : Task<obj> =
        let input =
            createObj [ "command" ==> command; "sessionID" ==> sessionId; "arguments" ==> arguments ]

        let output = createObj [ "parts" ==> [||] ]
        let adopt (_parent: SessionId) (_record: HandleRecord) : Result<unit, string> = Ok()

        task {
            do! ExplicitSessionResume.before None None adopt input output
            return output
        }
