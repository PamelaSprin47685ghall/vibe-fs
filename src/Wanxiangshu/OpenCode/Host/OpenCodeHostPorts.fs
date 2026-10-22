namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Host.Ports
open Wanxiangshu.Interaction.Dispatch

module OpenCodeHostPorts =

    type private OpenCodePromptPort(sessionHostPort: ISessionHostPort) =

        let deliveryVerdictOf (verdict: DetachedSendVerdict) : DeliveryVerdict =
            match verdict with
            | DetachedSendVerdict.OwnedSettled -> DeliveryVerdict.OwnedSettled
            | DetachedSendVerdict.Refused reason -> DeliveryVerdict.Refused reason
            | DetachedSendVerdict.OutcomeUnknown reason -> DeliveryVerdict.OutcomeUnknown reason

        let lateListenerOf (lateOpt: LateDeliveryListener option) : DetachedSendListener option =
            lateOpt
            |> Option.map (fun listener -> fun (verdict: DetachedSendVerdict) -> listener (deliveryVerdictOf verdict))

        let modelOf (routeOpt: ModelRoute option) : OpencodeModel option =
            routeOpt
            |> Option.map (fun r ->
                { providerID = r.Provider
                  modelID = r.Model
                  variant = r.Variant })

        let metadataOf (correlationOpt: PromptKey option) : obj option =
            correlationOpt
            |> Option.map (fun key -> PromptMetadataCodec.create key "hni-prompt-port" None)

        interface IPromptPort with
            member this.Send
                (session: SessionId, request: PromptRequest, late: LateDeliveryListener option)
                : Task<SendOutcome> =
                let opts: OpenCodePromptOptions =
                    { Model = modelOf request.Route
                      Agent = request.Role |> Option.map (fun (AgentRole s) -> s)
                      Directory = request.Workspace |> Option.map (fun (WorkspacePath p) -> p)
                      Metadata = metadataOf request.Correlation
                      Tools = request.ToolPolicy
                      DetachedListener = lateListenerOf late }

                sessionHostPort.SendPrompt(session, request.Text, opts)

    type private DiagnosticSinkPort() =
        interface IDiagnosticSink with
            member this.Emit(operation: string, fields: (string * string) list) : unit =
                Diagnostic.emit operation fields

    type private UnavailableSessionPort() =
        interface ISessionPort with
            member this.Create(_parent: SessionId option, _spec: SessionSpec) : HostResult<SessionId> =
                Task.FromResult(Error(HostError.Transport "no OpenCode session transport on host input"))

            member this.Parent(_session: SessionId) : HostResult<SessionId option> =
                Task.FromResult(Error(HostError.Transport "no OpenCode session transport on host input"))

            member this.Children(_parent: SessionId) : HostResult<SessionInfo list> =
                Task.FromResult(Error(HostError.Transport "no OpenCode session transport on host input"))

            member this.Abort(_session: SessionId) : HostResult<unit> =
                Task.FromResult(Error(HostError.Transport "no OpenCode session transport on host input"))

            member this.Interrupt(_session: SessionId) : HostResult<unit> =
                Task.FromResult(Error(HostError.Transport "no OpenCode session transport on host input"))

            member this.Close(_session: SessionId) : HostResult<unit> =
                Task.FromResult(Error(HostError.Transport "no OpenCode session transport on host input"))

            member this.FindByTitle(_title: string) : HostResult<SessionId list> =
                Task.FromResult(Error(HostError.Transport "no OpenCode session transport on host input"))

    type private UnavailableTranscriptPort() =
        interface ITranscriptPort with
            member this.Messages(_session: SessionId) : HostResult<TranscriptMessage list> =
                Task.FromResult(Error(HostError.Transport "no OpenCode transcript snapshot port on host input"))

    let private resolveTranscriptPort (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : ITranscriptPort =
        host.SnapshotOpt
        |> Option.orElseWith (fun () -> SessionSnapshotPort.create boot.Input)
        |> Option.map (fun snapshotPort -> TranscriptPort.HostTranscriptPort(snapshotPort) :> ITranscriptPort)
        |> Option.defaultWith (fun () -> UnavailableTranscriptPort() :> ITranscriptPort)

    let private resolveSessionPort (boot: PluginBoot.Boot) : ISessionPort =
        SessionSnapshotPort.createSessionPort boot.Input
        |> Option.defaultWith (fun () -> UnavailableSessionPort() :> ISessionPort)

    let private tryStringField (source: obj) (name: string) : string option =
        if isNull source || isNull source?(name) then
            None
        else
            Some(string source?(name))

    let private tryProjectObjectId (project: obj) : string option = tryStringField project "id"

    /// The OpenCode plugin input carries the project as a nested object
    /// (`input.project.id`); `projectID` / `projectId` are accepted as the flat
    /// spellings some hosts use. When the host names no project at all, the
    /// project root is the identity.
    let private projectIdOf (input: obj) (fallback: string) : string =
        let nested =
            if isNull input then
                None
            else
                tryProjectObjectId input?project

        let flatProjectId =
            if isNull input then
                None
            else
                tryStringField input "projectID"

        let flatProjectIdAlt =
            if isNull input then
                None
            else
                tryStringField input "projectId"

        nested
        |> Option.orElse flatProjectId
        |> Option.orElse flatProjectIdAlt
        |> Option.defaultValue fallback

    type private HostPathsPort(projectRootStr: string, stateRootStr: string, projectIdStr: string) =
        interface IHostPaths with
            member this.StateRoot: WorkspacePath = WorkspacePath stateRootStr

            member this.ProjectRoot: WorkspacePath = WorkspacePath projectRootStr

            member this.ProjectId: string = projectIdStr

    type private UnsupportedModelCatalog() =
        interface IModelCatalog with
            member this.Available() : HostResult<ModelRoute list> =
                Task.FromResult(Error(HostError.Unsupported HostCapability.ModelCatalog))

            member this.Apply(_session: SessionId, _route: ModelRoute) : HostResult<unit> =
                Task.FromResult(Error(HostError.Unsupported HostCapability.ModelCatalog))

    /// The neutral signal subscription is not wired on OpenCode yet, and the
    /// capability is not claimed. Returning a no-op handle would tell the
    /// product "subscribed" while dropping every signal, so the port refuses
    /// explicitly instead of faking a successful subscription.
    type private UnsupportedSignalSource() =
        interface ISignalSource with
            member this.Subscribe(_listener: HostSignal -> Task) : IDisposable =
                raise (
                    NotSupportedException(
                        "ISignalSource.Subscribe: neutral signal subscription is not wired on OpenCode yet (HostCapability.SignalSubscription is not claimed)"
                    )
                )

    type private UnsupportedToolRegistrar() =
        interface IToolRegistrar with
            member this.Register(_tools: ToolSpec list) : Result<unit, HostError> =
                Error(HostError.Unsupported HostCapability.ToolRegistration)

    type private UnsupportedAgentCatalog() =
        interface IAgentCatalog with
            member this.Roles() : HostResult<AgentRoleSpec list> =
                Task.FromResult(Error(HostError.Unsupported HostCapability.AgentCatalog))

    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) (liveConfig: LiveHostConfig) : Task<HostPorts> =
        task {
            let projectRootStr = boot.WorkspaceDirectory |> Option.defaultValue "."

            // The state root is the runtime's own state directory (`.Herdchestra`
            // under the primary worktree), not the project directory.
            let! stateRootStr = Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot projectRootStr

            let sessionPort = resolveSessionPort boot

            let transcriptPort = resolveTranscriptPort boot host

            let promptPort = OpenCodePromptPort(host.SessionPort) :> IPromptPort
            let configPort = liveConfig :> IHostConfigPort
            let diagnosticsPort = DiagnosticSinkPort() :> IDiagnosticSink

            let pathsPort =
                HostPathsPort(projectRootStr, stateRootStr, projectIdOf boot.Input projectRootStr) :> IHostPaths

            let modelsPort = UnsupportedModelCatalog() :> IModelCatalog
            let signalsPort = UnsupportedSignalSource() :> ISignalSource
            let toolsPort = UnsupportedToolRegistrar() :> IToolRegistrar
            let agentsPort = UnsupportedAgentCatalog() :> IAgentCatalog

            let capabilities: HostCapabilities =
                { HostName = "OpenCode"
                  Supports =
                    set
                        [ HostCapability.ChildSessions
                          HostCapability.SessionDiscovery
                          HostCapability.Interrupt ] }

            return
                { Sessions = sessionPort
                  Prompts = promptPort
                  Transcript = transcriptPort
                  Models = modelsPort
                  Signals = signalsPort
                  Tools = toolsPort
                  Paths = pathsPort
                  Config = configPort
                  Diagnostics = diagnosticsPort
                  Agents = agentsPort
                  Capabilities = capabilities }
        }
