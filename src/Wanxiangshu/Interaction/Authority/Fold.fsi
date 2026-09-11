namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt.Fallback

[<RequireQualifiedAccess>]
type PromptAuthorityProjectionChange =
    | PromptAuthoritySet of SessionId * PromptAuthority.PromptAuthorityProjection
    | ProviderFailuresSet of SessionId * ProviderFailureProjection

[<RequireQualifiedAccess>]
type PromptAuthorityFoldRejection =
    | ClaimOriginRejected of PromptAuthority.IdentitySeedValidationError
    | AuthorityRootSchemaRejected of string
    | AuthorityRootSeedRejected of PromptAuthority.IdentitySeedValidationError
    | AuthorityRootLedgerRejected of string

[<RequireQualifiedAccess>]
module PromptAuthorityFoldRejection =
    val fact: PromptAuthorityFoldRejection -> string
    val message: PromptAuthorityFoldRejection -> string

module PromptFactFold =
    val fold:
        authorityOf: (SessionId -> PromptAuthority.PromptAuthorityProjection option) ->
        runtimeStartCount: int ->
        fact: PromptFactCases ->
            Result<PromptAuthorityProjectionChange list, PromptAuthorityFoldRejection>
