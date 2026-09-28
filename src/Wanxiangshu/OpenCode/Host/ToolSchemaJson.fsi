namespace Wanxiangshu.OpenCode.Host

/// Provider-visible JSON schema for a Host tool definition.
///
/// opencode 1.18.32 hands `tool.definition` the tool's own argument schema: a
/// built-in Host tool carries an Effect `parameters` schema and no JSON schema,
/// and the Host renders the provider-visible JSON schema itself
/// (`packages/opencode/src/tool/json-schema.ts`: `Schema.toJsonSchemaDocument`
/// with `additionalProperties: true`, then its own normalization). This module
/// performs that same rendering inside the plugin so a decorated tool can be
/// published through `output.jsonSchema` while the Effect schema the Host
/// decodes arguments with stays untouched.
module ToolSchemaJson =

    /// Load Phase: resolve the Effect Schema renderer once. A failure is held
    /// rather than raised here, so an unrelated missing dependency cannot stop
    /// the plugin; the protocol that cannot render a schema fails closed when
    /// it is actually asked for one.
    val initialize: unit -> System.Threading.Tasks.Task

    /// Render `parameters` (the Effect schema a Host tool definition carries)
    /// into the same JSON schema the Host would have produced for it.
    /// Results are cached per schema object.
    val providerSchema: parameters: obj -> obj
