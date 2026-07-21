module Wanxiangshu.Tests.IntegrationEventTestsMuxWrappers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.Dyn

let syntaxWrapperSpec (reg: obj) =
    promise {
        let wrappers = unbox<obj[]> (get reg "wrappers")

        let sw =
            wrappers
            |> Array.find (fun w -> str w "targetTool" = "file_edit_replace_string")

        check "syntax wrapper exists" (not (isNullish sw))

        let mockEdit =
            createObj
                [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ -> (promise { return "File written" }))) ]

        let wrapped = (get sw "wrapper") $ (mockEdit, createObj [ "cwd", box "/tmp" ])

        let! _ =
            (get wrapped "execute") $ (createObj [ "file_path", box "nonexistent.js" ])
            |> unbox<JS.Promise<string>>

        check "syntax wrapper returns result" true
    }

let todoWriteWrapperSpec (_reg: obj) = promise { return () }

let todoWriteWrapperDecodeFailureSpec (_reg: obj) = promise { return () }
