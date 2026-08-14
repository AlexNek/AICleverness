using AiCleverness.Models;
using AiCleverness.Runtime.Capabilities;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class CapabilityResolutionTests
{
    private static CapabilityProfile Claude3 =>
        new()
            {
                Id = "anthropic/claude-3-sonnet",
                Name = "Claude 3 Sonnet",
                TypicalLatencyMs = 600,
                Tags = ["code", "reasoning"],
                Priority = 90,
                Capabilities = new()
                                   {
                                       CapabilityFlags =
                                           EModelCapability.TextGeneration
                                           | EModelCapability.ImageRecognition,
                                       MinContextWindow = 200000,
                                       MaxLatencyMs = 600,
                                       QualityTier = EQualityTier.High,
                                       CostTier = ECostTier.Optimal
                                   }
            };

    private static CapabilityProfile Gpt4o =>
        new()
            {
                Id = "openai/gpt-4o",
                Name = "GPT-4o",
                TypicalLatencyMs = 500,
                Tags = ["code", "reasoning", "creative"],
                Priority = 100,
                Capabilities = new()
                                   {
                                       CapabilityFlags =
                                           EModelCapability.TextGeneration
                                           | EModelCapability.ImageRecognition
                                           | EModelCapability.StructuredOutput,
                                       MinContextWindow = 128000,
                                       MaxLatencyMs = 500,
                                       QualityTier = EQualityTier.High,
                                       CostTier = ECostTier.Optimal
                                   }
            };

    private static CapabilityProfile Gpt4oMini =>
        new()
            {
                Id = "openai/gpt-4o-mini",
                Name = "GPT-4o Mini",
                TypicalLatencyMs = 200,
                Tags = ["code"],
                Priority = 50,
                Capabilities = new()
                                   {
                                       CapabilityFlags =
                                           EModelCapability.TextGeneration
                                           | EModelCapability.ImageRecognition
                                           | EModelCapability.StructuredOutput,
                                       MinContextWindow = 128000,
                                       MaxLatencyMs = 200,
                                       QualityTier = EQualityTier.Standard,
                                       CostTier = ECostTier.Cheap
                                   }
            };

    [Fact]
    public void AddProfile_IncreasesCount()
    {
        var resolver = new DefaultCapabilityResolver();
        resolver.AddProfile(Gpt4o);

        resolver.GetProfiles().Should().HaveCount(1);
    }

    [Fact]
    public void GetProfiles_ReturnsAllRegistered()
    {
        var resolver = new DefaultCapabilityResolver([Gpt4o, Gpt4oMini, Claude3]);

        resolver.GetProfiles().Should().HaveCount(3);
    }

    [Fact]
    public async Task Resolve_BudgetConstraint_FiltersCostlyModels()
    {
        var resolver = new DefaultCapabilityResolver([Gpt4o, Gpt4oMini]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.TextGeneration,
                                                     CostTier = ECostTier.Cheap
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o-mini");
    }

    [Fact]
    public async Task Resolve_CostStrategy_SelectsCheapest()
    {
        var resolver = new DefaultCapabilityResolver(
                [Gpt4o, Gpt4oMini, Claude3],
            new CostOptimizedModelSelectionStrategy());
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.TextGeneration
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o-mini");
    }

    [Fact]
    public async Task Resolve_LatencyConstraint_FiltersSlow()
    {
        var resolver = new DefaultCapabilityResolver([Gpt4o, Gpt4oMini]);
        var request = new CapabilityRequirements { Capabilities = new() { MaxLatencyMs = 300 } };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o-mini");
    }

    [Fact]
    public async Task Resolve_MinContextWindow_FiltersSmallModels()
    {
        var small = new CapabilityProfile
                        {
                            Id = "small-model",
                            Name = "Small",
                            Priority = 200,
                            Capabilities = new() { MinContextWindow = 4096 }
                        };
        var resolver = new DefaultCapabilityResolver([small, Gpt4o]);
        var request =
            new CapabilityRequirements { Capabilities = new() { MinContextWindow = 100000 } };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task Resolve_NoMatch_ReturnsFailed()
    {
        var noVideoGen = Gpt4oMini with
                             {
                                 Id = "no-video-gen",
                                 Capabilities = Gpt4oMini.Capabilities with
                                                    {
                                                        CapabilityFlags =
                                                        EModelCapability.TextGeneration
                                                        | EModelCapability.ImageRecognition
                                                        | EModelCapability.StructuredOutput
                                                        // No VideoGeneration
                                                    }
                             };
        var resolver = new DefaultCapabilityResolver([noVideoGen]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.VideoGeneration
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.Resolved.Should().BeFalse();
        result.Reason.Should().Contain("No available profile");
    }

    [Fact]
    public async Task Resolve_NoMatch_UsesFallback()
    {
        var noVideoGen = Gpt4oMini with
                             {
                                 Id = "no-video-gen",
                                 Capabilities = Gpt4oMini.Capabilities with
                                                    {
                                                        CapabilityFlags =
                                                        EModelCapability.TextGeneration
                                                        | EModelCapability.ImageRecognition
                                                        | EModelCapability.StructuredOutput
                                                    }
                             };
        var resolver = new DefaultCapabilityResolver(
                [noVideoGen],
            fallbackProfile: Gpt4o);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.VideoGeneration
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.Resolved.Should().BeTrue();
        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task Resolve_NullCapabilityFlags_MatchesAnyProfile()
    {
        var resolver = new DefaultCapabilityResolver([Gpt4o, Gpt4oMini]);
        var request =
            new CapabilityRequirements { Capabilities = new() { CapabilityFlags = null } };

        var result = await resolver.ResolveAsync(request);

        result.Resolved.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_PriorityStrategy_SelectsHighestPriority()
    {
        var resolver = new DefaultCapabilityResolver([Gpt4o, Gpt4oMini, Claude3]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.TextGeneration
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task Resolve_ProfileWithNullFlags_AssumedCapable()
    {
        var undeclared = new CapabilityProfile
                             {
                                 Id = "undeclared",
                                 Name = "Undeclared",
                                 Priority = 200,
                                 Capabilities =
                                     new()
                                         {
                                             CapabilityFlags = null
                                         } // not declared = assume capable
                             };
        var resolver = new DefaultCapabilityResolver([undeclared]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.ImageRecognition
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.Resolved.Should().BeTrue();
        result.SelectedProfile!.Id.Should().Be("undeclared");
    }

    [Fact]
    public async Task Resolve_QualityTier_FiltersNonMatchingTiers()
    {
        var lowQuality = new CapabilityProfile
                             {
                                 Id = "low-quality",
                                 Name = "Low Quality",
                                 Priority = 200,
                                 Capabilities = new() { QualityTier = EQualityTier.Economy }
                             };
        var resolver = new DefaultCapabilityResolver([lowQuality, Gpt4o]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new() { QualityTier = EQualityTier.Standard }
                          };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task Resolve_ReturnsCandidatesInResult()
    {
        var resolver = new DefaultCapabilityResolver([Gpt4o, Gpt4oMini, Claude3]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.TextGeneration
                                                         | EModelCapability.ImageRecognition
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.Candidates.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Resolve_UnavailableProfile_IsExcluded()
    {
        var unavailable = Gpt4o with { IsAvailable = false };
        var resolver = new DefaultCapabilityResolver([unavailable, Gpt4oMini]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.TextGeneration
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o-mini");
    }

    [Fact]
    public async Task Resolve_VisionRequired_FiltersNonVisionModels()
    {
        var noVision = new CapabilityProfile
                           {
                               Id = "text-only",
                               Name = "Text Only",
                               Priority = 200,
                               Capabilities =
                                   new()
                                       {
                                           CapabilityFlags = EModelCapability.TextGeneration
                                       } // no ImageRecognition
                           };
        var resolver = new DefaultCapabilityResolver([noVision, Gpt4o]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.ImageRecognition
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.SelectedProfile!.Id.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task Resolve_WithMatchingProfile_ReturnsSuccess()
    {
        var resolver = new DefaultCapabilityResolver([Gpt4o, Gpt4oMini]);
        var request = new CapabilityRequirements
                          {
                              Capabilities = new()
                                                 {
                                                     CapabilityFlags =
                                                         EModelCapability.TextGeneration
                                                 }
                          };

        var result = await resolver.ResolveAsync(request);

        result.Resolved.Should().BeTrue();
        result.SelectedProfile.Should().NotBeNull();
    }
}
