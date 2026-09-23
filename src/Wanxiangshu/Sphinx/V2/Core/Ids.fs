namespace Wanxiangshu.Sphinx.V2.Core

open System

module private IdValidation =

    /// Non-empty, no surrounding or interior whitespace, no control characters.
    /// Whitespace is rejected because these strings become map keys, wire ids and
    /// canonical-hash participants, where an invisible difference is a silent split.
    let isUsableId (value: string) =
        not (String.IsNullOrEmpty value)
        && not (String.IsNullOrWhiteSpace value)
        && not (value |> Seq.exists (fun character -> Char.IsWhiteSpace character))
        && not (value |> Seq.exists (fun character -> Char.IsControl character))

    let tryId (prefix: string) (value: string) : Result<string, string> =
        if isUsableId value then
            Ok value
        elif String.IsNullOrEmpty value then
            Error(sprintf "%s must not be blank" prefix)
        else
            Error(sprintf "%s must not contain whitespace or control characters" prefix)

[<Struct>]
type InquiryId = private InquiryId of string

[<Struct>]
type GoalId = private GoalId of string

[<Struct>]
type SnapshotId = private SnapshotId of string

[<Struct>]
type PlanId = private PlanId of string

[<Struct>]
type WorkId = private WorkId of string

[<Struct>]
type AttemptId = private AttemptId of string

[<Struct>]
type RoundId = private RoundId of string

[<Struct>]
type ObservationId = private ObservationId of string

[<Struct>]
type DecisionId = private DecisionId of string

[<Struct>]
type CertificateId = private CertificateId of string

[<Struct>]
type EventId = private EventId of string

[<Struct>]
type NodeId = private NodeId of string

[<Struct>]
type EdgeId = private EdgeId of string

[<Struct>]
type ArtifactRef = private ArtifactRef of string

/// Monotonic content revision of an immutable artifact. Revision 0 is the origin.
type Revision = private Revision of int64

/// Attempt counter inside one work identity. Starts at 1; a retry is a new attempt.
type Attempt = private Attempt of int64

/// Logical fence of one attempt: retries and late results carry different fences.
type Fence = private Fence of string

/// Builds an id wrapper, rejecting the two defects the wire layer cannot catch: a
/// blank id and an id whose invisible characters differ from a visually identical one.
module private IdConstruction =

    let make (prefix: string) (wrap: string -> 'id) (value: string) : Result<'id, string> =
        IdValidation.tryId prefix value |> Result.map wrap

    let raiseId (message: string) : 'id = raise (ArgumentException message)

module InquiryId =
    let tryCreate value =
        IdConstruction.make "InquiryId" InquiryId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (InquiryId inner) = inner

module GoalId =
    let tryCreate value =
        IdConstruction.make "GoalId" GoalId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (GoalId inner) = inner

module SnapshotId =
    let tryCreate value =
        IdConstruction.make "SnapshotId" SnapshotId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (SnapshotId inner) = inner

module PlanId =
    let tryCreate value =
        IdConstruction.make "PlanId" PlanId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (PlanId inner) = inner

module WorkId =
    let tryCreate value =
        IdConstruction.make "WorkId" WorkId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (WorkId inner) = inner

module AttemptId =
    let tryCreate value =
        IdConstruction.make "AttemptId" AttemptId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (AttemptId inner) = inner

module RoundId =
    let tryCreate value =
        IdConstruction.make "RoundId" RoundId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (RoundId inner) = inner

module ObservationId =
    let tryCreate value =
        IdConstruction.make "ObservationId" ObservationId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (ObservationId inner) = inner

module DecisionId =
    let tryCreate value =
        IdConstruction.make "DecisionId" DecisionId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (DecisionId inner) = inner

module CertificateId =
    let tryCreate value =
        IdConstruction.make "CertificateId" CertificateId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (CertificateId inner) = inner

module EventId =
    let tryCreate value =
        IdConstruction.make "EventId" EventId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (EventId inner) = inner

module NodeId =
    let tryCreate value =
        IdConstruction.make "NodeId" NodeId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (NodeId inner) = inner

module EdgeId =
    let tryCreate value =
        IdConstruction.make "EdgeId" EdgeId value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (EdgeId inner) = inner

module ArtifactRef =
    let tryCreate value =
        IdConstruction.make "ArtifactRef" ArtifactRef value

    let create value =
        match tryCreate value with
        | Ok id -> id
        | Error message -> IdConstruction.raiseId message

    let value (ArtifactRef inner) = inner

module Revision =
    let origin = Revision 0L

    let tryCreate value =
        if value < 0L then
            Error "Revision must not be negative"
        else
            Ok(Revision value)

    let create value =
        match tryCreate value with
        | Ok revision -> revision
        | Error message -> IdConstruction.raiseId message

    let value (Revision inner) = inner
    let next (Revision inner) = Revision(inner + 1L)

module Attempt =
    let first = Attempt 1L

    let tryCreate value =
        if value < 1L then
            Error "Attempt must be positive"
        else
            Ok(Attempt value)

    let create value =
        match tryCreate value with
        | Ok attempt -> attempt
        | Error message -> IdConstruction.raiseId message

    let value (Attempt inner) = inner
    let next (Attempt inner) = Attempt(inner + 1L)

module Fence =
    let tryCreate value = IdConstruction.make "Fence" Fence value

    let create value =
        match tryCreate value with
        | Ok fence -> fence
        | Error message -> IdConstruction.raiseId message

    let value (Fence inner) = inner

/// The single Core error type. Every fold failure carries a stable code plus a message;
/// the wire layer maps the code to a stable error object, never the prose.
type CoreError = { Code: string; Message: string }
