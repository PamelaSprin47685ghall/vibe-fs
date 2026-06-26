module Wanxiangshu.Mux.KnowledgeGraphRuntimeMuxQuery

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Kernel.KnowledgeGraph.Maintenance
open Wanxiangshu.Kernel.KnowledgeGraph.Prompts
open Wanxiangshu.Mux.Delegate
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.MessagingCodec
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.KnowledgeGraphStorage
open Wanxiangshu.Shell.KnowledgeGraphWorkflow
open Wanxiangshu.Shell.KnowledgeGraphMaintenanceRun
open Wanxiangshu.Shell.KnowledgeGraphBookkeeperLaunch
open Wanxiangshu.Shell.KnowledgeGraphRuntimeTestPorts
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.ToolContextCodec
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Mux.KnowledgeGraphRuntimeIO
open Wanxiangshu.Mux.KnowledgeGraphRuntimeMux

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
