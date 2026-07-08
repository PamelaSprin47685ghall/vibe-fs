module Wanxiangshu.Tests.IntegrationCapsSpecsSubagent

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup

open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Kernel.Message
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Mux.AiSettings
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Omp
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Shell.RuntimeScope

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
            let! out = transformEntriesAsyncWithAgent reviewStore workspaceDir "test" (box entries) "coder"
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
            let! out = transformEntriesAsyncWithAgent reviewStore workspaceDir "session-B" (box entries) "coder"
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

let ompChildSessionObjectiveReRegisterSpec () =
    promise {
        let scope = RuntimeScope()

        let mockSessionManagerType =
            createObj
                [ "create",
                  box (fun (cwd: string) ->
                      createObj
                          [ "sessionId", box (Some "child-sess-1")
                            "getSessionId", box (Some(fun () -> box "child-sess-1"))
                            "prompt", box (fun (p: string) -> Promise.lift ())
                            "waitForIdle", box (fun () -> Promise.lift ()) ]) ]

        let mockCodingAgent = createObj [ "SessionManager", box mockSessionManagerType ]

        scope.Add("omp.coding_agent_module", box mockCodingAgent)

        let parentSessionId = "parent-sess-1"
        let objective = "Build target component"
        scope.RegisterTempFiles(parentSessionId + "\u0000" + objective, [ "OmpTarget.fs" ])

        let mockInnerPi =
            createObj
                [ "createAgentSession",
                  box (fun (body: obj) ->
                      let wrapper =
                          createObj
                              [ "session",
                                box (
                                    createObj
                                        [ "sessionManager",
                                          box (
                                              createObj
                                                  [ "sessionId", box (Some "child-sess-1")
                                                    "getSessionId", box (Some(fun () -> box "child-sess-1"))
                                                    "getEntries", box (Some(fun () -> [||])) ]
                                          )
                                          "prompt", box (fun (p: string) -> Promise.lift ())
                                          "waitForIdle", box (fun () -> Promise.lift ()) ]
                                )
                                "dispose", box (Some(fun () -> ())) ]

                      Promise.lift wrapper) ]

        let mockPi =
            createObj
                [ "registerTool", box (fun _ -> ())
                  "getActiveTools", box (Some(fun () -> box null))
                  "setActiveTools", box (Some(fun _ -> Promise.lift ()))
                  "sendMessage", box (Some(fun _ -> Promise.lift ()))
                  "pi", box (Some mockInnerPi) ]

        let ctx =
            createObj
                [ "sessionManager",
                  box (
                      Some(
                          createObj
                              [ "sessionId", box (Some parentSessionId)
                                "getSessionId", box (Some(fun () -> box parentSessionId)) ]
                      )
                  )
                  "sessionId", box parentSessionId
                  "cwd", box "/mock/dir" ]

        let prompt =
            "---\nobjective: Build target component\n---\nWrite code in OmpTarget.fs"

        let fallbackRuntime = Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()

        let! result = runSubagent scope mockPi ctx [||] prompt None fallbackRuntime None

        let copiedFiles = scope.TryGetTempFiles("child-sess-1\u0000" + objective)

        check
            "OMP ChildSession automatically replicates temp file registrations to child session ID"
            (copiedFiles = Some [ "OmpTarget.fs" ])
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

        let backlogSession =
            Wanxiangshu.Opencode.BacklogSession.BacklogSession(Wanxiangshu.Kernel.HostTools.Host.Mimocode, scope)

        let reviewStore = createReviewStore ()

        do!
            Wanxiangshu.Opencode.MessageTransform.messagesTransform
                registry
                workspaceDir
                scope
                backlogSession
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
            let! out = transformEntriesAsyncWithAgent reviewStore workspaceDir "test-session" (box entries) "coder"
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

let private mockMuxUserMsg (id: string) (prompt: string) : obj =
    let parts =
        [| createObj [ "type", box "text"; "text", box prompt; "state", box "done" ] |]

    createObj [ "id", box id; "role", box "user"; "parts", box parts ]

let muxSubsessionParentIDSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-sub-parent-"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "MuxParent.fs"))) "let muxParentVal = 999"

        let parentSessionID = "parent-ws-A"
        let childSessionID = "child-ws-B"
        let objective = "Code mux parent"

        let scope = RuntimeScope()
        scope.RegisterTempFiles(parentSessionID + "\u0000" + objective, [ "MuxParent.fs" ])

        let mockWorkspaceEntry =
            createObj [ "workspace", box (createObj [ "parentWorkspaceId", box parentSessionID ]) ]

        let loadConfigFn = emitJsExpr () "() => null"

        let findWorkspaceEntryFn =
            emitJsExpr (childSessionID, mockWorkspaceEntry) "(config, wsId) => wsId === $0 ? $1 : null"

        let mockDeps =
            createObj
                [ "loadConfigOrDefault", box loadConfigFn
                  "findWorkspaceEntry", box findWorkspaceEntryFn ]

        let parentOpt =
            Wanxiangshu.Shell.MuxWorkspaceCodec.tryGetParentWorkspaceId mockDeps childSessionID

        check "parent workspace is parent-ws-A" (parentOpt = Some parentSessionID)

        let isChild =
            Wanxiangshu.Shell.MuxWorkspaceCodec.isChildWorkspace mockDeps childSessionID

        check "is child workspace" isChild

        let prompt = "---\nobjective: Code mux parent\n---\nImplement it"
        let userMsg = mockMuxUserMsg "msg-1" prompt
        let output = createObj [ "messages", box [| userMsg |] ]

        let input =
            createObj
                [ "agent", box "build"
                  "sessionID", box childSessionID
                  "workspacePath", box workspaceDir ]

        let backlogSession = Wanxiangshu.Mux.BacklogSession.BacklogSession(scope)
        let reviewStore = createReviewStore ()
        do! Wanxiangshu.Mux.MessageTransform.messagesTransform mockDeps scope backlogSession reviewStore input output

        let msgs = unbox<obj[]> (get output "messages")
        let mutable foundMuxParentRead = false

        for msg in msgs do
            let parts = Dyn.get msg "parts"

            if Dyn.isArray parts then
                for part in unbox<obj array> parts do
                    if Dyn.str part "toolName" = "file_read" then
                        let outputObj = Dyn.get part "output"
                        let content = Dyn.str outputObj "content"

                        if content.Contains("let muxParentVal = 999") then
                            foundMuxParentRead <- true

        check
            "Mux messagesTransform correctly uses parent workspace ID via tryGetParentWorkspaceId for temp files injection"
            foundMuxParentRead

        do! rmAsync workspaceDir
    }
