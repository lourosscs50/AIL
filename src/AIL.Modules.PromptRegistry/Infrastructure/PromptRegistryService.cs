using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.PromptRegistry.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.PromptRegistry.Infrastructure;

public sealed class PromptRegistryService : IPromptRegistryService
{
    private readonly IPromptDefinitionRepository _repository;

    public PromptRegistryService(IPromptDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PromptResolution> ResolvePromptAsync(
        string promptKey,
        string? promptVersion = null,
        IDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
            throw new ArgumentException("Prompt key is required", nameof(promptKey));

        var candidates = (await _repository.GetByKeyAsync(promptKey, cancellationToken)).ToList();
        if (!candidates.Any())
            throw new PromptNotFoundException(promptKey);

        if (!string.IsNullOrWhiteSpace(promptVersion))
            return ResolveByVersion(promptKey, promptVersion, variables, candidates);

        return ResolveActiveVersion(promptKey, variables, candidates);
    }

    private PromptResolution ResolveByVersion(string promptKey, string promptVersion, IDictionary<string, string>? variables, List<PromptDefinition> candidates)
    {
        var matches = candidates
            .Where(x => string.Equals(x.Version, promptVersion, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!matches.Any())
            throw new PromptVersionNotFoundException(promptKey, promptVersion);

        if (matches.Count > 1)
            throw new PromptAmbiguousException(promptKey, promptVersion);

        var selected = matches.Single();

        if (!selected.IsActive)
            throw new PromptInactiveException(promptKey, promptVersion);

        ValidateVariableContract(selected, variables);

        return CreateResolution(selected);
    }

    private PromptResolution ResolveActiveVersion(string promptKey, IDictionary<string, string>? variables, List<PromptDefinition> candidates)
    {
        var activeCandidates = candidates.Where(x => x.IsActive).ToList();
        if (!activeCandidates.Any())
            throw new PromptInactiveException(promptKey);

        var versionGroups = activeCandidates
            .GroupBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var chosenVersion = versionGroups
            .Select(g => g.Key)
            .OrderByDescending(v => new VersionKey(v), new VersionKeyComparer())
            .First();

        var chosenItems = activeCandidates
            .Where(x => string.Equals(x.Version, chosenVersion, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (chosenItems.Count > 1)
            throw new PromptAmbiguousException(promptKey, chosenVersion);

        var resolved = chosenItems.Single();

        ValidateVariableContract(resolved, variables);

        return CreateResolution(resolved);
    }

    private static PromptResolution CreateResolution(PromptDefinition prompt)
        => new PromptResolution(
            PromptKey: prompt.PromptKey,
            Version: prompt.Version,
            Template: prompt.Template,
            VariableDefinitions: prompt.VariableDefinitions);

    public async Task<PromptDefinition> CreatePromptVersionAsync(PromptDefinition promptDefinition, CancellationToken cancellationToken = default)
    {
        if (promptDefinition is null)
            throw new ArgumentNullException(nameof(promptDefinition));

        ValidatePromptDefinition(promptDefinition);

        var existing = await _repository.GetByKeyAndVersionAsync(promptDefinition.PromptKey, promptDefinition.Version, cancellationToken);
        if (existing is not null)
            throw new PromptAmbiguousException(promptDefinition.PromptKey, promptDefinition.Version);

        await _repository.AddAsync(promptDefinition, cancellationToken);
        return promptDefinition;
    }

    public async Task ActivatePromptVersionAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
            throw new ArgumentException("Prompt key is required", nameof(promptKey));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required", nameof(version));

        var current = await _repository.GetByKeyAndVersionAsync(promptKey, version, cancellationToken);
        if (current is null)
            throw new PromptVersionNotFoundException(promptKey, version);

        if (current.IsActive)
            return;

        var updated = current with { IsActive = true };
        await _repository.UpdateAsync(updated, cancellationToken);
    }

    public async Task DeactivatePromptVersionAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
            throw new ArgumentException("Prompt key is required", nameof(promptKey));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required", nameof(version));

        var current = await _repository.GetByKeyAndVersionAsync(promptKey, version, cancellationToken);
        if (current is null)
            throw new PromptVersionNotFoundException(promptKey, version);

        if (!current.IsActive)
            return;

        var updated = current with { IsActive = false };
        await _repository.UpdateAsync(updated, cancellationToken);
    }

    public async Task<PromptDefinition> PromotePromptVersionAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        await ActivatePromptVersionAsync(promptKey, version, cancellationToken);
        var promoted = await _repository.GetByKeyAndVersionAsync(promptKey, version, cancellationToken);
        if (promoted is null)
            throw new PromptVersionNotFoundException(promptKey, version);

        return promoted;
    }

    private static void ValidatePromptDefinition(PromptDefinition promptDefinition)
    {
        if (string.IsNullOrWhiteSpace(promptDefinition.PromptKey))
            throw new PromptValidationException("Prompt definition PromptKey is required.");

        if (string.IsNullOrWhiteSpace(promptDefinition.Version))
            throw new PromptValidationException("Prompt definition Version is required.");

        if (string.IsNullOrWhiteSpace(promptDefinition.Template))
            throw new PromptValidationException("Prompt definition Template is required.");

        if (promptDefinition.VariableDefinitions is null)
            throw new PromptValidationException("Prompt definition VariableDefinitions cannot be null.");

        // Ensure version is semver-compatible and parseable by the same logic used in resolution.
        _ = new VersionKey(promptDefinition.Version).Segments.ToArray();
    }

    private static void ValidateVariableContract(PromptDefinition prompt, IDictionary<string, string>? variables)
    {
        variables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var requiredNames = prompt.VariableDefinitions
            .Where(kv => kv.Value.Required)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = requiredNames.Except(variables.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Any())
            throw new PromptValidationException($"Missing required variables: {string.Join(", ", missing)}");

        var unknown = variables.Keys
            .Except(prompt.VariableDefinitions.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknown.Any())
            throw new PromptValidationException($"Unknown variables are not allowed: {string.Join(", ", unknown)}");
    }

    private sealed record VersionKey(string Raw)
    {
        private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

        private static readonly IEnumerable<int> Empty = Array.Empty<int>();

        private readonly string _normalized = Raw.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? Raw[1..] : Raw;

        public IEnumerable<int> Segments
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_normalized))
                    throw new PromptValidationException($"Invalid version format '{Raw}'.");

                return _normalized.Split('.').Select(x =>
                {
                    if (!int.TryParse(x, out var i))
                        throw new PromptValidationException($"Invalid version segment '{x}' in version '{Raw}'.");

                    return i;
                });
            }
        }

        public override string ToString() => Raw;
    }

    private static int CompareVersionKeys(VersionKey a, VersionKey b)
    {
        var segmentsA = a.Segments.ToArray();
        var segmentsB = b.Segments.ToArray();
        var length = Math.Max(segmentsA.Length, segmentsB.Length);

        for (var i = 0; i < length; i++)
        {
            var segA = i < segmentsA.Length ? segmentsA[i] : 0;
            var segB = i < segmentsB.Length ? segmentsB[i] : 0;
            if (segA != segB)
                return segA.CompareTo(segB);
        }

        return 0;
    }

    private sealed class VersionKeyComparer : IComparer<VersionKey>
    {
        public int Compare(VersionKey? x, VersionKey? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return CompareVersionKeys(x, y);
        }
    }
}
