using BoneAI.AI;
using Newtonsoft.Json.Linq;

var model = CodexModelInfo.Parse(JObject.Parse(@"{""id"":""small"",""model"":""gpt-small"",""displayName"":""Small Model"",""hidden"":true,""isDefault"":false,""defaultReasoningEffort"":""high"",""supportedReasoningEfforts"":[{""reasoningEffort"":""low""},{""reasoningEffort"":""medium""}]}"));
if (model is null || model.Id != "gpt-small" || !model.Hidden || model.DefaultEffort != "low" || model.SupportedEfforts.Count != 2)
    throw new Exception("Codex model parsing or effort fallback failed.");
if (CodexModelInfo.Parse(new JObject()) is not null) throw new Exception("Model with no identifier must be rejected.");
Console.WriteLine("Codex model catalog tests passed.");
