namespace Wanxiangshu.OpenCode

open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Ablation
open Wanxiangshu.Sphinx

module SphinxCommand =
    type Request = { Question: string; ExpectTurns: int option }

    let parse (arguments: string) =
        let text = if isNull arguments then "" else arguments.Trim()
        if text = "" then Error "Usage: /sphinx [--expect-turns N] question"
        elif text.StartsWith "--expect-turns" then
            let matched = Regex.Match(text, "^--expect-turns(?:=|\\s+)([0-9]+)\\s+([\\s\\S]+)$")
            if not matched.Success then Error "Usage: /sphinx --expect-turns N question (N must be a positive integer)"
            else
                match Int32.TryParse matched.Groups.[1].Value with
                | true, value when value > 0 ->
                    let question = matched.Groups.[2].Value.Trim()
                    if question = "" then Error "question required"
                    else TurnBudget.validate value |> Result.map (fun target -> { Question = question; ExpectTurns = Some target })
                | _ -> Error "expectTurns must be a positive integer"
        else Ok { Question = text; ExpectTurns = None }

    [<Emit("JSON.stringify($0, null, 2)")>]
    let private pretty (_value: obj) : string = jsNative

    let questionAndAnswer question (answer: obj) =
        let synthesis: string =
            if isNull answer?synthesis || isNull answer?synthesis?text then ""
            else string answer?synthesis?text
        let rendered =
            if String.IsNullOrWhiteSpace synthesis then pretty answer
            else synthesis + "\n\nSphinx evidence and limitations:\n" + pretty answer
        "## Question\n\n" + question + "\n\n## Answer\n\n" + rendered

    let private appendUserMaterial (client: obj) directory sessionId text =
        task {
            // The actual Host prompt option prevents a provider turn. The part
            // marker separately prevents this answer material becoming a new
            // Wanxiangshu business root or waking the managed parent loop.
            let parts = [| ExplicitResumeSuppression.markedTextPart text |]
            let body = createObj [ "noReply" ==> true; "parts" ==> parts ]
            let payload =
                createObj
                    ([ "path" ==> createObj [ "id" ==> sessionId; "sessionID" ==> sessionId ]
                       "body" ==> body
                       "sessionID" ==> sessionId
                       "noReply" ==> true
                       "parts" ==> parts
                       "throwOnError" ==> true ]
                     @ (directory |> Option.map (fun value -> [ "query" ==> createObj [ "directory" ==> value ]; "directory" ==> value ]) |> Option.defaultValue []))
            let! result = unbox<Task<obj>> (client?session?prompt(payload))
            if not (isNull result) && not (isNull result?error) then
                invalidOp ("Sphinx answer could not be inserted: " + pretty result?error)
        }

    [<Emit("Object.prototype.hasOwnProperty.call($0, 'handled')")>]
    let private supportsHandled (_output: obj) : bool = jsNative

    let private finish (output: obj) =
        output?parts <- [||]
        if supportsHandled output then
            output?handled <- true
        else
            // Released hosts without a handled hook flag otherwise unconditionally
            // prompt after this hook. Never silently fall through to a new turn.
            // This compatibility sentinel may be shown by those clients as a toast.
            let error = Exception "Sphinx completed: question and answer were inserted without starting a new turn."
            emitJsStatement error "$0.name = 'SphinxCommandHandled'"
            raise error

    let before (run: string -> string -> int option -> Task<obj>) (client: obj) (directory: string option) (input: obj) (output: obj) : Task<bool> =
        task {
            if isNull input || string input?command <> "sphinx" then return false
            else
                if not (AblationSettings.allowsToolSchema "sphinx") then invalidOp "Sphinx is disabled"
                let request =
                    match parse (unbox<string> input?arguments) with
                    | Ok value -> value
                    | Error reason -> invalidArg "arguments" reason
                let sessionId = string input?sessionID
                if String.IsNullOrWhiteSpace sessionId then invalidArg "sessionID" "current session is required"
                let! answer = run sessionId request.Question request.ExpectTurns
                do! appendUserMaterial client directory sessionId (questionAndAnswer request.Question answer)
                finish output
                return true
        }
