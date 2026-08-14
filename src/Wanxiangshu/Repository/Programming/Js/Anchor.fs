namespace Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// JS-006: ordered anchor declarations — string or RegExp, with an optional
/// 1-based occurrence selector; duplicate textual occurrence resolves in
/// declaration order. `^`/`$` mean absolute file start/end (not line anchors).
[<RequireQualifiedAccess>]
type AnchorSpec =
    | Exact of string
    | Regex of string

type AnchorDeclaration =
    {
        Spec: AnchorSpec
        /// 1-based occurrence; None = the anchor must be unique (JS-006).
        Occurrence: int option
    }

module AnchorRules =

    /// The Domain-owned refusal class: an empty anchor is refused without
    /// touching file content. The other four classes (not-unique-without-
    /// occurrence / not-found / invalid-regex / cross-file) need the sandbox
    /// matcher or the transaction layer and are enforced there (JS-006/019).
    let validateDeclaration (declaration: AnchorDeclaration) : Result<unit, JsFailure> =
        match declaration.Spec with
        | AnchorSpec.Exact text when System.String.IsNullOrEmpty text -> Error JsFailure.AnchorEmptyContent
        | AnchorSpec.Regex pattern when System.String.IsNullOrEmpty pattern -> Error JsFailure.AnchorEmptyContent
        | AnchorSpec.Exact _
        | AnchorSpec.Regex _ -> Ok()

    /// Occurrence selector must be positive when declared (JS-006); a
    /// non-positive selector is an invalid declaration.
    let validateOccurrence (declaration: AnchorDeclaration) : Result<unit, JsFailure> =
        match declaration.Occurrence with
        | Some n when n < 1 -> Error JsFailure.AnchorInvalidPattern
        | Some _
        | None -> Ok()
