using AiCleverness.Models;
using AiCleverness.Runtime.Conversation;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class ConversationTests
{
    public sealed class ConversationManagerTests
    {
        [Fact]
        public void AddMessage_IncreasesCount()
        {
            var manager = new DefaultConversationManager();

            manager.AddMessage(new LlmMessage("user", "hi"));

            manager.MessageCount.Should().Be(1);
        }

        [Fact]
        public void AddMessages_AddsAll()
        {
            var manager = new DefaultConversationManager();

            manager.AddMessages(
                [
                    new LlmMessage("user", "one"),
                    new LlmMessage("assistant", "two")
                ]);

            manager.MessageCount.Should().Be(2);
        }

        [Fact]
        public void Clear_RemovesAllMessages()
        {
            var manager = new DefaultConversationManager();
            manager.AddMessage(new LlmMessage("user", "msg"));

            manager.Clear();

            manager.MessageCount.Should().Be(0);
            manager.EstimatedTokenCount.Should().Be(0);
        }

        [Fact]
        public void EstimatedTokenCount_CalculatesApproximation()
        {
            var manager = new DefaultConversationManager(charsPerToken: 4);
            manager.AddMessage(new LlmMessage("user", "hello world")); // ~15 chars -> ~4 tokens

            manager.EstimatedTokenCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetMessages_ReturnsInOrder()
        {
            var manager = new DefaultConversationManager();
            manager.AddMessage(new LlmMessage("system", "sys"));
            manager.AddMessage(new LlmMessage("user", "hello"));

            var messages = manager.GetMessages();

            messages[0].Role.Should().Be("system");
            messages[1].Content.Should().Be("hello");
        }

        [Fact]
        public async Task GetMessagesForCompletion_OverBudget_Truncates()
        {
            var manager = new DefaultConversationManager(charsPerToken: 1);
            manager.AddMessage(new LlmMessage("system", "system prompt here"));
            for (var i = 0; i < 100; i++)
            {
                manager.AddMessage(
                    new LlmMessage("user", $"Message number {i} with some content padding."));
            }

            var result = await manager.GetMessagesForCompletionAsync(100);

            result.Should().HaveCountLessThan(101);
            result[0].Role.Should().Be("system");
        }

        [Fact]
        public async Task GetMessagesForCompletion_WithinBudget_ReturnsAll()
        {
            var manager = new DefaultConversationManager(charsPerToken: 1);
            manager.AddMessage(new LlmMessage("user", "hi"));

            var result = await manager.GetMessagesForCompletionAsync(10000);

            result.Should().HaveCount(1);
        }
    }

    public sealed class PromptRendererTests
    {
        [Fact]
        public void Render_MissingVariable_LeavesPlaceholder()
        {
            var renderer = new SimplePromptRenderer();
            var template = new SimplePromptTemplate("t", "Hello {{name}}!");
            var vars = new Dictionary<string, object>();

            var result = renderer.Render(template, vars);

            result.Should().Be("Hello {{name}}!");
        }

        [Fact]
        public void Render_SubstitutesVariables()
        {
            var renderer = new SimplePromptRenderer();
            var template = new SimplePromptTemplate("t", "Hello {{name}}, welcome to {{place}}.");
            var vars =
                new Dictionary<string, object> { ["name"] = "Alice", ["place"] = "Wonderland" };

            var result = renderer.Render(template, vars);

            result.Should().Be("Hello Alice, welcome to Wonderland.");
        }

        [Fact]
        public void RenderMessages_ReturnsUserMessage()
        {
            var renderer = new SimplePromptRenderer();
            var template = new SimplePromptTemplate("t", "Say {{word}}");
            var vars = new Dictionary<string, object> { ["word"] = "hello" };

            var messages = renderer.RenderMessages(template, vars);

            messages.Should().HaveCount(1);
            messages[0].Role.Should().Be("user");
            messages[0].Content.Should().Be("Say hello");
        }
    }

    public sealed class PromptTemplateTests
    {
        [Fact]
        public void Name_IsSet()
        {
            var template = new SimplePromptTemplate("greeting", "Hi {{user}}");

            template.Name.Should().Be("greeting");
        }

        [Fact]
        public void Variables_ExtractsPlaceholders()
        {
            var template = new SimplePromptTemplate("test", "Hello {{name}}, you are {{role}}.");

            template.Variables.Should().BeEquivalentTo("name", "role");
        }

        [Fact]
        public void Variables_NoDuplicates()
        {
            var template = new SimplePromptTemplate("t", "{{x}} and {{x}} again");

            template.Variables.Should().HaveCount(1);
        }

        [Fact]
        public void Version_DefaultsToOnePointZero()
        {
            var template = new SimplePromptTemplate("t", "text");

            template.Version.VersionString.Should().Be("1.0.0");
        }
    }

    public sealed class PromptVersionMetadataTests
    {
        [Fact]
        public void AllProperties_Settable()
        {
            var meta = new PromptVersionMetadata
                           {
                               VersionString = "2.0.0",
                               Author = "Alice",
                               ChangeDescription = "Updated prompt",
                               CreatedAt = DateTimeOffset.UtcNow,
                               Tags = ["production"]
                           };

            meta.Author.Should().Be("Alice");
            meta.Tags.Should().Contain("production");
        }

        [Fact]
        public void IsActive_DefaultsToTrue()
        {
            var meta = new PromptVersionMetadata { VersionString = "1.0.0" };

            meta.IsActive.Should().BeTrue();
        }
    }

    public sealed class TruncationStrategyTests
    {
        [Fact]
        public void Truncate_Empty_ReturnsEmpty()
        {
            var strategy = new SlidingWindowTruncationStrategy();
            var result = strategy.Truncate([], maxTokens: 1000);

            result.Should().BeEmpty();
        }

        [Fact]
        public void Truncate_NoSystemMessage_KeepsRecent()
        {
            var strategy = new SlidingWindowTruncationStrategy(charsPerToken: 1);
            var messages = new List<LlmMessage>
                               {
                                   new("user", "first"),
                                   new("assistant", "response"),
                                   new("user", "last")
                               };

            var result = strategy.Truncate(messages, maxTokens: 20);

            result[^1].Content.Should().Be("last");
        }

        [Fact]
        public void Truncate_OverBudget_KeepsSystemAndRecent()
        {
            var strategy = new SlidingWindowTruncationStrategy(charsPerToken: 1);
            var messages = new List<LlmMessage>
                               {
                                   new("system", "sys"), // ~7 chars
                                   new("user", "old message 1"), // ~18 chars
                                   new("user", "old message 2"), // ~18 chars
                                   new("user", "recent"), // ~10 chars
                                   new("assistant", "reply") // ~14 chars
                               };

            // Budget: only room for system + last 2 messages (~31 chars)
            var result = strategy.Truncate(messages, maxTokens: 35);

            result.Should().HaveCountGreaterThanOrEqualTo(2);
            result[0].Role.Should().Be("system");
            result[^1].Content.Should().Be("reply");
        }

        [Fact]
        public void Truncate_WithinBudget_ReturnsAll()
        {
            var strategy = new SlidingWindowTruncationStrategy(charsPerToken: 1);
            var messages = new List<LlmMessage>
                               {
                                   new("system", "You are helpful."),
                                   new("user", "Hi"),
                                   new("assistant", "Hello!")
                               };

            var result = strategy.Truncate(messages, maxTokens: 1000);

            result.Should().HaveCount(3);
        }
    }
}
