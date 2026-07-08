module Wanxiangshu.Tests.IntegrationDedupModelSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.Dyn


let dedupModelTextSpec () =
    let seen, msgs =
        Wanxiangshu.Mux.ReadDedup.deduplicateModelReadOutputsWithSeen
            [||]
            [| box
                   {| content =
                       [| box
                              {| ``type`` = "tool-result"
                                 toolName = "read"
                                 output =
                                  box
                                      {| ``type`` = "text"
                                         value = box "hello" |} |} |] |}
               box
                   {| content =
                       [| box
                              {| ``type`` = "tool-result"
                                 toolName = "read"
                                 output =
                                  box
                                      {| ``type`` = "text"
                                         value = box "hello" |} |} |] |} |]

    check "ModelMessage text: returns seen" (seen |> Array.contains "hello")
    let firstOut = get ((unbox<obj[]> (get msgs.[0] "content")).[0]) "output"
    let secondOut = get ((unbox<obj[]> (get msgs.[1] "content")).[0]) "output"
    check "ModelMessage text: first preserved" (str firstOut "value" = "hello")
    check "ModelMessage text: second replaced" (str secondOut "value" = noChangeEnvelope ())

let dedupModelJsonSpec () =
    let seen, msgs =
        Wanxiangshu.Mux.ReadDedup.deduplicateModelReadOutputsWithSeen
            [||]
            [| box
                   {| content =
                       [| box
                              {| ``type`` = "tool-result"
                                 toolName = "file_read"
                                 output =
                                  box
                                      {| ``type`` = "json"
                                         value = box {| content = "json content" |} |} |} |] |}
               box
                   {| content =
                       [| box
                              {| ``type`` = "tool-result"
                                 toolName = "file_read"
                                 output =
                                  box
                                      {| ``type`` = "json"
                                         value = box {| content = "json content" |} |} |} |] |} |]

    check "ModelMessage json: returns seen" (seen |> Array.contains "json content")
    let secondOut = get ((unbox<obj[]> (get msgs.[1] "content")).[0]) "output"
    check "ModelMessage json: second replaced" (str secondOut "value" = noChangeEnvelope ())

let dedupModelDifferentSpec () =
    let _, msgs =
        Wanxiangshu.Mux.ReadDedup.deduplicateModelReadOutputsWithSeen
            [||]
            [| box
                   {| content =
                       [| box
                              {| ``type`` = "tool-result"
                                 toolName = "read"
                                 output =
                                  box
                                      {| ``type`` = "text"
                                         value = box "first" |} |} |] |}
               box
                   {| content =
                       [| box
                              {| ``type`` = "tool-result"
                                 toolName = "read"
                                 output =
                                  box
                                      {| ``type`` = "text"
                                         value = box "second" |} |} |] |} |]

    let firstOut = get ((unbox<obj[]> (get msgs.[0] "content")).[0]) "output"
    let secondOut = get ((unbox<obj[]> (get msgs.[1] "content")).[0]) "output"
    check "ModelMessage different: first unchanged" (str firstOut "value" = "first")
    check "ModelMessage different: second unchanged" (str secondOut "value" = "second")

let dedupModelNonReadSpec () =
    let _, msgs =
        Wanxiangshu.Mux.ReadDedup.deduplicateModelReadOutputsWithSeen
            [||]
            [| box
                   {| content =
                       [| box
                              {| ``type`` = "tool-result"
                                 toolName = "write"
                                 output =
                                  box
                                      {| ``type`` = "text"
                                         value = box "write result" |} |} |] |} |]

    let out = get ((unbox<obj[]> (get msgs.[0] "content")).[0]) "output"
    check "ModelMessage non-read: write preserved" (str out "value" = "write result")

let dedupModelEmptySpec () =
    let _, msgs =
        Wanxiangshu.Mux.ReadDedup.deduplicateModelReadOutputsWithSeen [||] [||]
    check "ModelMessage empty: empty array" (msgs.Length = 0)