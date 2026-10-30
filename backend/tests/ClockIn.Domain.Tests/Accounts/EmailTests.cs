using AwesomeAssertions;
using ClockIn.Domain.Accounts;

namespace ClockIn.Domain.Tests.Accounts;

/// <summary>create-user.feature, rule "Email is unique, ignoring case and surrounding spaces".</summary>
public sealed class EmailTests
{
    [Theory]
    [InlineData("eve@clockin.test")]
    [InlineData("EVE@ClockIn.Test")]
    [InlineData("  eve@clockin.test  ")]
    public void Stores_the_email_trimmed_and_lowercased_when_valid(string input)
    {
        new Email(input).Value.Should().Be("eve@clockin.test");
    }

    [Fact]
    public void Treats_emails_as_equal_when_only_case_and_spaces_differ()
    {
        new Email(" EVE@ClockIn.Test ").Should().Be(new Email("eve@clockin.test"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("eve@")]
    [InlineData("@clockin.test")]
    [InlineData("eve@@clockin.test")]
    public void Rejects_the_email_when_malformed(string input)
    {
        Email.TryCreate(input, out Email? email).Should().BeFalse();
        email.Should().BeNull();
        var act = () => new Email(input);
        act.Should().Throw<ArgumentException>();
    }
}
