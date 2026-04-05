using System;
using System.Collections.Generic;

namespace AIL.Modules.Execution.Application.Visibility;

/// <summary>
/// Cross-system identifier semantics (read-only observability; not execution authority).
/// <list type="bullet">
/// <item><description><see cref="TraceThreadId"/> — distributed / platform trace thread when callers propagate it; often null until gateways wire it.</description></item>
/// <item><description><see cref="CorrelationGroupId"/> — optional broader grouping (incident, batch); null when not supplied.</description></item>
/// <item><description><see cref="ExecutionInstanceId"/> — stable id for this A.I.L. execution invocation (generated if omitted on request).</description></item>
/// <item><description><see cref="RelatedEntityIds"/> — parsed <see cref="Guid"/> values from context reference strings where parseable; not a substitute for full context refs.</description></item>
/// </list>
/// </summary>
public sealed record ExecutionTraceVisibility(
    string? TraceThreadId,
    Guid? CorrelationGroupId,
    Guid ExecutionInstanceId,
    IReadOnlyList<Guid> RelatedEntityIds);
