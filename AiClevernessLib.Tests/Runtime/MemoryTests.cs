using AiCleverness.Abstractions;
using AiCleverness.Runtime.Memory;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class MemoryTests
{
    public sealed class AggregateMemoryTests
    {
        [Fact]
        public void Constructor_WithCustomTiers_UsesProvided()
        {
            var working = new InMemoryWorkingMemory();
            var longTerm = new InMemoryLongTermMemory();
            var vector = new InMemoryVectorMemory();

            var aggregate = new DefaultAggregateMemory(working, longTerm, vector);

            aggregate.Working.Should().BeSameAs(working);
            aggregate.LongTerm.Should().BeSameAs(longTerm);
            aggregate.Vector.Should().BeSameAs(vector);
        }

        [Fact]
        public void ExposesAllTiers()
        {
            var aggregate = new DefaultAggregateMemory();

            aggregate.Working.Should().NotBeNull();
            aggregate.LongTerm.Should().NotBeNull();
            aggregate.Vector.Should().NotBeNull();
        }

        [Fact]
        public async Task IAgentMemory_ContainsAsync_Works()
        {
            var aggregate = new DefaultAggregateMemory();
            IAgentMemory memory = aggregate;

            await memory.SaveAsync("exists", 42);

            (await memory.ContainsAsync("exists")).Should().BeTrue();
            (await memory.ContainsAsync("nope")).Should().BeFalse();
        }

        [Fact]
        public async Task IAgentMemory_DelegatesToLongTerm()
        {
            var aggregate = new DefaultAggregateMemory();
            IAgentMemory memory = aggregate;

            await memory.SaveAsync("key", "value");
            var loaded = await memory.LoadAsync<string>("key");

            loaded.Should().Be("value");
        }

        [Fact]
        public async Task IAgentMemory_GetKeysAsync_Works()
        {
            var aggregate = new DefaultAggregateMemory();
            IAgentMemory memory = aggregate;

            await memory.SaveAsync("a", 1);
            await memory.SaveAsync("b", 2);

            var keys = await memory.GetKeysAsync();
            keys.Should().BeEquivalentTo("a", "b");
        }

        [Fact]
        public async Task WorkingMemory_IsSeparateFromLongTerm()
        {
            var aggregate = new DefaultAggregateMemory();

            aggregate.Working.Set("temp", "data");

            // Working memory is not visible through IAgentMemory (which delegates to long-term)
            var contains = await aggregate.ContainsAsync("temp");
            contains.Should().BeFalse();
        }
    }

    public sealed class LongTermMemoryTests
    {
        [Fact]
        public async Task GetKeysAsync_ReturnsAllKeys()
        {
            var memory = new InMemoryLongTermMemory();
            await memory.SaveAsync("a", 1);
            await memory.SaveAsync("b", 2);

            var keys = await memory.GetKeysAsync();

            keys.Should().BeEquivalentTo("a", "b");
        }

        [Fact]
        public async Task GetKeysAsync_WithPrefix_FiltersKeys()
        {
            var memory = new InMemoryLongTermMemory();
            await memory.SaveAsync("user:alice", "data1");
            await memory.SaveAsync("user:bob", "data2");
            await memory.SaveAsync("config:theme", "dark");

            var keys = await memory.GetKeysAsync("user:");

            keys.Should().HaveCount(2);
            keys.Should().AllSatisfy(k => k.Should().StartWith("user:"));
        }

        [Fact]
        public async Task Load_MissingKey_ReturnsDefault()
        {
            var memory = new InMemoryLongTermMemory();

            var result = await memory.LoadAsync<string>("missing");

            result.Should().BeNull();
        }

        [Fact]
        public async Task Remove_ExistingKey_ReturnsTrue()
        {
            var memory = new InMemoryLongTermMemory();
            await memory.SaveAsync("k", "v");

            var removed = await memory.RemoveAsync("k");

            removed.Should().BeTrue();
            (await memory.ContainsAsync("k")).Should().BeFalse();
        }

        [Fact]
        public async Task Remove_MissingKey_ReturnsFalse()
        {
            var memory = new InMemoryLongTermMemory();

            var removed = await memory.RemoveAsync("missing");

            removed.Should().BeFalse();
        }

        [Fact]
        public async Task SaveAndLoad_ComplexObject_RoundTrips()
        {
            var memory = new InMemoryLongTermMemory();
            var data = new TestData("hello", 42);

            await memory.SaveAsync("obj", data);
            var result = await memory.LoadAsync<TestData>("obj");

            result.Should().NotBeNull();
            result!.Name.Should().Be("hello");
            result.Value.Should().Be(42);
        }

        [Fact]
        public async Task SaveAndLoad_RoundTrips()
        {
            var memory = new InMemoryLongTermMemory();
            await memory.SaveAsync("name", "Alice");

            var result = await memory.LoadAsync<string>("name");

            result.Should().Be("Alice");
        }

        private sealed record TestData(string Name, int Value);
    }

    public sealed class VectorMemoryTests
    {
        [Fact]
        public async Task Clear_RemovesAll()
        {
            var memory = new InMemoryVectorMemory();
            await memory.UpsertAsync(new VectorMemoryEntry("a", "t1", new[] { 1.0f }));
            await memory.UpsertAsync(new VectorMemoryEntry("b", "t2", new[] { 0.0f }));

            await memory.ClearAsync();

            (await memory.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task Remove_ReturnsFalse_WhenMissing()
        {
            var memory = new InMemoryVectorMemory();

            (await memory.RemoveAsync("nope")).Should().BeFalse();
        }

        [Fact]
        public async Task Remove_ReturnsTrue_WhenExists()
        {
            var memory = new InMemoryVectorMemory();
            await memory.UpsertAsync(new VectorMemoryEntry("x", "text", new[] { 1.0f }));

            (await memory.RemoveAsync("x")).Should().BeTrue();
            (await memory.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task Search_RespectsMinScore()
        {
            var memory = new InMemoryVectorMemory();
            await memory.UpsertAsync(
                new VectorMemoryEntry("high", "similar", new[] { 1.0f, 0.0f, 0.0f }));
            await memory.UpsertAsync(
                new VectorMemoryEntry("low", "dissimilar", new[] { 0.0f, 1.0f, 0.0f }));

            var results = await memory.SearchAsync(new[] { 1.0f, 0.0f, 0.0f }, minScore: 0.9);

            results.Should().HaveCount(1);
            results[0].Entry.Id.Should().Be("high");
        }

        [Fact]
        public async Task Search_RespectsTopK()
        {
            var memory = new InMemoryVectorMemory();
            for (var i = 0; i < 10; i++)
            {
                await memory.UpsertAsync(
                    new VectorMemoryEntry(
                        $"e{i}",
                        $"entry {i}",
                        new[] { 1.0f, i * 0.1f, 0.0f }));
            }

            var results = await memory.SearchAsync(new[] { 1.0f, 0.0f, 0.0f }, topK: 3);

            results.Should().HaveCount(3);
        }

        [Fact]
        public async Task Search_ReturnsMostSimilarFirst()
        {
            var memory = new InMemoryVectorMemory();
            await memory.UpsertAsync(new VectorMemoryEntry("a", "far", new[] { 0.0f, 1.0f, 0.0f }));
            await memory.UpsertAsync(
                new VectorMemoryEntry("b", "close", new[] { 0.9f, 0.1f, 0.0f }));
            await memory.UpsertAsync(
                new VectorMemoryEntry("c", "closest", new[] { 1.0f, 0.0f, 0.0f }));

            var results = await memory.SearchAsync(new[] { 1.0f, 0.0f, 0.0f });

            results[0].Entry.Id.Should().Be("c");
            results[1].Entry.Id.Should().Be("b");
        }

        [Fact]
        public async Task Upsert_And_Search_ReturnsEntry()
        {
            var memory = new InMemoryVectorMemory();
            var vector = new[] { 1.0f, 0.0f, 0.0f };
            var entry = new VectorMemoryEntry("1", "hello world", vector);
            await memory.UpsertAsync(entry);

            var results = await memory.SearchAsync(new[] { 1.0f, 0.0f, 0.0f });

            results.Should().HaveCount(1);
            results[0].Entry.Text.Should().Be("hello world");
            results[0].Score.Should().BeApproximately(1.0, 0.001);
        }

        [Fact]
        public async Task Upsert_SameId_Overwrites()
        {
            var memory = new InMemoryVectorMemory();
            await memory.UpsertAsync(new VectorMemoryEntry("x", "original", new[] { 1.0f }));
            await memory.UpsertAsync(new VectorMemoryEntry("x", "updated", new[] { 1.0f }));

            (await memory.CountAsync()).Should().Be(1);
            var results = await memory.SearchAsync(new[] { 1.0f });
            results[0].Entry.Text.Should().Be("updated");
        }
    }

    public sealed class WorkingMemoryTests
    {
        [Fact]
        public void Clear_RemovesAll()
        {
            var memory = new InMemoryWorkingMemory();
            memory.Set("a", 1);
            memory.Set("b", 2);

            memory.Clear();

            memory.Count.Should().Be(0);
        }

        [Fact]
        public void Contains_ReturnsExpected()
        {
            var memory = new InMemoryWorkingMemory();
            memory.Set("a", 1);

            memory.Contains("a").Should().BeTrue();
            memory.Contains("b").Should().BeFalse();
        }

        [Fact]
        public void Count_ReflectsEntries()
        {
            var memory = new InMemoryWorkingMemory();
            memory.Set("a", 1);
            memory.Set("b", 2);

            memory.Count.Should().Be(2);
        }

        [Fact]
        public void Get_MissingKey_ReturnsDefault()
        {
            var memory = new InMemoryWorkingMemory();

            memory.Get<string>("missing").Should().BeNull();
            memory.Get<int>("missing").Should().Be(0);
        }

        [Fact]
        public void Get_WrongType_ReturnsDefault()
        {
            var memory = new InMemoryWorkingMemory();
            memory.Set("val", "text");

            memory.Get<int>("val").Should().Be(0);
        }

        [Fact]
        public void Keys_ReturnsAllKeys()
        {
            var memory = new InMemoryWorkingMemory();
            memory.Set("x", 1);
            memory.Set("y", 2);

            memory.Keys.Should().BeEquivalentTo("x", "y");
        }

        [Fact]
        public void Remove_ExistingKey_ReturnsTrue()
        {
            var memory = new InMemoryWorkingMemory();
            memory.Set("k", "v");

            memory.Remove("k").Should().BeTrue();
            memory.Contains("k").Should().BeFalse();
        }

        [Fact]
        public void Remove_MissingKey_ReturnsFalse()
        {
            var memory = new InMemoryWorkingMemory();

            memory.Remove("nope").Should().BeFalse();
        }

        [Fact]
        public void Set_And_Get_ReturnsValue()
        {
            var memory = new InMemoryWorkingMemory();
            memory.Set("count", 42);

            memory.Get<int>("count").Should().Be(42);
        }
    }
}
