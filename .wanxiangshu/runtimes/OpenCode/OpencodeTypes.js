
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { list_type, obj_type, bool_type, record_type, option_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";

export class OpencodeModel extends Record {
    constructor(providerID, modelID, variant) {
        super();
        this.providerID = providerID;
        this.modelID = modelID;
        this.variant = variant;
    }
}

export function OpencodeModel_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeModel", [], OpencodeModel, () => [["providerID", string_type], ["modelID", string_type], ["variant", option_type(string_type)]]);
}

export class OpencodeTextPart extends Record {
    constructor(id, type, text, synthetic) {
        super();
        this.id = id;
        this.type = type;
        this.text = text;
        this.synthetic = synthetic;
    }
}

export function OpencodeTextPart_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeTextPart", [], OpencodeTextPart, () => [["id", string_type], ["type", string_type], ["text", string_type], ["synthetic", option_type(bool_type)]]);
}

export class OpencodeToolCallPart extends Record {
    constructor(id, type, callID, tool, args) {
        super();
        this.id = id;
        this.type = type;
        this.callID = callID;
        this.tool = tool;
        this.args = args;
    }
}

export function OpencodeToolCallPart_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeToolCallPart", [], OpencodeToolCallPart, () => [["id", string_type], ["type", string_type], ["callID", string_type], ["tool", string_type], ["args", option_type(obj_type)]]);
}

export class OpencodeCompactionPart extends Record {
    constructor(id, type, auto, overflow) {
        super();
        this.id = id;
        this.type = type;
        this.auto = auto;
        this.overflow = overflow;
    }
}

export function OpencodeCompactionPart_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeCompactionPart", [], OpencodeCompactionPart, () => [["id", string_type], ["type", string_type], ["auto", bool_type], ["overflow", bool_type]]);
}

export class OpencodeUserMessage extends Record {
    constructor(id, role, sessionID, agent, model, parts) {
        super();
        this.id = id;
        this.role = role;
        this.sessionID = sessionID;
        this.agent = agent;
        this.model = model;
        this.parts = parts;
    }
}

export function OpencodeUserMessage_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeUserMessage", [], OpencodeUserMessage, () => [["id", string_type], ["role", string_type], ["sessionID", string_type], ["agent", option_type(string_type)], ["model", option_type(OpencodeModel_$reflection())], ["parts", list_type(obj_type)]]);
}

export class OpencodeAssistantMessage extends Record {
    constructor(id, parentID, role, sessionID, agent, providerID, modelID, summary, error, parts) {
        super();
        this.id = id;
        this.parentID = parentID;
        this.role = role;
        this.sessionID = sessionID;
        this.agent = agent;
        this.providerID = providerID;
        this.modelID = modelID;
        this.summary = summary;
        this.error = error;
        this.parts = parts;
    }
}

export function OpencodeAssistantMessage_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeAssistantMessage", [], OpencodeAssistantMessage, () => [["id", string_type], ["parentID", option_type(string_type)], ["role", string_type], ["sessionID", string_type], ["agent", option_type(string_type)], ["providerID", option_type(string_type)], ["modelID", option_type(string_type)], ["summary", option_type(bool_type)], ["error", option_type(obj_type)], ["parts", list_type(obj_type)]]);
}

export class OpencodeHookInput extends Record {
    constructor(sessionID, messageID, agent, model) {
        super();
        this.sessionID = sessionID;
        this.messageID = messageID;
        this.agent = agent;
        this.model = model;
    }
}

export function OpencodeHookInput_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeHookInput", [], OpencodeHookInput, () => [["sessionID", string_type], ["messageID", option_type(string_type)], ["agent", option_type(string_type)], ["model", option_type(OpencodeModel_$reflection())]]);
}

export class OpencodeToolExecuteInput extends Record {
    constructor(tool, sessionID, callID) {
        super();
        this.tool = tool;
        this.sessionID = sessionID;
        this.callID = callID;
    }
}

export function OpencodeToolExecuteInput_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeToolExecuteInput", [], OpencodeToolExecuteInput, () => [["tool", string_type], ["sessionID", string_type], ["callID", string_type]]);
}

export class OpencodeToolExecuteOutput extends Record {
    constructor(args) {
        super();
        this.args = args;
    }
}

export function OpencodeToolExecuteOutput_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpencodeToolExecuteOutput", [], OpencodeToolExecuteOutput, () => [["args", obj_type]]);
}

