module Wanxiangshu.Tests.IntegrationCapsSpecsSubagentMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup

open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Hosts.Mux.AiSettings
open Wanxiangshu.Hosts.Mux.BacklogSession
open Wanxiangshu.Hosts.Mux.MessageTransform
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MuxWorkspaceCodec

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Runtime.RuntimeScope

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
            Wanxiangshu.Runtime.MuxWorkspaceCodec.tryGetParentWorkspaceId mockDeps childSessionID

        check "parent workspace is parent-ws-A" (parentOpt = Some parentSessionID)

        let isChild =
            Wanxiangshu.Runtime.MuxWorkspaceCodec.isChildWorkspace mockDeps childSessionID

        check "is child workspace" isChild

        let prompt = "---\nobjective: Code mux parent\n---\nImplement it"
        let userMsg = mockMuxUserMsg "msg-1" prompt
        let output = createObj [ "messages", box [| userMsg |] ]

        let input =
            createObj
                [ "agent", box "build"
                  "sessionID", box childSessionID
                  "workspacePath", box workspaceDir ]

        let backlogSession = Wanxiangshu.Hosts.Mux.BacklogSession.BacklogSession(scope)
        let reviewStore = createReviewStore ()

        do!
            Wanxiangshu.Hosts.Mux.MessageTransform.messagesTransform
                mockDeps
                scope
                backlogSession
                reviewStore
                input
                output

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
