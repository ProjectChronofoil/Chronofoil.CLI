using System.Text.Json.Serialization;
using Chronofoil.Common;
using Chronofoil.Common.Censor;

namespace Chronofoil.CLI;

[JsonSerializable(typeof(ApiResult))]
[JsonSerializable(typeof(ApiResult<CensoredOpcodesResponse>))]
[JsonSerializable(typeof(CensoredOpcodesResponse))]
[JsonSerializable(typeof(ApiStatusCode))]
[JsonSerializable(typeof(Dictionary<string, int>))]

public partial class JsonContext : JsonSerializerContext
{
}