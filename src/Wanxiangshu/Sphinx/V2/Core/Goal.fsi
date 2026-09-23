namespace Wanxiangshu.Sphinx.V2.Core

type GoalAmendment =
    { AuthorizedBy: string
      Revision: Revision
      AddedConstraints: string list
      ReplacedText: string option }

type GoalSpec =
    {
        GoalId: GoalId
        Revision: Revision
        /// Byte-exact user text. Never normalized, trimmed or reworded in storage.
        OriginalText: string
        /// Explicit supplementary constraints from the user, as supplied.
        Constraints: string list
        /// Content refs of the material the user attached; may be empty.
        MaterialRefs: ArtifactRef list
        /// Authorization reference proving the user supplied this goal.
        AuthorizationRef: string
        CreatedBy: string
        Amendments: GoalAmendment list
    }

type GoalError = { Code: string; Message: string }

module Goal =
    val tryCreate: GoalSpec -> Result<GoalSpec, GoalError>
    val create: GoalSpec -> GoalSpec

    val amend:
        authorizedBy: string -> addedConstraints: string list -> replacementText: string option -> GoalSpec -> GoalSpec

    val tryAmend:
        authorizedBy: string ->
        addedConstraints: string list ->
        replacementText: string option ->
        GoalSpec ->
            Result<GoalSpec, GoalError>
