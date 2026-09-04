namespace Wanxiangshu.Mission.Manager

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// GLORY-029: Manager idle → business-condition-aware encouragement (§7.4.6).
module ManagerIdle =

    /// GLORY-029: process-local dedupe uses the same exact terminal occasion
    /// identity as the durable claim. Life/condition classify the encouragement;
    /// ProviderRunIdentity prevents one physical terminal from sending twice.
    val occasionKey:
        sessionId: SessionId ->
        lifeId: ManagerLifeId ->
        conditionKey: string ->
        terminalProviderRun: ProviderRunIdentity ->
            string

    val encourageLabor:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        nudgeSent: HashSet<string> ->
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
        life: LifeProjection ->
            Task
