namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Runtime

/// The OpenCode Host adapter. Every receipt comes from the real Host; it owns no
/// inquiry state and executes only already-persisted work.
type OpenCodeHostPort =
    new: sessions: ISessionHostPort -> OpenCodeHostPort

    member Capabilities: unit -> string list

    /// Dispatches already-persisted work to a real session and returns its receipt.
    member Dispatch: InquiryId -> WorkSpec -> JsonEnvelope -> JsonEnvelope -> Task<Result<DispatchReceipt, string>>

    /// A Host "idle" is a physical notification, so it is reported as still running.
    member ReadStatus: InquiryId -> WorkId -> Attempt -> string -> Task<PhysicalStatus>
    member ReadResult: InquiryId -> WorkId -> Attempt -> string -> Task<Result<string option, string>>

    /// An unconfirmed abort is an error, so the inquiry stays cancelling.
    member RequestCancel: InquiryId -> WorkId -> Attempt -> string -> Task<Result<Unit, string>>
    member Reconcile: InquiryId -> string -> Task<Result<string option, string>>
