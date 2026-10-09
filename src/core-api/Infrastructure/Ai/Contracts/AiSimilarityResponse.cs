using System.Text.Json.Serialization;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

public sealed record AiSimilarityResponse([property: JsonRequired] double Score);