module VibeFs.Mux.KnowledgeGraphRuntimeMuxQuery

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.Messaging
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.RuntimeState
open VibeFs.Kernel.KnowledgeGraph.Maintenance
open VibeFs.Kernel.KnowledgeGraph.Prompts
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.MessagingCodec
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphStorage
open VibeFs.Shell.KnowledgeGraphWorkflow
open VibeFs.Shell.KnowledgeGraphMaintenanceRun
open VibeFs.Shell.KnowledgeGraphBookkeeperLaunch
open VibeFs.Shell.KnowledgeGraphRuntimeTestPorts
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.ToolContextCodec
open VibeFs.Shell.Dyn
open VibeFs.Mux.KnowledgeGraphRuntimeIO
open VibeFs.Mux.KnowledgeGraphRuntimeMux

type MuxKnowledgeGraphRuntime with

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        promise {
            if not (knowledgeGraphDirExists directory) then return None
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                return buildPreludeSection projection
        }

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, entity: string) : JS.Promise<string> =
        promise {
            if System.String.IsNullOrWhiteSpace directory then
                return "No knowledge graph directory provided."
            elif not (knowledgeGraphDirExists directory) then
                return "Knowledge graph directory not found."
            elif sessionID = "" then
                let! projection = readProjectionForRoot directory
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
        }
