using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public class ExecutionArtifactCollectionTests
{
    [Fact]
    public void Add_And_Get_ReturnsArtifact()
    {
        var collection = new DefaultExecutionArtifactCollection();
        var artifact = CreateArtifact("report.txt");

        collection.Add(artifact);

        collection.Get("report.txt").Should().BeSameAs(artifact);
    }

    [Fact]
    public void Add_DuplicateName_OverwritesPrevious()
    {
        var collection = new DefaultExecutionArtifactCollection();
        var first = CreateArtifact("data", "first");
        var second = CreateArtifact("data", "second");

        collection.Add(first);
        collection.Add(second);

        collection.Get("data").Should().BeSameAs(second);
        collection.Count.Should().Be(1);
    }

    [Fact]
    public void Add_NullArtifact_Throws()
    {
        var collection = new DefaultExecutionArtifactCollection();

        var act = () => collection.Add(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Contains_ReturnsExpected()
    {
        var collection = new DefaultExecutionArtifactCollection();
        collection.Add(CreateArtifact("exists"));

        collection.Contains("exists").Should().BeTrue();
        collection.Contains("nope").Should().BeFalse();
    }

    [Fact]
    public void Count_ReflectsNumberOfArtifacts()
    {
        var collection = new DefaultExecutionArtifactCollection();

        collection.Count.Should().Be(0);

        collection.Add(CreateArtifact("a"));
        collection.Add(CreateArtifact("b"));

        collection.Count.Should().Be(2);
    }

    [Fact]
    public void Enumeration_IteratesAllArtifacts()
    {
        var collection = new DefaultExecutionArtifactCollection();
        collection.Add(CreateArtifact("x"));
        collection.Add(CreateArtifact("y"));

        var enumerated = collection.ToArray();

        enumerated.Should().HaveCount(2);
    }

    [Fact]
    public void Get_MissingName_ReturnsNull()
    {
        var collection = new DefaultExecutionArtifactCollection();

        collection.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Get_NullName_Throws()
    {
        var collection = new DefaultExecutionArtifactCollection();

        var act = () => collection.Get(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Names_ReturnsAllArtifactNames()
    {
        var collection = new DefaultExecutionArtifactCollection();
        collection.Add(CreateArtifact("alpha"));
        collection.Add(CreateArtifact("beta"));

        collection.Names.Should().BeEquivalentTo("alpha", "beta");
    }

    [Fact]
    public void ToList_ReturnsAllArtifacts()
    {
        var collection = new DefaultExecutionArtifactCollection();
        var a1 = CreateArtifact("one");
        var a2 = CreateArtifact("two");
        collection.Add(a1);
        collection.Add(a2);

        var list = collection.ToList();

        list.Should().HaveCount(2);
        list.Should().Contain(a1);
        list.Should().Contain(a2);
    }

    private static IExecutionArtifact CreateArtifact(string name, string content = "data") =>
        new ExecutionArtifact(name, content);
}
