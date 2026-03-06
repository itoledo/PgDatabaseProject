using System;
using System.IO;
using Microsoft.VisualStudio.Shell;

namespace PgDatabaseProject.Extension;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class ProvideTextMateGrammarDirectoryAttribute : RegistrationAttribute
{
    private readonly string _name;
    private readonly string _relativePath;

    public ProvideTextMateGrammarDirectoryAttribute(string name, string relativePath)
    {
        _name = name;
        _relativePath = relativePath;
    }

    public override void Register(RegistrationContext context)
    {
        using var key = context.CreateKey(@"TextMate\Repositories");
        key.SetValue(_name, Path.Combine(context.ComponentPath, _relativePath));
    }

    public override void Unregister(RegistrationContext context)
    {
        context.RemoveKey(@"TextMate\Repositories");
    }
}
