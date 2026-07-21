module Wanxiangshu.Tests.IntegrationCapsSpecsSubagent

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup

open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Hosts.Mux.AiSettings
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Runtime.RuntimeScope

let private mockUserMsg (id: string) (sessionID: string) (prompt: string) : obj =
    let info =
        createObj [ "id", box id; "role", box "user"; "sessionID", box sessionID ]

    let parts = [| createObj [ "type", box "text"; "text", box prompt ] |]
    createObj [ "info", box info; "parts", box parts ]

let subagentCapsInjectionSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "subagent-caps-inj-"

        do!
            writeFileAsync
                (unbox<string> (pathModule?join (workspaceDir, "Target.fs")))
                "let targetContent = \"hello subagent\""

        let prompt = "Implement Target.fs"
        ExecutorTools.ompScope.RegisterTempFiles("test\u0000" + prompt, [ "Target.fs" ])
        markChildSession ExecutorTools.ompScope "test"

        try
            let reviewStore = createReviewStore ()
            let userEntry = mockUserMsg "user-1" "test" prompt
            let entries = [| userEntry |]

            let! out =
                transformEntriesAsyncWithAgent
                    reviewStore
                    workspaceDir
                    "test"
                    (box entries)
                    "coder"
                    (fun () -> Promise.lift ())
                    (box null)

            let mutable foundTargetRead = false

            for entry in out do
                let parts = Dyn.get entry "parts"

                if Dyn.isArray parts then
                    for part in unbox<obj array> parts do
                        if Dyn.str part "tool" = "read" then
                            let state = Dyn.get part "state"
                            let outText = Dyn.str state "output"

                            if outText.Contains("hello subagent") then
                                foundTargetRead <- true

            check "subagent caps injection injects read tool for Target.fs" foundTargetRead
        finally
            unmarkChildSession ExecutorTools.ompScope "test"
            rmAsync workspaceDir |> ignore
    }

let crossSessionIsolationSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "cross-session-iso-"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "TargetA.fs"))) "let a = 1"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "TargetB.fs"))) "let b = 2"

        ExecutorTools.ompScope.RegisterTempFiles("session-A\u0000Fix it", [ "TargetA.fs" ])
        ExecutorTools.ompScope.RegisterTempFiles("session-B\u0000Fix it", [ "TargetB.fs" ])

        let reviewStore = createReviewStore ()
        let prompt = "---\nobjective: Fix it\n---\n"
        let userEntry = mockUserMsg "user-1" "session-B" prompt
        let entries = [| userEntry |]

        markChildSession ExecutorTools.ompScope "session-B"

        try
            let! out =
                transformEntriesAsyncWithAgent
                    reviewStore
                    workspaceDir
                    "session-B"
                    (box entries)
                    "coder"
                    (fun () -> Promise.lift ())
                    (box null)

            let mutable foundA = false
            let mutable foundB = false

            for entry in out do
                let parts = Dyn.get entry "parts"

                if Dyn.isArray parts then
                    for part in unbox<obj array> parts do
                        if Dyn.str part "tool" = "read" then
                            let state = Dyn.get part "state"
                            let outText = Dyn.str state "output"

                            if outText.Contains("let a = 1") then
                                foundA <- true

                            if outText.Contains("let b = 2") then
                                foundB <- true

            check "session-B transform does NOT inject Session A files" (not foundA)
            check "session-B transform DOES inject Session B files" foundB
        finally
            unmarkChildSession ExecutorTools.ompScope "session-B"
            rmAsync workspaceDir |> ignore
    }

let opencodeSubsessionParentIDSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-sub-parent-"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "ParentFile.fs"))) "let parentVal = 42"

        let parentSessionID = "parent-session-A"
        let childSessionID = "child-session-B"
        let objective = "Code parent file"

        let scope = RuntimeScope()
        scope.RegisterTempFiles(parentSessionID + "\u0000" + objective, [ "ParentFile.fs" ])

        let registry = ChildAgentRegistry.Create()
        registry.RegisterChildAgent(childSessionID, "coder", Some parentSessionID)

        let prompt = "---\nobjective: Code parent file\n---\nImplement it"
        let userMsg = mockUserMsg "msg-1" childSessionID prompt
        let output = createObj [ "messages", box [| userMsg |] ]
        let input = createObj [ "agent", box "coder"; "sessionID", box childSessionID ]

        let reviewStore = createReviewStore ()

        do!
            Wanxiangshu.Hosts.Opencode.MessageTransformHook.messagesTransform
                registry
                workspaceDir
                scope
                reviewStore
                (box null)
                input
                output

        let msgs = unbox<obj[]> (get output "messages")
        let mutable foundParentRead = false

        for msg in msgs do
            let parts = Dyn.get msg "parts"

            if Dyn.isArray parts then
                for part in unbox<obj array> parts do
                    if Dyn.str part "tool" = "read" then
                        let state = Dyn.get part "state"
                        let outText = Dyn.str state "output"

                        if outText.Contains("let parentVal = 42") then
                            foundParentRead <- true

        check
            "Opencode messagesTransform correctly uses parent session ID via ResolveSubsessionParentID for temp files injection"
            foundParentRead

        do! rmAsync workspaceDir
    }

let subagentFallbackRawTextSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "subagent-fallback-"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "RawFile.fs"))) "let raw = 100"

        let key = "Do raw task"
        ExecutorTools.ompScope.RegisterTempFiles("test-session\u0000" + key, [ "RawFile.fs" ])

        let reviewStore = createReviewStore ()
        let userEntry = mockUserMsg "user-1" "test-session" "Do raw task"
        let entries = [| userEntry |]

        markChildSession ExecutorTools.ompScope "test-session"

        try
            let! out =
                transformEntriesAsyncWithAgent
                    reviewStore
                    workspaceDir
                    "test-session"
                    (box entries)
                    "coder"
                    (fun () -> Promise.lift ())
                    (box null)

            let mutable foundRawRead = false

            for entry in out do
                let parts = Dyn.get entry "parts"

                if Dyn.isArray parts then
                    for part in unbox<obj array> parts do
                        if Dyn.str part "tool" = "read" then
                            let state = Dyn.get part "state"
                            let outText = Dyn.str state "output"

                            if outText.Contains("let raw = 100") then
                                foundRawRead <- true

            check
                "subagent caps injection falls back to raw text key when objective front-matter is missing"
                foundRawRead
        finally
            unmarkChildSession ExecutorTools.ompScope "test-session"
            rmAsync workspaceDir |> ignore
    }
