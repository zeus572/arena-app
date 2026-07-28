using Arena.API.Services.Email;
using FluentAssertions;
using Xunit;

namespace Arena.UnitTests;

/// <summary>
/// Regression cover for the 2026-07-14 → 2026-07-28 outage: the sender was built as
/// "Display Name &lt;address&gt;", which ACS rejects with a 400 ("Request body
/// validation error. See property 'senderAddress'"). Every verification and
/// password-reset email failed for 13 days. These tests pin the bare-address
/// contract so the display-name form can't come back.
/// </summary>
public class AcsEmailSenderTests
{
    private static EmailOptions Options(string senderName) => new()
    {
        SenderAddress = "DoNotReply@notify.civersify.com",
        SenderName = senderName,
    };

    private static Azure.Communication.Email.EmailMessage Build(EmailOptions options) =>
        AcsEmailSender.BuildMessage(options, "user@example.com", "Subject", "<p>html</p>", "text");

    [Theory]
    [InlineData("Political Arena")]
    [InlineData("")]
    [InlineData("   ")]
    public void Sender_address_is_always_the_bare_address(string senderName)
    {
        var message = Build(Options(senderName));

        // The bug: a configured SenderName turned this into
        // "Political Arena <DoNotReply@notify.civersify.com>".
        message.SenderAddress.Should().Be("DoNotReply@notify.civersify.com");
    }

    [Fact]
    public void Sender_address_never_uses_the_rfc5322_display_name_form()
    {
        var message = Build(Options("Political Arena"));

        message.SenderAddress.Should().NotContain("<").And.NotContain(">");
        message.SenderAddress.Should().NotContain(" ");
    }

    [Fact]
    public void Carries_recipient_and_both_content_bodies()
    {
        var message = Build(Options("Political Arena"));

        message.Recipients.To.Should().ContainSingle()
            .Which.Address.Should().Be("user@example.com");
        message.Content.Subject.Should().Be("Subject");
        message.Content.Html.Should().Be("<p>html</p>");
        message.Content.PlainText.Should().Be("text");
    }
}
