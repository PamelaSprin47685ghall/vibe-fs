namespace Wanxiangshu.Enforcer

type EnforcerRule =
    { Name: string
      EnforcerText: string
      MainText: string
      RuleId: string
      FieldName: string
      LexicalOrder: int }

module EnforcerCatalog =
    val validate: schemaVersion: int -> rules: EnforcerRule list -> Result<EnforcerRule list, string>
    val tryFindByField: field: string -> rules: EnforcerRule list -> EnforcerRule option
    val fieldNames: rules: EnforcerRule list -> string list
