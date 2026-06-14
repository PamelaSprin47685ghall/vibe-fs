module VibeFs.Kernel.Schema

/// A field-level schema for tool metadata — declarative validation data.
type SchemaType =
    | StringType | NumberType | BooleanType | EnumType
    | ArrayType | ObjectType | UnionType

type SchemaField =
    { ``type``: SchemaType
      description: string option
      optional: bool
      enumValues: string list
      items: SchemaField option
      properties: Map<string, SchemaField>
      anyOf: SchemaField list }

type ToolMetadata =
    { name: string
      description: string
      parameters: Map<string, SchemaField> }
