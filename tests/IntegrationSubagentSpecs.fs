module Wanxiangshu.Tests.IntegrationSubagentSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationSubagentMockClient

open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Hosts.Mux.AiSettings
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Dyn


let investigatorToolSpec () =
    promise {
        let pObjRef = ref null

        let createCalls, promptCalls, mockClient =
            makeMockClient pObjRef "investigator-parent" "Found src/Opencode/Tools.fs"

        let! workspaceDir = mkdtempAsync "investigator-tool-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mockClient |}
            )

        pObjRef.Value <- p

        let investigator = get (get p "tool") "investigator"

        let! result =
            (get investigator "execute")
            $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ],
               createObj
                   [ "directory", box workspaceDir
                     "sessionID", box "investigator-parent"
                     "abort", box null ])
            |> unbox<JS.Promise<string>>

        check "investigator tool returns subagent output" (result.Contains("src/Opencode/Tools.fs"))

        check
            "investigator tool creates child session under parent"
            (str (get createCalls.[0] "body") "parentID" = "investigator-parent")

        check
            "investigator tool prompts child investigator agent"
            (str (get promptCalls.[0] "body") "agent" = "investigator")

        do! rmAsync workspaceDir
    }

let coderToolSpec () =
    promise {
        let pObjRef = ref null

        let createCalls, promptCalls, mockClient =
            makeMockClient pObjRef "coder-parent" "Coder finished"

        let! workspaceDir = mkdtempAsync "coder-tool-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mockClient |}
            )

        pObjRef.Value <- p

        let coder = get (get p "tool") "coder"

        let intents: obj array =
            [| sampleCoderIntentWithDoNotTouch "fix bug" "a.ts" [| "src/shared.fs"; "Do not rename public API" |]
               sampleCoderIntent "add feature" "b.ts" |]

        let! result =
            (get coder "execute")
            $ (createObj [ "intents", box intents ],
               createObj
                   [ "directory", box workspaceDir
                     "sessionID", box "coder-parent"
                     "abort", box null ])
            |> unbox<JS.Promise<string>>

        check "coder tool returns subagent output" (result.Contains("Coder finished"))

        let coderCreates =
            createCalls
            |> Seq.filter (fun call -> str (get call "body") "parentID" = "coder-parent")
            |> Seq.toArray

        check "coder tool creates one child per intent" (coderCreates.Length = 2)
        check "coder tool prompts child coder agent" (str (get promptCalls.[0] "body") "agent" = "coder")

        let firstPrompt =
            str (unbox<obj[]> (get (get promptCalls.[0] "body") "parts")).[0] "text"

        let secondPrompt =
            str (unbox<obj[]> (get (get promptCalls.[1] "body") "parts")).[0] "text"

        check
            "coder prompt includes first intent do_not_touch"
            (firstPrompt.Contains("do_not_touch:")
             && firstPrompt.Contains("src/shared.fs")
             && firstPrompt.Contains("Do not rename public API"))

        check "coder prompt omits do_not_touch section when absent" (not (secondPrompt.Contains("do_not_touch:")))
        do! rmAsync workspaceDir
    }

let investigatorToolLateClientInjectionSpec () =
    promise {
        let pObjRef = ref null

        let createCalls, promptCalls, mockClient =
            makeMockClient pObjRef "investigator-parent-late" "Late client injection worked"

        let! workspaceDir = mkdtempAsync "investigator-tool-late-client-"
        let ctx = createObj [ "directory", box workspaceDir ]
        let! p = plugin ctx
        pObjRef.Value <- p
        ctx?("client") <- mockClient
        let investigator = get (get p "tool") "investigator"

        let! result =
            (get investigator "execute")
            $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ],
               createObj
                   [ "directory", box workspaceDir
                     "sessionID", box "investigator-parent-late"
                     "abort", box null ])
            |> unbox<JS.Promise<string>>

        check
            "investigator tool sees client injected after plugin init"
            (result.Contains("Late client injection worked"))

        check
            "investigator tool late injection creates child session under parent"
            (str (get createCalls.[0] "body") "parentID" = "investigator-parent-late")

        check
            "investigator tool late injection prompts child investigator agent"
            (str (get promptCalls.[0] "body") "agent" = "investigator")

        do! rmAsync workspaceDir
    }

let investigatorToolWithHostConfiguredModelSpec () =
    promise {
        let pObjRef = ref null

        let configApi =
            createObj
                [ "get",
                  box (fun () ->
                      promise {
                          return
                              box
                                  {| data =
                                      box
                                          {| agent =
                                              createObj
                                                  [ "investigator", box (createObj [ "model", box "openai/gpt-4o" ]) ] |} |}
                      }) ]

        let createCalls, promptCalls, mockClient =
            makeMockClient pObjRef "investigator-parent-config" "Found src/Opencode/Tools.fs"

        setKey mockClient "config" configApi
        let! workspaceDir = mkdtempAsync "investigator-tool-config-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mockClient |}
            )

        pObjRef.Value <- p
        let investigator = get (get p "tool") "investigator"

        let! result =
            (get investigator "execute")
            $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ],
               createObj
                   [ "directory", box workspaceDir
                     "sessionID", box "investigator-parent-config"
                     "abort", box null ])
            |> unbox<JS.Promise<string>>

        check "investigator tool returns subagent output" (result.Contains("src/Opencode/Tools.fs"))
        let promptBody = get promptCalls.[0] "body"
        check "prompt body does not contain model" (isNullish (get promptBody "model"))
        do! rmAsync workspaceDir
    }

let muxCoderInvalidIntentsSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let tools = unbox<obj[]> (get reg "tools")
        let coder = tools |> Array.find (fun t -> str t "name" = "coder")
        let execute = get coder "execute"
        let invalidIntents = [| createObj [ "objective", box "x"; "background", box "y" ] |]

        let! result =
            (execute
             $ (createObj [ "workspaceId", box "mux-invalid-intents"; "cwd", box "/tmp" ],
                createObj [ "intents", box invalidIntents; "tdd", box "red" ]))
            |> unbox<JS.Promise<string>>

        check "mux coder invalid intents mentions parse" (result.Contains "parse")

        check
            "mux coder invalid intents mentions Invalid LLM or intents"
            (result.Contains "Invalid LLM" || result.Contains "intents")
    }
