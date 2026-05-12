using System.ComponentModel;
using System.Reflection;
using EmailMcp;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace EmailMcp.Tests;

public class ToolDiscoveryTests
{
    private static readonly Type ToolType = typeof(EmailTools);

    [Fact]
    public void EmailTools_HasMcpServerToolTypeAttribute()
    {
        Assert.NotNull(ToolType.GetCustomAttribute<McpServerToolTypeAttribute>());
    }

    [Theory]
    [InlineData("SendEmail")]
    [InlineData("ReadEmails")]
    [InlineData("GetEmail")]
    [InlineData("SearchEmails")]
    [InlineData("ReplyToEmail")]
    [InlineData("ForwardEmail")]
    [InlineData("DownloadAttachments")]
    [InlineData("CreateFolder")]
    [InlineData("DeleteFolder")]
    [InlineData("RenameFolder")]
    [InlineData("MoveToFolder")]
    [InlineData("ListFolders")]
    [InlineData("MessageCounts")]
    [InlineData("AddFlags")]
    [InlineData("RemoveFlags")]
    [InlineData("CreateDraft")]
    public void Tool_Exists_WithMcpServerToolAttribute(string methodName)
    {
        var method = ToolType.GetMethod(methodName);
        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<McpServerToolAttribute>());
    }

    [Theory]
    [InlineData("SendEmail")]
    [InlineData("ReadEmails")]
    [InlineData("GetEmail")]
    [InlineData("SearchEmails")]
    [InlineData("ReplyToEmail")]
    [InlineData("ForwardEmail")]
    [InlineData("DownloadAttachments")]
    [InlineData("CreateFolder")]
    [InlineData("DeleteFolder")]
    [InlineData("RenameFolder")]
    [InlineData("MoveToFolder")]
    [InlineData("ListFolders")]
    [InlineData("MessageCounts")]
    [InlineData("AddFlags")]
    [InlineData("RemoveFlags")]
    [InlineData("CreateDraft")]
    public void Tool_HasDescription(string methodName)
    {
        var method = ToolType.GetMethod(methodName)!;
        var desc = method.GetCustomAttribute<DescriptionAttribute>();
        Assert.NotNull(desc);
        Assert.False(string.IsNullOrWhiteSpace(desc.Description));
    }

    [Theory]
    [InlineData("SendEmail")]
    [InlineData("ReadEmails")]
    [InlineData("GetEmail")]
    [InlineData("SearchEmails")]
    [InlineData("ReplyToEmail")]
    [InlineData("ForwardEmail")]
    [InlineData("DownloadAttachments")]
    [InlineData("CreateFolder")]
    [InlineData("DeleteFolder")]
    [InlineData("RenameFolder")]
    [InlineData("MoveToFolder")]
    [InlineData("ListFolders")]
    [InlineData("MessageCounts")]
    [InlineData("AddFlags")]
    [InlineData("RemoveFlags")]
    [InlineData("CreateDraft")]
    public void Tool_ReturnsTaskString(string methodName)
    {
        var method = ToolType.GetMethod(methodName)!;
        Assert.Equal(typeof(Task<string>), method.ReturnType);
    }

    [Theory]
    [InlineData("SendEmail")]
    [InlineData("ReadEmails")]
    [InlineData("GetEmail")]
    [InlineData("SearchEmails")]
    [InlineData("ReplyToEmail")]
    [InlineData("ForwardEmail")]
    [InlineData("DownloadAttachments")]
    [InlineData("ListFolders")]
    [InlineData("MessageCounts")]
    [InlineData("AddFlags")]
    [InlineData("RemoveFlags")]
    [InlineData("CreateDraft")]
    public void Tool_AcceptsIOptionsEmailSettings(string methodName)
    {
        var method = ToolType.GetMethod(methodName)!;
        var firstParam = method.GetParameters()[0];
        Assert.Equal(typeof(IOptions<EmailSettings>), firstParam.ParameterType);
    }

    [Fact]
    public void Tool_Count_Is_16()
    {
        var tools = ToolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .ToList();
        Assert.Equal(16, tools.Count);
    }
}
