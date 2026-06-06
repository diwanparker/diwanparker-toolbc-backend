using Toolbc.Api.Domain;

namespace Toolbc.Api.Contracts;

public sealed record ChatTurnDto(string Text, bool FromUser);

public sealed record ChatReplyRequest(IReadOnlyList<ChatTurnDto> History, UserRole Mode = UserRole.Patient);

public sealed record ChatReplyResponse(string Reply, bool UsedAiProvider);
