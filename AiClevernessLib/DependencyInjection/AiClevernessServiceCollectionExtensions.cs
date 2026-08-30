using System.Diagnostics.CodeAnalysis;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Capabilities;
using AiCleverness.Runtime.Conversation;
using AiCleverness.Runtime.DecisionTree;
using AiCleverness.Runtime.Filtering;

using Microsoft.Extensions.DependencyInjection.Extensions;

// DI registration methods use generic type parameters to register implementations.
// The DI container preserves constructors at runtime; trimming warnings are false positives
// for this standard .NET DI pattern.
[assembly:
    UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors for registered implementations.")]

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AiCleverness services with <see cref="IServiceCollection"/>.
/// </summary>
public static class AiClevernessServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IAgentInputValidator"/> that runs on all agents (global).
    /// </summary>
    public static IServiceCollection AddAgentInputValidator<TValidator>(
        this IServiceCollection services)
        where TValidator : class, IAgentInputValidator
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentInputValidator, TValidator>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentInputValidator"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentInputValidator<TValidator>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TValidator : class, IAgentInputValidator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentInputValidator>(sp =>
            new FilteredInputValidator(
                ActivatorUtilities.CreateInstance<TValidator>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentObserver"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IAgentObserver
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentObserver, TObserver>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentObserver"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentObserver<TObserver>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TObserver : class, IAgentObserver
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentObserver>(sp =>
            new FilteredObserver(
                ActivatorUtilities.CreateInstance<TObserver>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentPipelineMiddleware"/> implementation.
    /// Middleware is invoked in registration order between the built-in middleware and the LLM tool loop.
    /// </summary>
    public static IServiceCollection AddAgentPipelineMiddleware<TMiddleware>(
        this IServiceCollection services)
        where TMiddleware : class, IAgentPipelineMiddleware
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentPipelineMiddleware, TMiddleware>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentPipelineMiddleware"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentPipelineMiddleware<TMiddleware>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TMiddleware : class, IAgentPipelineMiddleware
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentPipelineMiddleware>(sp =>
            new FilteredMiddleware(
                ActivatorUtilities.CreateInstance<TMiddleware>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentPlanner"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentPlanner<TPlanner>(this IServiceCollection services)
        where TPlanner : class, IAgentPlanner
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentPlanner, TPlanner>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentPolicy"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentPolicy<TPolicy>(this IServiceCollection services)
        where TPolicy : class, IAgentPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentPolicy, TPolicy>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentPolicy"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentPolicy<TPolicy>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TPolicy : class, IAgentPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentPolicy>(sp =>
            new FilteredPolicy(
                ActivatorUtilities.CreateInstance<TPolicy>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentQualityGate"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentQualityGate<TGate>(this IServiceCollection services)
        where TGate : class, IAgentQualityGate
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentQualityGate, TGate>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentQualityGate"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentQualityGate<TGate>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TGate : class, IAgentQualityGate
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentQualityGate>(sp =>
            new FilteredQualityGate(
                ActivatorUtilities.CreateInstance<TGate>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentResultTransformer"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentResultTransformer<TTransformer>(
        this IServiceCollection services)
        where TTransformer : class, IAgentResultTransformer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentResultTransformer, TTransformer>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentResultTransformer"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentResultTransformer<TTransformer>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TTransformer : class, IAgentResultTransformer
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentResultTransformer>(sp =>
            new FilteredTransformer(
                ActivatorUtilities.CreateInstance<TTransformer>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentResultValidator"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentResultValidator<TValidator>(
        this IServiceCollection services)
        where TValidator : class, IAgentResultValidator
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentResultValidator, TValidator>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentResultValidator"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentResultValidator<TValidator>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TValidator : class, IAgentResultValidator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentResultValidator>(sp =>
            new FilteredResultValidator(
                ActivatorUtilities.CreateInstance<TValidator>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentStrategy"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : class, IAgentStrategy
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentStrategy, TStrategy>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAgentStrategy"/> scoped to agents matching the predicate.
    /// </summary>
    public static IServiceCollection AddAgentStrategy<TStrategy>(
        this IServiceCollection services,
        Func<IAgentContext, bool> appliesTo)
        where TStrategy : class, IAgentStrategy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(appliesTo);
        services.AddSingleton<IAgentStrategy>(sp =>
            new FilteredStrategy(
                ActivatorUtilities.CreateInstance<TStrategy>(sp),
                appliesTo));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="ITool"/> implementation in the tool registry.
    /// </summary>
    public static IServiceCollection AddAgentTool<TTool>(this IServiceCollection services)
        where TTool : class, ITool
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<TTool>();
        services.AddSingleton<ITool>(sp => sp.GetRequiredService<TTool>());
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IToolExecutor"/> implementation.
    /// </summary>
    public static IServiceCollection AddAgentToolExecutor<TExecutor>(
        this IServiceCollection services)
        where TExecutor : class, IToolExecutor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IToolExecutor, TExecutor>();
        return services;
    }

    /// <summary>
    /// Registers a concrete <see cref="ILlmClient"/> implementation.
    /// </summary>
    public static IServiceCollection AddAiClevernessLlmClient<TClient>(
        this IServiceCollection services)
        where TClient : class, ILlmClient
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ILlmClient, TClient>();
        return services;
    }

    /// <summary>
    /// Adds the core AiCleverness runtime services: context factory, in-memory memory,
    /// tool registry, and policy/strategy/planner discovery via DI.
    /// </summary>
    public static IServiceCollection AddAiClevernessRuntime(this IServiceCollection services)
    {
        return services.AddAiClevernessRuntime(null, null);
    }

    /// <summary>
    /// Adds the core AiCleverness runtime services and configures runtime defaults.
    /// </summary>
    public static IServiceCollection AddAiClevernessRuntime(
        this IServiceCollection services,
        Action<AgentRuntimeOptions>? configure)
    {
        return services.AddAiClevernessRuntime(configure, null);
    }

    /// <summary>
    /// Adds the core runtime services and configures runtime and failure-classification defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional runtime configuration.</param>
    /// <param name="configureFailureClassification">
    /// Optional application-owned provider error and status mappings.
    /// </param>
    public static IServiceCollection AddAiClevernessRuntime(
        this IServiceCollection services,
        Action<AgentRuntimeOptions>? configure,
        Action<LlmFailureClassificationOptions>? configureFailureClassification)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IToolRegistry, ToolRegistry>();
        services.TryAddSingleton<IToolExecutor, DefaultToolExecutor>();
        services.TryAddSingleton<IAgentMemory, InMemoryAgentMemory>();
        services.TryAddSingleton(sp =>
        {
            var options = new LlmFailureClassificationOptions();
            configureFailureClassification?.Invoke(options);
            return options;
        });
        services.TryAddSingleton<ILlmCompletionPipeline, DefaultLlmCompletionPipeline>();
        services.TryAddSingleton<IAgentRuntime, AgentRuntime>();
        services.TryAddSingleton<IPlannerRegistry, PlannerRegistry>();
        services.TryAddSingleton<IStrategyRegistry, StrategyRegistry>();
        services.TryAddSingleton(sp =>
            {
                var options = new AgentRuntimeOptions();
                configure?.Invoke(options);
                return options;
            });

        return services;
    }

    /// <summary>
    /// Adds the decision-tree execution services and default generic predicates.
    /// Conversation managers are transient so each resolved executor receives isolated history.
    /// </summary>
    public static IServiceCollection AddDecisionTreeExecution(
        this IServiceCollection services,
        Action<DecisionTreeExecutionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(sp =>
        {
            var options = new DecisionTreeExecutionOptions();
            configure?.Invoke(options);
            return options;
        });
        services.TryAddSingleton<IExecutionJournal, InMemoryExecutionJournal>();
        services.TryAddSingleton<IExecutionEventPublisher, InMemoryEventBus>();
        services.TryAddSingleton<ILlmCompletionPipeline, DefaultLlmCompletionPipeline>();
        services.TryAddTransient<IConversationManager, DefaultConversationManager>();
        services.TryAddTransient<IConversationManagerFactory, DefaultConversationManagerFactory>();
        services.TryAddSingleton<IDecisionDataPolicy>(sp =>
            new DefaultDecisionDataPolicy(
                sp.GetRequiredService<DecisionTreeExecutionOptions>().DecisionDataPolicy));
        services.TryAddSingleton<IDecisionLlmContextBuilder, DefaultDecisionLlmContextBuilder>();
        services.TryAddSingleton<EnumAnswerParser>();
        services.TryAddSingleton<IDecisionTreeLoader, DecisionTreeLoader>();
        services.TryAddTransient<DecisionTreeExecutor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionPredicate, PropertyExistsPredicate>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionPredicate, DataExistsPredicate>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionPredicate, DataCountAtLeastPredicate>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionPredicate, PropertyEqualsPredicate>());
        return services;
    }

    /// <summary>Registers an application decision action.</summary>
    public static IServiceCollection AddDecisionAction<TAction>(this IServiceCollection services)
        where TAction : class, IDecisionAction
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDecisionAction, TAction>();
        return services;
    }

    /// <summary>Registers an application decision predicate.</summary>
    public static IServiceCollection AddDecisionPredicate<TPredicate>(this IServiceCollection services)
        where TPredicate : class, IDecisionPredicate
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDecisionPredicate, TPredicate>();
        return services;
    }

    // ============================================================
    // Agent-scoped registration overloads
    // ============================================================

    /// <summary>
    /// Registers the default <see cref="ICapabilityResolver"/> with the given profiles.
    /// </summary>
    public static IServiceCollection AddCapabilityResolver(
        this IServiceCollection services,
        IReadOnlyList<CapabilityProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(profiles);
        services.AddSingleton<ICapabilityResolver>(sp =>
            {
                var resolver = new DefaultCapabilityResolver(profiles);
                return resolver;
            });
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="ICapabilityResolver"/> implementation.
    /// </summary>
    public static IServiceCollection AddCapabilityResolver<TResolver>(
        this IServiceCollection services)
        where TResolver : class, ICapabilityResolver
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICapabilityResolver, TResolver>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="ICheckpointStore"/> implementation.
    /// </summary>
    public static IServiceCollection AddCheckpointStore<TStore>(this IServiceCollection services)
        where TStore : class, ICheckpointStore
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICheckpointStore, TStore>();
        return services;
    }

    /// <summary>
    /// Registers the default LLM-based planner.
    /// </summary>
    public static IServiceCollection AddDefaultPlanner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentPlanner, DefaultPlanner>();
        services.AddSingleton<INamedAgentPlanner, DefaultPlanner>();
        return services;
    }

    /// <summary>
    /// Registers the default <see cref="IDiagnosticCollector"/>.
    /// </summary>
    public static IServiceCollection AddDiagnosticCollector(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IDiagnosticCollector, DefaultDiagnosticCollector>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IDiagnosticCollector"/> implementation.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddDiagnosticCollector<TCollector>(
        this IServiceCollection services)
        where TCollector : class, IDiagnosticCollector
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IDiagnosticCollector, TCollector>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IExecutionEventHandler{TEvent}"/> implementation.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddExecutionEventHandler<THandler, TEvent>(
        this IServiceCollection services)
        where THandler : class, IExecutionEventHandler<TEvent>
        where TEvent : IExecutionEvent
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IExecutionEventHandler<TEvent>, THandler>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IExecutionEventPublisher"/> implementation.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddExecutionEventPublisher<TPublisher>(
        this IServiceCollection services)
        where TPublisher : class, IExecutionEventPublisher
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IExecutionEventPublisher, TPublisher>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IExecutionJournal"/> implementation.
    /// </summary>
    public static IServiceCollection AddExecutionJournal<TJournal>(this IServiceCollection services)
        where TJournal : class, IExecutionJournal
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IExecutionJournal, TJournal>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IExecutionReplayer"/> implementation.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddExecutionReplayer<TReplayer>(
        this IServiceCollection services)
        where TReplayer : class, IExecutionReplayer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IExecutionReplayer, TReplayer>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IExecutionScheduler"/> implementation.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddExecutionScheduler<TScheduler>(
        this IServiceCollection services)
        where TScheduler : class, IExecutionScheduler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IExecutionScheduler, TScheduler>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="HostedAgentRuntimeService"/> as a hosted service
    /// and configures its options.
    /// </summary>
    public static IServiceCollection AddHostedAgentRuntime(this IServiceCollection services)
    {
        return services.AddHostedAgentRuntime(null);
    }

    /// <summary>
    /// Registers the <see cref="HostedAgentRuntimeService"/> as a hosted service
    /// and configures its options.
    /// </summary>
    public static IServiceCollection AddHostedAgentRuntime(
        this IServiceCollection services,
        Action<HostedRuntimeOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp =>
            {
                var options = new HostedRuntimeOptions();
                configure?.Invoke(options);
                return options;
            });

        services.AddShutdownCoordinator();
        services.AddHostedService<HostedAgentRuntimeService>();

        return services;
    }

    /// <summary>
    /// Registers the in-memory <see cref="IIdempotencyCache"/>.
    /// </summary>
    public static IServiceCollection AddIdempotencyCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IIdempotencyCache, InMemoryIdempotencyCache>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IIdempotencyCache"/> implementation.
    /// </summary>
    public static IServiceCollection AddIdempotencyCache<TCache>(this IServiceCollection services)
        where TCache : class, IIdempotencyCache
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IIdempotencyCache, TCache>();
        return services;
    }

    /// <summary>
    /// Registers the in-memory <see cref="ICheckpointStore"/>.
    /// </summary>
    public static IServiceCollection AddInMemoryCheckpointStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICheckpointStore, InMemoryCheckpointStore>();
        return services;
    }

    /// <summary>
    /// Registers the in-memory <see cref="IExecutionEventPublisher"/>.
    /// </summary>
    public static IServiceCollection AddInMemoryEventBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IExecutionEventPublisher, InMemoryEventBus>();
        return services;
    }

    /// <summary>
    /// Registers the in-memory <see cref="IExecutionJournal"/>.
    /// </summary>
    public static IServiceCollection AddInMemoryExecutionJournal(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IExecutionJournal, InMemoryExecutionJournal>();
        return services;
    }

    /// <summary>
    /// Registers the default <see cref="IMetricsCollector"/>.
    /// </summary>
    public static IServiceCollection AddMetricsCollector(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IMetricsCollector, DefaultMetricsCollector>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IMetricsCollector"/> implementation.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddMetricsCollector<TCollector>(
        this IServiceCollection services)
        where TCollector : class, IMetricsCollector
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IMetricsCollector, TCollector>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="IModelCatalog"/> with a profile-to-model mapping dictionary.
    /// </summary>
    public static IServiceCollection AddModelCatalog(
        this IServiceCollection services,
        IReadOnlyDictionary<string, IReadOnlyList<ModelDefinition>> mapping)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mapping);
        services.TryAddSingleton<IModelCatalog>(new DefaultModelCatalog(mapping));
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IModelCatalog"/> implementation.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddModelCatalog<TCatalog>(this IServiceCollection services)
        where TCatalog : class, IModelCatalog
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IModelCatalog, TCatalog>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="IModelManager"/>. Defaults to <see cref="DefaultModelManager"/>.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddModelManager<TManager>(this IServiceCollection services)
        where TManager : class, IModelManager
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IModelManager, TManager>();
        return services;
    }

    /// <summary>
    /// Registers the default model resolution stack: <see cref="DefaultSelectionPolicy"/> and <see cref="DefaultModelManager"/>.
    /// Requires <see cref="ICapabilityResolver"/> and <see cref="IModelCatalog"/> to be already registered.
    /// </summary>
    public static IServiceCollection AddModelResolution(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IModelSelectionPolicy, DefaultSelectionPolicy>();
        services.TryAddSingleton<IModelManager, DefaultModelManager>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="IModelSelectionPolicy"/>. Defaults to <see cref="DefaultSelectionPolicy"/>.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "DI container preserves constructors.")]
    public static IServiceCollection AddModelSelectionPolicy<TPolicy>(
        this IServiceCollection services)
        where TPolicy : class, IModelSelectionPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IModelSelectionPolicy, TPolicy>();
        return services;
    }

    /// <summary>
    /// Registers a named planner implementation.
    /// Named planners are discoverable through <see cref="IPlannerRegistry"/>.
    /// </summary>
    public static IServiceCollection AddNamedPlanner<TPlanner>(this IServiceCollection services)
        where TPlanner : class, INamedAgentPlanner
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<INamedAgentPlanner, TPlanner>();
        // Also register as IAgentPlanner for backward compatibility
        services.AddSingleton<IAgentPlanner>(sp => sp.GetRequiredService<TPlanner>());
        return services;
    }

    /// <summary>
    /// Registers the <see cref="OpenTelemetryObserverSample"/> as an agent observer.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryObserver(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentObserver, OpenTelemetryObserverSample>();
        return services;
    }

    /// <summary>
    /// Registers the sequential planner.
    /// </summary>
    public static IServiceCollection AddSequentialPlanner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SequentialPlanner>();
        services.AddSingleton<INamedAgentPlanner>(sp => sp.GetRequiredService<SequentialPlanner>());
        services.AddSingleton<IAgentPlanner>(sp => sp.GetRequiredService<SequentialPlanner>());
        return services;
    }

    /// <summary>
    /// Registers the default <see cref="IShutdownCoordinator"/>.
    /// </summary>
    public static IServiceCollection AddShutdownCoordinator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IShutdownCoordinator, DefaultShutdownCoordinator>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IShutdownHook"/> implementation.
    /// </summary>
    public static IServiceCollection AddShutdownHook<THook>(this IServiceCollection services)
        where THook : class, IShutdownHook
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IShutdownHook, THook>();
        return services;
    }

    /// <summary>
    /// Registers the default <see cref="IStartupAnalyzer"/>.
    /// </summary>
    public static IServiceCollection AddStartupAnalyzer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IStartupAnalyzer, DefaultStartupAnalyzer>();
        return services;
    }

    /// <summary>
    /// Runs the <see cref="IStartupAnalyzer"/> against the given service provider
    /// and throws <see cref="InvalidOperationException"/> if any errors are found.
    /// Call this after building the service provider to fail fast on missing services.
    /// </summary>
    /// <param name="serviceProvider">The built service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The same service provider, validated.</returns>
    /// <exception cref="InvalidOperationException">Thrown when startup analysis finds errors.</exception>
    public static async Task<IServiceProvider> ValidateAiClevernessAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var analyzer = serviceProvider.GetService<IStartupAnalyzer>();
        if (analyzer is null)
        {
            throw new InvalidOperationException(
                "IStartupAnalyzer is not registered. Call AddStartupAnalyzer() before validating.");
        }

        var result = await analyzer.AnalyzeAsync(serviceProvider, cancellationToken);
        result.ThrowOnErrors();

        return serviceProvider;
    }
}
