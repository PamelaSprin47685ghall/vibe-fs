module Wanxiangshu.Tests.Wanxiangzhen.SessionIoTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.SessionIo
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list =
    [ ("getSession with session prop returns Ok",
       fun () ->
           let client = createObj [ "session", box (createObj []) ]

           match getSession client with
           | Ok _ -> checkBare true
           | Error _ -> checkBare false)

      ("getSession with null session returns Error",
       fun () ->
           let client = createObj [ "session", box null ]

           match getSession client with
           | Ok _ -> checkBare false
           | Error msg -> checkBare (msg.Contains "missing"))

      ("getSession without session prop returns Error",
       fun () ->
           let client = createObj []

           match getSession client with
           | Ok _ -> checkBare false
           | Error msg -> checkBare (msg.Contains "missing"))

      ("clientId returns sessionID value",
       fun () ->
           let input = createObj [ "sessionID", box "sid-1" ]
           equal "sid-1" (clientId input))

      ("clientId returns empty for missing sessionID",
       fun () ->
           let input = createObj []
           equal "" (clientId input))

      ("clientId returns empty for empty sessionID",
       fun () ->
           let input = createObj [ "sessionID", box "" ]
           equal "" (clientId input))

      ("promptFailureDiagnostic is structured with event reason sessionId detail",
       fun () ->
           let d = promptFailureDiagnostic "session_api_missing" "sid-9" "wanxiangzhen_session_api_missing:x"
           equal "wanxiangzhen_prompt_session_failed" (str d "event")
           equal "session_api_missing" (str d "reason")
           equal "sid-9" (str d "sessionId")
           checkBare ((str d "detail").Contains "wanxiangzhen_session_api_missing"))

      ("buildPromptArg shapes path.id and text part",
       fun () ->
           let arg = buildPromptArg "sid-1" "hello"
           let p = get arg "path"
           let b = get arg "body"
           equal "sid-1" (string (get p "id"))
           let parts = get b "parts" |> unbox<obj array>
           equal 1 (int parts.Length)
           equal "text" (str parts.[0] "type")
           equal "hello" (str parts.[0] "text")) ]

let entriesAsync () : (string * (unit -> JS.Promise<unit>)) list =
    [ ("promptSession rejects with ArgumentNullException if client is null",
       fun () ->
           promise {
               let mutable caught = false

               try
                   do! promptSession null "sid-1" "hello"
               with ex ->
                   caught <- true
                   checkBare (ex.Message.Contains "client")
                   checkBare (ex.Message.Contains "wanxiangzhen_prompt_parameter_missing")

               checkBare caught
           })

      ("promptSession rejects with ArgumentException if sessionId is empty",
       fun () ->
           promise {
               let mutable caught = false
               let client = createObj []

               try
                   do! promptSession client "" "hello"
               with ex ->
                   caught <- true
                   checkBare (ex.Message.Contains "sessionId")
                   checkBare (ex.Message.Contains "wanxiangzhen_prompt_parameter_missing")

               checkBare caught
           })

      ("promptSession rejects with ArgumentException if text is empty",
       fun () ->
           promise {
               let mutable caught = false
               let client = createObj []

               try
                   do! promptSession client "sid-1" ""
               with ex ->
                   caught <- true
                   checkBare (ex.Message.Contains "text")
                   checkBare (ex.Message.Contains "wanxiangzhen_prompt_parameter_missing")

               checkBare caught
           })

      ("promptSession rejects with SessionApiMissing if session prop missing",
       fun () ->
           promise {
               let mutable caught = false
               let client = createObj []

               try
                   do! promptSession client "sid-1" "hello"
               with ex ->
                   caught <- true
                   checkBare (ex.Message.Contains "wanxiangzhen_session_api_missing")

               checkBare caught
           })

      ("promptSession invokes session.prompt on happy path",
       fun () ->
           promise {
               let mutable invokedWith = None

               let fakePromptFn =
                   System.Func<obj, JS.Promise<obj>>(fun arg ->
                       invokedWith <- Some arg
                       Promise.lift (createObj []))

               let session = createObj [ "prompt", box fakePromptFn ]
               let client = createObj [ "session", box session ]

               do! promptSession client "sid-1" "hello"

               match invokedWith with
               | None -> checkBare false
               | Some arg ->
                   let p = get arg "path"
                   let b = get arg "body"
                   let id = get p "id"
                   let parts = get b "parts" |> unbox<obj array>
                   equal "sid-1" (string id)
                   equal 1 (int parts.Length)
                   let part = parts.[0]
                   equal "text" (str part "type")
                   equal "hello" (str part "text")
           })

      ("promptSession normalizes synchronous non-promise return",
       fun () ->
           promise {
               let mutable invokedWith = None

               let fakePromptFn =
                   System.Func<obj, obj>(fun arg ->
                       invokedWith <- Some arg
                       box 42)

               let session = createObj [ "prompt", box fakePromptFn ]
               let client = createObj [ "session", box session ]

               do! promptSession client "sid-1" "hello"

               match invokedWith with
               | None -> checkBare false
               | Some _ -> checkBare true
           })

      ("promptSession normalizes undefined return",
       fun () ->
           promise {
               let fakePromptFn = System.Func<obj, obj>(fun _ -> null)
               let session = createObj [ "prompt", box fakePromptFn ]
               let client = createObj [ "session", box session ]

               do! promptSession client "sid-1" "hello"
               checkBare true
           })

      ("promptSession rejects when session.prompt is not a function",
       fun () ->
           promise {
               let mutable caught = false
               let session = createObj [ "prompt", box "not-a-function" ]
               let client = createObj [ "session", box session ]

               try
                   do! promptSession client "sid-1" "hello"
               with ex ->
                   caught <- true
                   checkBare (ex.Message.Contains "wanxiangzhen_session_api_missing")

               checkBare caught
           })

      ("promptSession logs structured failure when session.prompt missing",
       fun () ->
           promise {
               let mutable logged: obj option = None
               emitJsStatement (fun msg -> logged <- Some msg) "const oldErr = console.error; console.error = $0;"

               try
                   let mutable caught = false
                   let client = createObj []

                   try
                       do! promptSession client "sid-struct" "hello"
                   with _ ->
                       caught <- true

                   checkBare caught

                   match logged with
                   | None -> checkBare false
                   | Some diag ->
                       equal "wanxiangzhen_prompt_session_failed" (str diag "event")
                       equal "session_api_missing" (str diag "reason")
                       equal "sid-struct" (str diag "sessionId")
                       checkBare ((str diag "detail").Contains "wanxiangzhen_session_api_missing")
               finally
                   emitJsStatement () "console.error = oldErr;"
           }) ]
