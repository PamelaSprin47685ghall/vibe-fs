namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Plugins

/// The provider adapter. Owns no inquiry state; executes only already-persisted calls
/// and reports the outcome the Host observed, including "no usage reported".
type ProviderAdapter =
    new: sessions: ISessionHostPort -> ProviderAdapter

    /// Runs one provider call and returns what the Host actually observed.
    member Run: InquiryId -> string -> SessionId -> Task<Result<Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome, string>>

    /// An unconfirmed cancel is an error, so the work stays cancelling.
    member Cancel: InquiryId -> string -> Task<Result<Unit, string>>
