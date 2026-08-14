using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public class ExecutionItemsTests
{
    [Fact]
    public void Clear_RemovesAllItems()
    {
        var items = new DefaultExecutionItems();
        items.Set("a", 1);
        items.Set("b", 2);

        items.Clear();

        items.Count.Should().Be(0);
        items.Keys.Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReturnsExpected()
    {
        var items = new DefaultExecutionItems();
        items.Set("a", 1);

        items.Contains("a").Should().BeTrue();
        items.Contains("b").Should().BeFalse();
    }

    [Fact]
    public void Count_ReflectsItemCount()
    {
        var items = new DefaultExecutionItems();

        items.Count.Should().Be(0);

        items.Set("a", 1);
        items.Set("b", 2);

        items.Count.Should().Be(2);
    }

    [Fact]
    public void Get_MissingKey_ReturnsDefault()
    {
        var items = new DefaultExecutionItems();

        items.Get<string>("missing").Should().BeNull();
        items.Get<int>("missing").Should().Be(0);
    }

    [Fact]
    public void Get_WrongType_ReturnsDefault()
    {
        var items = new DefaultExecutionItems();
        items.Set("value", "hello");

        items.Get<int>("value").Should().Be(0);
    }

    [Fact]
    public void GetOrAdd_WhenMissing_CallsFactory()
    {
        var items = new DefaultExecutionItems();

        var result = items.GetOrAdd("list", () => new List<string> { "initial" });

        result.Should().BeEquivalentTo("initial");
        items.Contains("list").Should().BeTrue();
    }

    [Fact]
    public void GetOrAdd_WhenPresent_DoesNotCallFactory()
    {
        var items = new DefaultExecutionItems();
        items.Set("val", 10);
        var factoryCalled = false;

        var result = items.GetOrAdd(
            "val",
            () =>
                {
                    factoryCalled = true;
                    return 99;
                });

        result.Should().Be(10);
        factoryCalled.Should().BeFalse();
    }

    [Fact]
    public void Keys_ReturnsAllKeys()
    {
        var items = new DefaultExecutionItems();
        items.Set("x", 1);
        items.Set("y", 2);
        items.Set("z", 3);

        items.Keys.Should().BeEquivalentTo("x", "y", "z");
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        var items = new DefaultExecutionItems();
        items.Set("key", "value");

        items.Remove("key").Should().BeTrue();
        items.Contains("key").Should().BeFalse();
    }

    [Fact]
    public void Remove_MissingKey_ReturnsFalse()
    {
        var items = new DefaultExecutionItems();

        items.Remove("nope").Should().BeFalse();
    }

    [Fact]
    public void Set_And_Get_ReturnsTypedValue()
    {
        var items = new DefaultExecutionItems();

        items.Set("count", 42);

        items.Get<int>("count").Should().Be(42);
    }

    [Fact]
    public void Set_NullKey_Throws()
    {
        var items = new DefaultExecutionItems();

        var act = () => items.Set<string>(null!, "value");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Set_NullValue_Throws()
    {
        var items = new DefaultExecutionItems();

        var act = () => items.Set<string>("key", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Set_Overwrites_ExistingValue()
    {
        var items = new DefaultExecutionItems();
        items.Set("key", "first");

        items.Set("key", "second");

        items.Get<string>("key").Should().Be("second");
    }
}
